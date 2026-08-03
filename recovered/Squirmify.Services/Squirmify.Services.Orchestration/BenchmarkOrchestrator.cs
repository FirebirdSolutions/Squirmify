using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Squirmify.Core.DTOs;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;
using Squirmify.Data.Database;
using Squirmify.Services.Evaluation;

namespace Squirmify.Services.Orchestration;

public class BenchmarkOrchestrator : IBenchmarkOrchestrator
{
	private record ValidationResult(bool StrictPass, bool LenientPass, string? Reason = null);

	private record ContextWindowTestResultBundle(ContextWindowTestResult Result, List<ContextWindowProbe> Probes);

	private record FailureDetail(int TokenPosition, string SecretWord, string Response, string FailureType);

	private readonly IServiceScopeFactory _scopeFactory;

	private readonly Dictionary<int, RunProgress> _activeRuns = new Dictionary<int, RunProgress>();

	private readonly Dictionary<int, CancellationTokenSource> _cancellationTokens = new Dictionary<int, CancellationTokenSource>();

	private readonly Dictionary<int, int> _completedTests = new Dictionary<int, int>();

	public event Action<RunProgress>? OnProgressUpdate;

	public event Action<LogEvent>? OnLogEvent;

	public BenchmarkOrchestrator(IServiceScopeFactory scopeFactory)
	{
		_scopeFactory = scopeFactory;
	}

	public Task<int> StartRunAsync(int configId, int providerId, string? runName = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		return StartRunAsync(configId, providerId, null, null, runName, cancellationToken);
	}

	public async Task<int> StartRunAsync(int configId, int providerId, int? modelGroupId, int? judgeModelId, string? runName = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		Console.WriteLine($"[Orchestrator] Starting run with configId={configId}, providerId={providerId}, modelGroupId={modelGroupId}, name={runName ?? "(unnamed)"}");
		using IServiceScope scope = _scopeFactory.CreateScope();
		IConfigRepository configRepo = scope.ServiceProvider.GetRequiredService<IConfigRepository>();
		IProviderRepository providerRepo = scope.ServiceProvider.GetRequiredService<IProviderRepository>();
		IBenchmarkRepository benchmarkRepo = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
		TestSuiteConfig config = (await configRepo.GetConfigByIdAsync(configId)) ?? throw new ArgumentException($"Config {configId} not found");
		Console.WriteLine("[Orchestrator] Loaded config: " + config.Name);
		Provider provider = (await providerRepo.GetByIdAsync(providerId)) ?? throw new ArgumentException($"Provider {providerId} not found");
		Console.WriteLine("[Orchestrator] Loaded provider: " + provider.Name + " at " + provider.BaseUrl);
		BenchmarkRun run = new BenchmarkRun
		{
			Name = (string.IsNullOrWhiteSpace(runName) ? null : runName.Trim()),
			ConfigId = configId,
			ProviderId = providerId,
			ModelGroupId = modelGroupId,
			BaseJudgeModelId = judgeModelId,
			Status = "running",
			StartedAt = DateTime.Now,
			CreatedAt = DateTime.Now
		};
		int runId = (run.Id = await benchmarkRepo.CreateRunAsync(run));
		CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_cancellationTokens[runId] = cts;
		RunProgress progress = new RunProgress
		{
			RunId = runId,
			Stage = "Initializing"
		};
		_activeRuns[runId] = progress;
		_completedTests[runId] = 0;
		try
		{
			Console.WriteLine($"[Orchestrator] Executing run {runId}...");
			await ExecuteRunAsync(run, config, provider, cts.Token);
			using IServiceScope completeScope = _scopeFactory.CreateScope();
			IBenchmarkRepository completeRepo = completeScope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
			await completeRepo.UpdateRunStatusAsync(runId, "completed");
			Console.WriteLine($"[Orchestrator] Run {runId} completed successfully");
			Log(runId, "info", "Benchmark run completed successfully");
		}
		catch (OperationCanceledException)
		{
			using IServiceScope cancelScope = _scopeFactory.CreateScope();
			IBenchmarkRepository cancelRepo = cancelScope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
			await cancelRepo.UpdateRunStatusAsync(runId, "cancelled");
			Console.WriteLine($"[Orchestrator] Run {runId} was cancelled");
			Log(runId, "warning", "Benchmark run was cancelled");
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			using IServiceScope errorScope = _scopeFactory.CreateScope();
			IBenchmarkRepository errorRepo = errorScope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
			await errorRepo.UpdateRunStatusAsync(runId, "failed");
			Console.WriteLine($"[Orchestrator] Run {runId} FAILED: {ex3.Message}");
			Console.WriteLine("[Orchestrator] Stack trace: " + ex3.StackTrace);
			Log(runId, "error", "Benchmark run failed: " + ex3.Message);
			if (!(ex2 is Exception source))
			{
				throw ex2;
			}
			ExceptionDispatchInfo.Capture(source).Throw();
		}
		finally
		{
			_activeRuns.Remove(runId);
			_cancellationTokens.Remove(runId);
			_completedTests.Remove(runId);
		}
		return runId;
	}

	private async Task ExecuteRunAsync(BenchmarkRun run, TestSuiteConfig config, Provider provider, CancellationToken ct)
	{
		using IServiceScope scope = _scopeFactory.CreateScope();
		IBenchmarkRepository benchmarkRepo = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
		IModelRepository modelRepo = scope.ServiceProvider.GetRequiredService<IModelRepository>();
		ITestDefinitionRepository testDefRepo = scope.ServiceProvider.GetRequiredService<ITestDefinitionRepository>();
		ISeedRepository seedRepo = scope.ServiceProvider.GetRequiredService<ISeedRepository>();
		ILlmClient llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();
		UpdateProgress(run.Id, "Loading Models", 0, 0);
		List<Model> models;
		if (run.ModelGroupId.HasValue)
		{
			IModelGroupRepository groupRepo = scope.ServiceProvider.GetRequiredService<IModelGroupRepository>();
			models = (await groupRepo.GetModelsAsync(run.ModelGroupId.Value)).Where((Model m) => m.CanTest).ToList();
			Console.WriteLine($"[Orchestrator] Loaded {models.Count} testable model(s) from model group {run.ModelGroupId}");
			Log(run.Id, "info", $"Loaded {models.Count} model(s) from model group");
		}
		else
		{
			models = await LoadModelsAsync(run.Id, provider, modelRepo, llmClient);
		}
		if (!models.Any())
		{
			throw new InvalidOperationException("No models available for testing");
		}
		int totalTests = 0;
		if (config.RunQualificationTests)
		{
			int instructionCount = (await testDefRepo.GetInstructionTestsAsync()).Count();
			int reasoningCount = (await testDefRepo.GetReasoningTestsAsync()).Count();
			if (config.MaxInstructionTests > 0)
			{
				instructionCount = Math.Min(instructionCount, config.MaxInstructionTests);
			}
			if (config.MaxReasoningTests > 0)
			{
				reasoningCount = Math.Min(reasoningCount, config.MaxReasoningTests);
			}
			totalTests += (instructionCount + reasoningCount) * models.Count;
		}
		if (config.RunConversationTests)
		{
			int conversationCount = (await testDefRepo.GetConversationTestsAsync()).Count();
			if (config.MaxConversationTests > 0)
			{
				conversationCount = Math.Min(conversationCount, config.MaxConversationTests);
			}
			totalTests += conversationCount * models.Count;
		}
		if (config.RunPromptTests)
		{
			IEnumerable<Seed> allSeeds = await seedRepo.GetAllAsync();
			int augmentedCount = allSeeds.Count((Seed s) => s.IsAugmented);
			int baseCount = allSeeds.Count((Seed s) => !s.IsAugmented);
			int seedCount = ((augmentedCount > 0) ? Math.Min(augmentedCount, config.TargetSeedCount) : Math.Min(baseCount, config.TargetSeedCount));
			totalTests += seedCount * models.Count;
		}
		if (config.RunContextWindowTests && config.ContextWindowLevel != "none")
		{
			int contextWindowCount;
			if (!string.IsNullOrEmpty(config.ContextWindowTestType))
			{
				contextWindowCount = 1;
			}
			else if (!string.IsNullOrEmpty(config.ContextWindowTestIds))
			{
				contextWindowCount = config.ContextWindowTestIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Count((string s) => int.TryParse(s.Trim(), out var _));
			}
			else
			{
				contextWindowCount = (await testDefRepo.GetContextWindowTestsAsync()).Count();
				if (config.ContextWindowMaxTests > 0)
				{
					contextWindowCount = Math.Min(contextWindowCount, config.ContextWindowMaxTests);
				}
			}
			totalTests += contextWindowCount * models.Count;
		}
		if (config.RunMcpToolTests)
		{
			int mcpToolCount = (await testDefRepo.GetMcpToolTestsAsync()).Count();
			if (config.MaxMcpToolTests > 0)
			{
				mcpToolCount = Math.Min(mcpToolCount, config.MaxMcpToolTests);
			}
			totalTests += mcpToolCount * models.Count;
		}
		run.TotalModels = models.Count;
		run.TotalTests = totalTests;
		await benchmarkRepo.UpdateRunAsync(run);
		foreach (Model model in models)
		{
			await benchmarkRepo.AddRunModelAsync(new BenchmarkRunModel
			{
				RunId = run.Id,
				ModelId = model.Id,
				Status = "pending"
			});
		}
		ct.ThrowIfCancellationRequested();
		if (config.RunQualificationTests)
		{
			UpdateProgress(run.Id, "Instruction Tests", 0, models.Count);
			int qualifiedCount = await RunInstructionTestsAsync(run, config, provider, models, ct);
			Log(run.Id, "info", $"Instruction tests complete: {qualifiedCount}/{models.Count} models met {config.InstructionPassThreshold:P0} threshold");
			UpdateProgress(run.Id, "Reasoning Tests", 0, models.Count);
			await RunReasoningTestsAsync(run, config, provider, models, ct);
		}
		ct.ThrowIfCancellationRequested();
		if (config.RunConversationTests)
		{
			UpdateProgress(run.Id, "Conversation Tests", 0, models.Count);
			await RunConversationTestsAsync(run, config, provider, models, ct);
		}
		ct.ThrowIfCancellationRequested();
		if (config.RunContextWindowTests && config.ContextWindowLevel != "none")
		{
			UpdateProgress(run.Id, "Context Window Tests", 0, models.Count);
			await RunContextWindowTestsAsync(run, config, provider, models, ct);
		}
		ct.ThrowIfCancellationRequested();
		if (config.RunMcpToolTests)
		{
			UpdateProgress(run.Id, "MCP Tool Tests", 0, models.Count);
			await RunMcpToolTestsAsync(run, config, provider, models, ct);
		}
		ct.ThrowIfCancellationRequested();
		if (config.RunPromptTests)
		{
			UpdateProgress(run.Id, "Generation Tests", 0, models.Count);
			await RunGenerationTestsAsync(run, config, provider, models, ct);
		}
		ct.ThrowIfCancellationRequested();
		if (config.RunPromptTests && models.Any())
		{
			UpdateProgress(run.Id, "Judging", 0, 1);
			await RunJudgingAsync(run, config, provider, models.First(), ct);
		}
		UpdateProgress(run.Id, "Complete", models.Count, models.Count);
		Log(run.Id, "success", $"Run complete. {models.Count} models tested.");
	}

	private async Task<List<Model>> LoadModelsAsync(int runId, Provider provider, IModelRepository modelRepo, ILlmClient llmClient)
	{
		Console.WriteLine($"[Orchestrator] Loading models from {provider.Name} ({provider.BaseUrl})");
		Log(runId, "info", "Loading models from " + provider.Name);
		Console.WriteLine("[Orchestrator] Fetching models from server API...");
		IEnumerable<string> serverModels = await llmClient.LoadModelsFromServerAsync(provider);
		Console.WriteLine($"[Orchestrator] Server returned {serverModels.Count()} model(s)");
		List<Model> allDbModels = (await modelRepo.GetAllByProviderIncludingDeletedAsync(provider.Id)).ToList();
		Console.WriteLine($"[Orchestrator] Database has {allDbModels.Count} total model(s) for this provider");
		HashSet<string> serverModelSet = serverModels.ToHashSet();
		foreach (string identifier in serverModels)
		{
			Model existing = allDbModels.FirstOrDefault((Model m) => m.Identifier == identifier);
			if (existing == null)
			{
				Console.WriteLine("[Orchestrator] Adding new model: " + identifier);
				Model newModel = new Model
				{
					ProviderId = provider.Id,
					Identifier = identifier,
					DisplayName = identifier,
					IsDisabled = false,
					IsAvailable = true,
					IsDeleted = false,
					CreatedAt = DateTime.UtcNow
				};
				newModel.Id = await modelRepo.CreateAsync(newModel);
				allDbModels.Add(newModel);
			}
			else if (existing.IsDeleted)
			{
				Console.WriteLine("[Orchestrator] Skipping deleted model: " + identifier);
			}
			else if (!existing.IsAvailable)
			{
				Console.WriteLine("[Orchestrator] Model now available: " + identifier);
				existing.IsAvailable = true;
				await modelRepo.SetAvailableAsync(existing.Id, available: true);
			}
		}
		foreach (Model model in allDbModels.Where((Model m) => m.IsAvailable && !m.IsDeleted && !serverModelSet.Contains(m.Identifier)))
		{
			Console.WriteLine("[Orchestrator] Model unavailable: " + model.Identifier);
			model.IsAvailable = false;
			await modelRepo.SetAvailableAsync(model.Id, available: false);
		}
		List<Model> testableModels = allDbModels.Where((Model m) => m.CanTest).ToList();
		Console.WriteLine($"[Orchestrator] Returning {testableModels.Count} testable model(s)");
		Log(runId, "info", $"Found {testableModels.Count} model(s)");
		return testableModels;
	}

	private async Task<int> RunInstructionTestsAsync(BenchmarkRun run, TestSuiteConfig config, Provider provider, List<Model> models, CancellationToken ct)
	{
		using IServiceScope scope = _scopeFactory.CreateScope();
		ITestDefinitionRepository testDefRepo = scope.ServiceProvider.GetRequiredService<ITestDefinitionRepository>();
		IConfigRepository configRepo = scope.ServiceProvider.GetRequiredService<IConfigRepository>();
		IResultsRepository resultsRepo = scope.ServiceProvider.GetRequiredService<IResultsRepository>();
		IBenchmarkRepository benchmarkRepo = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
		ILlmClient llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();
		Console.WriteLine($"[Orchestrator] RunInstructionTestsAsync called with {models.Count} models");
		List<InstructionTest> allTests = (await testDefRepo.GetInstructionTestsAsync()).ToList();
		Console.WriteLine($"[Orchestrator] Found {allTests.Count} instruction tests in database");
		if (allTests.Count == 0)
		{
			Console.WriteLine("[Orchestrator] WARNING: No instruction tests found! Run migration first.");
		}
		List<InstructionTest> tests = ((config.MaxInstructionTests > 0) ? allTests.OrderBy((InstructionTest _) => Random.Shared.Next()).Take(config.MaxInstructionTests).ToList() : allTests);
		if (config.MaxInstructionTests > 0 && allTests.Count > config.MaxInstructionTests)
		{
			Log(run.Id, "info", $"Randomly selected {tests.Count} instruction tests (of {allTests.Count} available)");
		}
		int qualifiedCount = 0;
		IEnumerable<CategorySetting> categorySettings = await configRepo.GetCategorySettingsAsync(config.Id);
		string defaultSystemPrompt = "You are a helpful assistant. Follow instructions precisely.";
		Log(run.Id, "info", $"Running {tests.Count} instruction tests on {models.Count} models");
		int testIndex = 0;
		int totalTests = models.Count * tests.Count;
		for (int i = 0; i < models.Count; i++)
		{
			ct.ThrowIfCancellationRequested();
			Model model = models[i];
			await WarmupModelAsync(run.Id, llmClient, provider, model.Identifier);
			int passed = 0;
			int total = 0;
			foreach (InstructionTest test in tests)
			{
				ct.ThrowIfCancellationRequested();
				testIndex++;
				string testPreview = ((test.Prompt.Length > 50) ? (test.Prompt.Substring(0, 50) + "...") : test.Prompt);
				UpdateProgress(run.Id, "Instruction Tests", testIndex, totalTests, model.Identifier, test.Category + ": " + testPreview);
				CompletionResult result = await llmClient.CompletionAsync(systemPrompt: categorySettings.FirstOrDefault((CategorySetting c) => c.Category == test.Category)?.SystemPrompt ?? defaultSystemPrompt, provider: provider, modelIdentifier: model.Identifier, userPrompt: test.Prompt, temperature: config.GlobalTemperature, topP: config.GlobalTopP, maxTokens: config.GlobalMaxTokens);
				if (result != null)
				{
					ValidationResult validation = ValidateInstructionResult(result.Response, test);
					bool anyPass = validation.StrictPass || validation.LenientPass;
					await resultsRepo.SaveInstructionResultAsync(new InstructionTestResult
					{
						RunId = run.Id,
						ModelId = model.Id,
						TestId = test.Id,
						Passed = anyPass,
						StrictPass = validation.StrictPass,
						LenientPass = validation.LenientPass,
						Response = result.Response,
						FailureReason = validation.Reason,
						TotalMs = result.Perf.TotalMs,
						TokensPerSec = result.Perf.TokensPerSec,
						PromptTokens = result.Perf.PromptTokens,
						CompletionTokens = result.Perf.CompletionTokens,
						CreatedAt = DateTime.UtcNow
					});
					if (anyPass)
					{
						passed++;
					}
					total++;
				}
				else
				{
					total++;
				}
				await IncrementCompletedTests(run.Id, benchmarkRepo);
			}
			double passRate = ((total > 0) ? ((double)passed / (double)total) : 0.0);
			BenchmarkRunModel runModel = await benchmarkRepo.GetRunModelAsync(run.Id, model.Id);
			if (runModel != null)
			{
				runModel.InstructionPassRate = passRate;
				runModel.QualificationPassed = passRate >= config.InstructionPassThreshold;
				runModel.Status = "instruction_complete";
				await benchmarkRepo.UpdateRunModelAsync(runModel);
			}
			if (passRate >= config.InstructionPassThreshold)
			{
				qualifiedCount++;
				Log(run.Id, "info", $"{model.Identifier}: Instruction tests passed ({passRate:P0})");
			}
			else
			{
				Log(run.Id, "warning", $"{model.Identifier}: Instruction tests below threshold ({passRate:P0} < {config.InstructionPassThreshold:P0}) - continuing with all tests");
			}
		}
		return qualifiedCount;
	}

	private ValidationResult ValidateInstructionResult(string response, InstructionTest test)
	{
		response = CleanResponse(response);
		string expected = CleanResponse(test.ExpectedResult);
		string text = test.ValidationType.ToLower();
		if (1 == 0)
		{
		}
		ValidationResult result = text switch
		{
			"exact" => ValidateExact(response, expected), 
			"contains" => ValidateContains(response, expected), 
			"words" => ValidateWords(response, expected, test.StrictOrder), 
			"lines" => ValidateLines(response, expected, test.StrictOrder), 
			"lines_constrained" => ValidateLinesConstrained(response, test), 
			"numeric" => ValidateNumeric(response, test.ExpectedResult.Trim()), 
			"json" => ValidateJson(response, test.ExpectedResult.Trim()), 
			"boolean" => ValidateBoolean(response, test.ExpectedResult.Trim()), 
			_ => ValidateContains(response, expected), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private string CleanResponse(string response)
	{
		if (string.IsNullOrWhiteSpace(response))
		{
			return string.Empty;
		}
		response = response.Trim();
		response = Regex.Replace(response, "^```(?:json)?\\s*", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
		response = Regex.Replace(response, "\\s*```$", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
		if ((response.StartsWith('"') && response.EndsWith('"')) || (response.StartsWith('\'') && response.EndsWith('\'')))
		{
			string text = response;
			response = text.Substring(1, text.Length - 1 - 1);
		}
		response = response.TrimEnd(new char[3] { '.', '!', ';' });
		response = Regex.Replace(response, "[\\u00A0\\uFEFF]", " ");
		response = Regex.Replace(response, "[ \\t]+", " ");
		response = Regex.Replace(response, "\\r\\n|\\r", "\n");
		response = Regex.Replace(response, "\\n{2,}", "\n");
		return response.Trim();
	}

	private ValidationResult ValidateExact(string response, string expected)
	{
		string text = response.Trim().Replace("\r\n", "\n").Replace("\r", "\n");
		string text2 = expected.Trim().Replace("\r\n", "\n").Replace("\r", "\n");
		if (string.Equals(text, text2, StringComparison.Ordinal))
		{
			return new ValidationResult(StrictPass: true, LenientPass: false);
		}
		if (string.Equals(text, text2, StringComparison.OrdinalIgnoreCase))
		{
			return new ValidationResult(StrictPass: false, LenientPass: true, "case difference");
		}
		string a = Regex.Replace(text, "\\s+", "");
		string b = Regex.Replace(text2, "\\s+", "");
		if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
		{
			return new ValidationResult(StrictPass: false, LenientPass: true, "whitespace difference");
		}
		return new ValidationResult(StrictPass: false, LenientPass: false, "content mismatch");
	}

	private ValidationResult ValidateContains(string response, string expected)
	{
		if (response.Contains(expected, StringComparison.Ordinal))
		{
			return new ValidationResult(StrictPass: true, LenientPass: false);
		}
		if (response.Contains(expected, StringComparison.OrdinalIgnoreCase))
		{
			return new ValidationResult(StrictPass: false, LenientPass: true, "case difference");
		}
		return new ValidationResult(StrictPass: false, LenientPass: false, "not found");
	}

	private ValidationResult ValidateWords(string response, string expected, bool strictOrder)
	{
		List<string> list = ExtractWords(response);
		List<string> list2 = ExtractWords(expected);
		if (list.SequenceEqual<string>(list2, StringComparer.OrdinalIgnoreCase))
		{
			return new ValidationResult(StrictPass: true, LenientPass: false);
		}
		HashSet<string> hashSet = list.Select((string w) => w.ToLowerInvariant()).ToHashSet();
		HashSet<string> hashSet2 = list2.Select((string w) => w.ToLowerInvariant()).ToHashSet();
		if (hashSet.SetEquals(hashSet2))
		{
			if (strictOrder)
			{
				return new ValidationResult(StrictPass: false, LenientPass: true, "correct words, wrong order");
			}
			return new ValidationResult(StrictPass: true, LenientPass: false);
		}
		if (hashSet2.IsSubsetOf(hashSet))
		{
			return new ValidationResult(StrictPass: false, LenientPass: true, "extra words added");
		}
		return new ValidationResult(StrictPass: false, LenientPass: false, $"word mismatch: expected {list2.Count}, got {list.Count}");
	}

	private ValidationResult ValidateLines(string response, string expected, bool strictOrder)
	{
		List<string> list = (from l in response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			select l.Trim() into l
			where !string.IsNullOrEmpty(l)
			select l).ToList();
		List<string> list2 = (from l in expected.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			select l.Trim() into l
			where !string.IsNullOrEmpty(l)
			select l).ToList();
		if (list.SequenceEqual<string>(list2, StringComparer.OrdinalIgnoreCase))
		{
			return new ValidationResult(StrictPass: true, LenientPass: false);
		}
		HashSet<string> hashSet = list.Select((string l) => l.ToLowerInvariant()).ToHashSet();
		HashSet<string> hashSet2 = list2.Select((string l) => l.ToLowerInvariant()).ToHashSet();
		if (hashSet.SetEquals(hashSet2))
		{
			if (strictOrder)
			{
				return new ValidationResult(StrictPass: false, LenientPass: true, "correct lines, wrong order");
			}
			return new ValidationResult(StrictPass: true, LenientPass: false);
		}
		List<string> source = (from l in list
			select Regex.Replace(l, "^[\\d\\.\\-\\*\\•\\→]+\\s*", "").Trim() into l
			where !string.IsNullOrEmpty(l)
			select l).ToList();
		HashSet<string> hashSet3 = source.Select((string l) => l.ToLowerInvariant()).ToHashSet();
		if (hashSet3.SetEquals(hashSet2))
		{
			return new ValidationResult(StrictPass: false, LenientPass: true, "correct content with formatting");
		}
		return new ValidationResult(StrictPass: false, LenientPass: false, $"line mismatch: expected {list2.Count}, got {list.Count}");
	}

	private ValidationResult ValidateLinesConstrained(string response, InstructionTest test)
	{
		List<string> source = (from l in response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			select l.Trim() into l
			where !string.IsNullOrEmpty(l)
			select l).ToList();
		List<string> list = (from l in source
			select Regex.Replace(l, "^[\\d\\.\\-\\*\\•\\→]+\\s*", "").Trim() into l
			where !string.IsNullOrEmpty(l)
			select l).ToList();
		int num = test.ExpectedCount ?? ((!string.IsNullOrEmpty(test.ExpectedResult)) ? test.ExpectedResult.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length : 0);
		if (num > 0 && list.Count != num)
		{
			return new ValidationResult(StrictPass: false, LenientPass: false, $"expected {num} lines, got {list.Count}");
		}
		if (!string.IsNullOrEmpty(test.ExcludePatterns))
		{
			try
			{
				List<string> list2 = JsonSerializer.Deserialize<List<string>>(test.ExcludePatterns);
				if (list2 != null)
				{
					foreach (string item in list)
					{
						foreach (string item2 in list2)
						{
							if (item.Contains(item2, StringComparison.OrdinalIgnoreCase))
							{
								return new ValidationResult(StrictPass: false, LenientPass: false, "contains excluded pattern: " + item2);
							}
						}
					}
				}
			}
			catch
			{
			}
		}
		if (!string.IsNullOrEmpty(test.AllowedValues))
		{
			try
			{
				List<string> list3 = JsonSerializer.Deserialize<List<string>>(test.AllowedValues);
				if (list3 != null)
				{
					HashSet<string> hashSet = list3.Select((string a) => a.ToLowerInvariant()).ToHashSet();
					foreach (string item3 in list)
					{
						if (!hashSet.Contains(item3.ToLowerInvariant()))
						{
							return new ValidationResult(StrictPass: false, LenientPass: false, "'" + item3 + "' not in allowed values");
						}
					}
				}
			}
			catch
			{
			}
		}
		return new ValidationResult(StrictPass: true, LenientPass: false);
	}

	private ValidationResult ValidateNumeric(string response, string expected)
	{
		if (!double.TryParse(expected, out var result))
		{
			return new ValidationResult(StrictPass: false, LenientPass: false, "invalid expected value");
		}
		string s = response.Trim();
		if (double.TryParse(s, out var result2))
		{
			if (Math.Abs(result2 - result) < 1E-09)
			{
				return new ValidationResult(StrictPass: true, LenientPass: false);
			}
			if (Math.Abs(result2 - result) < 0.01)
			{
				return new ValidationResult(StrictPass: false, LenientPass: true, "minor numeric difference");
			}
			return new ValidationResult(StrictPass: false, LenientPass: false, $"expected {result}, got {result2}");
		}
		MatchCollection matchCollection = Regex.Matches(response, "-?\\d+\\.?\\d*");
		if (matchCollection.Count == 0)
		{
			return new ValidationResult(StrictPass: false, LenientPass: false, "no number found");
		}
		List<double> list = matchCollection.Select(delegate(Match m)
		{
			double.TryParse(m.Value, out var result3);
			return result3;
		}).ToList();
		double num = list.Last();
		if (Math.Abs(num - result) < 1E-09)
		{
			return new ValidationResult(StrictPass: true, LenientPass: false);
		}
		foreach (double item in list)
		{
			if (Math.Abs(item - result) < 1E-09)
			{
				return new ValidationResult(StrictPass: false, LenientPass: true, "correct number found (not last)");
			}
		}
		if (Math.Abs(num - result) < 0.01)
		{
			return new ValidationResult(StrictPass: false, LenientPass: true, "minor numeric difference");
		}
		return new ValidationResult(StrictPass: false, LenientPass: false, $"expected {result}, got {num}");
	}

	private ValidationResult ValidateJson(string response, string expected)
	{
		try
		{
			Match match = Regex.Match(response, "[\\{\\[].*[\\}\\]]", RegexOptions.Singleline);
			if (match.Success)
			{
				response = match.Value;
			}
			JsonElement a = JsonSerializer.Deserialize<JsonElement>(response);
			JsonElement b = JsonSerializer.Deserialize<JsonElement>(expected);
			if (JsonElementEquals(a, b, strict: true))
			{
				return new ValidationResult(StrictPass: true, LenientPass: false);
			}
			if (JsonElementEquals(a, b, strict: false))
			{
				return new ValidationResult(StrictPass: false, LenientPass: true, "JSON structure matches with type coercion");
			}
			return new ValidationResult(StrictPass: false, LenientPass: false, "JSON content mismatch");
		}
		catch (JsonException)
		{
			return new ValidationResult(StrictPass: false, LenientPass: false, "invalid JSON");
		}
	}

	private bool JsonElementEquals(JsonElement a, JsonElement b, bool strict)
	{
		if (a.ValueKind != b.ValueKind)
		{
			if (!strict)
			{
				string a2 = a.ToString();
				string b2 = b.ToString();
				return string.Equals(a2, b2, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		JsonValueKind valueKind = a.ValueKind;
		if (1 == 0)
		{
		}
		bool result;
		switch (valueKind)
		{
		case JsonValueKind.Object:
			result = JsonObjectEquals(a, b, strict);
			break;
		case JsonValueKind.Array:
			result = JsonArrayEquals(a, b, strict);
			break;
		case JsonValueKind.String:
			result = string.Equals(a.GetString(), b.GetString(), strict ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
			break;
		case JsonValueKind.Number:
			result = a.GetDecimal() == b.GetDecimal();
			break;
		case JsonValueKind.True:
		case JsonValueKind.False:
			result = a.GetBoolean() == b.GetBoolean();
			break;
		case JsonValueKind.Null:
			result = true;
			break;
		default:
			result = a.ToString() == b.ToString();
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	private bool JsonObjectEquals(JsonElement a, JsonElement b, bool strict)
	{
		Dictionary<string, JsonElement> dictionary = a.EnumerateObject().ToDictionary((JsonProperty p) => p.Name, (JsonProperty p) => p.Value);
		Dictionary<string, JsonElement> dictionary2 = b.EnumerateObject().ToDictionary((JsonProperty p) => p.Name, (JsonProperty p) => p.Value);
		if (dictionary.Count != dictionary2.Count)
		{
			return false;
		}
		foreach (var (key, a2) in dictionary)
		{
			if (!dictionary2.TryGetValue(key, out var value))
			{
				return false;
			}
			if (!JsonElementEquals(a2, value, strict))
			{
				return false;
			}
		}
		return true;
	}

	private bool JsonArrayEquals(JsonElement a, JsonElement b, bool strict)
	{
		List<JsonElement> list = a.EnumerateArray().ToList();
		List<JsonElement> list2 = b.EnumerateArray().ToList();
		if (list.Count != list2.Count)
		{
			return false;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (!JsonElementEquals(list[i], list2[i], strict))
			{
				return false;
			}
		}
		return true;
	}

	private ValidationResult ValidateBoolean(string response, string expected)
	{
		string text = response.ToLowerInvariant().Trim();
		string text2 = expected.ToLowerInvariant().Trim();
		if (text == text2)
		{
			return new ValidationResult(StrictPass: true, LenientPass: false);
		}
		HashSet<string> hashSet = new HashSet<string> { "true", "yes", "1", "correct", "affirmative" };
		HashSet<string> hashSet2 = new HashSet<string> { "false", "no", "0", "incorrect", "negative" };
		bool flag = hashSet.Contains(text2);
		if ((hashSet.Contains(text) && flag) || (hashSet2.Contains(text) && !flag))
		{
			return new ValidationResult(StrictPass: true, LenientPass: false);
		}
		Match match = Regex.Match(text, "^(\\w+)");
		string text3 = (match.Success ? match.Groups[1].Value : "");
		bool flag2 = hashSet.Contains(text3);
		bool flag3 = hashSet2.Contains(text3);
		if ((flag2 && flag) || (flag3 && !flag))
		{
			if (text3 == text2)
			{
				return new ValidationResult(StrictPass: false, LenientPass: true, "correct boolean with extra text");
			}
			return new ValidationResult(StrictPass: false, LenientPass: true, "boolean equivalent with extra text");
		}
		return new ValidationResult(StrictPass: false, LenientPass: false, "boolean mismatch");
	}

	private static List<string> ExtractWords(string text)
	{
		return (from w in Regex.Split(text, "[\\s,]+")
			where !string.IsNullOrWhiteSpace(w)
			select w.Trim()).ToList();
	}

	private async Task RunReasoningTestsAsync(BenchmarkRun run, TestSuiteConfig config, Provider provider, List<Model> models, CancellationToken ct)
	{
		using IServiceScope scope = _scopeFactory.CreateScope();
		ITestDefinitionRepository testDefRepo = scope.ServiceProvider.GetRequiredService<ITestDefinitionRepository>();
		IResultsRepository resultsRepo = scope.ServiceProvider.GetRequiredService<IResultsRepository>();
		IBenchmarkRepository benchmarkRepo = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
		ILlmClient llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();
		List<ReasoningTest> allTests = (await testDefRepo.GetReasoningTestsAsync()).ToList();
		List<ReasoningTest> tests = ((config.MaxReasoningTests > 0) ? allTests.OrderBy((ReasoningTest _) => Random.Shared.Next()).Take(config.MaxReasoningTests).ToList() : allTests);
		if (config.MaxReasoningTests > 0 && allTests.Count > config.MaxReasoningTests)
		{
			Log(run.Id, "info", $"Randomly selected {tests.Count} reasoning tests (of {allTests.Count} available)");
		}
		Log(run.Id, "info", $"Running {tests.Count} reasoning tests on {models.Count} models");
		int testIndex = 0;
		int totalTests = models.Count * tests.Count;
		for (int i = 0; i < models.Count; i++)
		{
			ct.ThrowIfCancellationRequested();
			Model model = models[i];
			await WarmupModelAsync(run.Id, llmClient, provider, model.Identifier);
			foreach (ReasoningTest test in tests)
			{
				ct.ThrowIfCancellationRequested();
				testIndex++;
				string testPreview = ((test.Prompt.Length > 50) ? (test.Prompt.Substring(0, 50) + "...") : test.Prompt);
				UpdateProgress(run.Id, "Reasoning Tests", testIndex, totalTests, model.Identifier, test.Category + ": " + testPreview);
				CompletionResult result = await llmClient.CompletionAsync(provider, model.Identifier, "You are a logical reasoning assistant. Think step by step.", test.Prompt, 0.3, config.GlobalTopP, 1000);
				if (result != null)
				{
					(double Overall, double CorrectAnswer, double LogicalSteps, double Clarity, string Reasoning)? scores = await JudgeReasoningResponseAsync(llmClient, provider, model.Identifier, test.Prompt, test.CorrectAnswer, result.Response);
					await resultsRepo.SaveReasoningResultAsync(new ReasoningTestResult
					{
						RunId = run.Id,
						ModelId = model.Id,
						TestId = test.Id,
						Response = result.Response,
						OverallScore = scores?.Overall,
						CorrectAnswerScore = scores?.CorrectAnswer,
						LogicalStepsScore = scores?.LogicalSteps,
						ClarityScore = scores?.Clarity,
						JudgeReasoning = scores?.Reasoning,
						JudgeModelId = model.Id,
						TotalMs = result.Perf.TotalMs,
						TokensPerSec = result.Perf.TokensPerSec,
						PromptTokens = result.Perf.PromptTokens,
						CompletionTokens = result.Perf.CompletionTokens,
						CreatedAt = DateTime.UtcNow
					});
				}
				await IncrementCompletedTests(run.Id, benchmarkRepo);
			}
		}
	}

	private async Task<(double Overall, double CorrectAnswer, double LogicalSteps, double Clarity, string Reasoning)?> JudgeReasoningResponseAsync(ILlmClient llmClient, Provider provider, string modelId, string prompt, string correctAnswer, string response)
	{
		string judgePrompt = $"Evaluate this reasoning response:\r\n\r\nProblem: {prompt}\r\n\r\nCorrect Answer: {correctAnswer}\r\n\r\nModel Response:\r\n{response}\r\n\r\nScore each aspect from 1-10:\r\n1. Correct Answer: Did they arrive at the correct answer?\r\n2. Logical Steps: Were the reasoning steps clear and logical?\r\n3. Clarity: Was the explanation easy to follow?\r\n4. Overall: Overall quality of the response\r\n\r\nRespond in this exact JSON format:\r\n{{\r\n  \"correct_answer\": <1-10>,\r\n  \"logical_steps\": <1-10>,\r\n  \"clarity\": <1-10>,\r\n  \"overall\": <1-10>,\r\n  \"reasoning\": \"<brief explanation>\"\r\n}}";
		try
		{
			CompletionResult judgeResult = await llmClient.CompletionAsync(provider, modelId, "You are an expert evaluator of reasoning responses. Score objectively. You MUST respond with ONLY valid JSON, no other text.", judgePrompt, 0.3, 0.9, 300);
			if (judgeResult == null)
			{
				return null;
			}
			string json = ExtractJson(judgeResult.Response);
			if (json == null)
			{
				return null;
			}
			JsonDocument doc = JsonDocument.Parse(json);
			JsonElement root = doc.RootElement;
			return (GetScoreFromJson(root, "overall"), GetScoreFromJson(root, "correct_answer"), GetScoreFromJson(root, "logical_steps"), GetScoreFromJson(root, "clarity"), root.GetProperty("reasoning").GetString() ?? "");
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Console.WriteLine("[Orchestrator] Failed to judge reasoning: " + ex2.Message);
			return null;
		}
	}

	private double GetScoreFromJson(JsonElement element, string propertyName)
	{
		try
		{
			JsonElement property = element.GetProperty(propertyName);
			if (property.ValueKind == JsonValueKind.Number)
			{
				if (property.TryGetDouble(out var value))
				{
					return value;
				}
				if (property.TryGetInt32(out var value2))
				{
					return value2;
				}
			}
			if (property.ValueKind == JsonValueKind.String)
			{
				string s = property.GetString();
				if (double.TryParse(s, out var result))
				{
					return result;
				}
			}
			return 0.0;
		}
		catch
		{
			return 0.0;
		}
	}

	private async Task RunConversationTestsAsync(BenchmarkRun run, TestSuiteConfig config, Provider provider, List<Model> models, CancellationToken ct)
	{
		using IServiceScope scope = _scopeFactory.CreateScope();
		ITestDefinitionRepository testDefRepo = scope.ServiceProvider.GetRequiredService<ITestDefinitionRepository>();
		IResultsRepository resultsRepo = scope.ServiceProvider.GetRequiredService<IResultsRepository>();
		IBenchmarkRepository benchmarkRepo = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
		ILlmClient llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();
		List<ConversationTest> allTests = (await testDefRepo.GetConversationTestsAsync()).ToList();
		List<ConversationTest> tests = ((config.MaxConversationTests > 0) ? allTests.OrderBy((ConversationTest _) => Random.Shared.Next()).Take(config.MaxConversationTests).ToList() : allTests);
		if (config.MaxConversationTests > 0 && allTests.Count > config.MaxConversationTests)
		{
			Log(run.Id, "info", $"Randomly selected {tests.Count} conversation tests (of {allTests.Count} available)");
		}
		Log(run.Id, "info", $"Running {tests.Count} conversation tests on {models.Count} models");
		int testIndex = 0;
		int totalTests = models.Count * tests.Count;
		for (int i = 0; i < models.Count; i++)
		{
			ct.ThrowIfCancellationRequested();
			Model model = models[i];
			await WarmupModelAsync(run.Id, llmClient, provider, model.Identifier);
			foreach (ConversationTest test in tests)
			{
				ct.ThrowIfCancellationRequested();
				testIndex++;
				UpdateProgress(run.Id, "Conversation Tests", testIndex, totalTests, model.Identifier, test.Category + ": " + (test.Description ?? "Conversation Test"));
				List<ConversationTurn> turns = (await testDefRepo.GetConversationTurnsAsync(test.Id)).ToList();
				List<ConversationExchange> exchanges = new List<ConversationExchange>();
				List<Message> messages = new List<Message>();
				if (!string.IsNullOrEmpty(test.SystemPrompt))
				{
					messages.Add(new Message("system", test.SystemPrompt));
				}
				foreach (ConversationTurn turn in turns)
				{
					messages.Add(new Message("user", turn.UserMessage));
					CompletionResult result = await llmClient.CompletionAsync(provider, model.Identifier, test.SystemPrompt ?? "You are a helpful assistant.", string.Join("\n", from m in messages
						where m.Role == "user"
						select m.Content), 0.7, config.GlobalTopP, config.GlobalMaxTokens);
					if (result != null)
					{
						messages.Add(new Message("assistant", result.Response));
						exchanges.Add(new ConversationExchange
						{
							TurnNumber = turn.TurnNumber,
							UserMessage = turn.UserMessage,
							ModelResponse = result.Response,
							TotalMs = result.Perf.TotalMs,
							TokensPerSec = result.Perf.TokensPerSec,
							PromptTokens = result.Perf.PromptTokens,
							CompletionTokens = result.Perf.CompletionTokens
						});
					}
				}
				await IncrementCompletedTests(run.Id, benchmarkRepo);
				if (exchanges.Any())
				{
					(double Overall, double TopicCoherence, double ConversationalTone, double ContextRetention, double Helpfulness, string Reasoning)? scores = await JudgeConversationAsync(conversation: string.Join("\n\n", exchanges.Select((ConversationExchange e) => "User: " + e.UserMessage + "\nAssistant: " + e.ModelResponse)), llmClient: llmClient, provider: provider, modelId: model.Identifier);
					List<ConversationExchange> validTpsExchanges = exchanges.Where((ConversationExchange e) => e.TokensPerSec.HasValue).ToList();
					double? avgTokensPerSec = (validTpsExchanges.Any() ? new double?(validTpsExchanges.Average((ConversationExchange e) => e.TokensPerSec.Value)) : ((double?)null));
					await resultsRepo.SaveConversationResultAsync(new ConversationTestResult
					{
						RunId = run.Id,
						ModelId = model.Id,
						TestId = test.Id,
						OverallScore = scores?.Overall,
						TopicCoherence = scores?.TopicCoherence,
						ConversationalTone = scores?.ConversationalTone,
						ContextRetention = scores?.ContextRetention,
						Helpfulness = scores?.Helpfulness,
						JudgeReasoning = scores?.Reasoning,
						JudgeModelId = model.Id,
						TotalMs = exchanges.Sum((ConversationExchange e) => e.TotalMs.GetValueOrDefault()),
						TokensPerSec = avgTokensPerSec,
						PromptTokens = exchanges.Sum((ConversationExchange e) => e.PromptTokens.GetValueOrDefault()),
						CompletionTokens = exchanges.Sum((ConversationExchange e) => e.CompletionTokens.GetValueOrDefault()),
						CreatedAt = DateTime.UtcNow
					}, exchanges);
				}
			}
		}
	}

	private async Task<(double Overall, double TopicCoherence, double ConversationalTone, double ContextRetention, double Helpfulness, string Reasoning)?> JudgeConversationAsync(ILlmClient llmClient, Provider provider, string modelId, string conversation)
	{
		string judgePrompt = "Evaluate this conversation:\r\n\r\n" + conversation + "\r\n\r\nScore each aspect from 1-10:\r\n1. Topic Coherence: Did responses stay on topic?\r\n2. Conversational Tone: Was the tone natural and appropriate?\r\n3. Context Retention: Did the assistant remember earlier context?\r\n4. Helpfulness: Were the responses helpful and informative?\r\n5. Overall: Overall quality of the conversation\r\n\r\nRespond in this exact JSON format:\r\n{\r\n  \"topic_coherence\": <1-10>,\r\n  \"conversational_tone\": <1-10>,\r\n  \"context_retention\": <1-10>,\r\n  \"helpfulness\": <1-10>,\r\n  \"overall\": <1-10>,\r\n  \"reasoning\": \"<brief explanation>\"\r\n}";
		try
		{
			CompletionResult judgeResult = await llmClient.CompletionAsync(provider, modelId, "You are an expert evaluator of AI conversations. Score objectively. You MUST respond with ONLY valid JSON, no other text.", judgePrompt, 0.3, 0.9, 300);
			if (judgeResult == null)
			{
				return null;
			}
			string json = ExtractJson(judgeResult.Response);
			if (json == null)
			{
				Console.WriteLine("[Orchestrator] No JSON found in conversation judge response: " + judgeResult.Response.Substring(0, Math.Min(200, judgeResult.Response.Length)) + "...");
				return null;
			}
			JsonDocument doc = JsonDocument.Parse(json);
			JsonElement root = doc.RootElement;
			JsonElement reasoningProp;
			(double Overall, double TopicCoherence, double ConversationalTone, double ContextRetention, double Helpfulness, string Reasoning) result = (Overall: GetScoreFromJson(root, "overall"), TopicCoherence: GetScoreFromJson(root, "topic_coherence"), ConversationalTone: GetScoreFromJson(root, "conversational_tone"), ContextRetention: GetScoreFromJson(root, "context_retention"), Helpfulness: GetScoreFromJson(root, "helpfulness"), Reasoning: root.TryGetProperty("reasoning", out reasoningProp) ? (reasoningProp.GetString() ?? "") : "");
			Console.WriteLine($"[Orchestrator] Conversation scores: Overall={result.Overall:F1}, Topic={result.TopicCoherence:F1}, Tone={result.ConversationalTone:F1}");
			return result;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Console.WriteLine("[Orchestrator] Failed to judge conversation: " + ex2.Message);
			return null;
		}
	}

	private string? ExtractJson(string response)
	{
		response = Regex.Replace(response, "```json\\s*", "");
		response = Regex.Replace(response, "```\\s*", "");
		response = response.Trim();
		int num = response.IndexOf('{');
		int num2 = response.LastIndexOf('}');
		if (num >= 0 && num2 > num)
		{
			return response.Substring(num, num2 - num + 1);
		}
		return null;
	}

	private async Task RunContextWindowTestsAsync(BenchmarkRun run, TestSuiteConfig config, Provider provider, List<Model> models, CancellationToken ct)
	{
		using IServiceScope scope = _scopeFactory.CreateScope();
		ITestDefinitionRepository testDefRepo = scope.ServiceProvider.GetRequiredService<ITestDefinitionRepository>();
		IResultsRepository resultsRepo = scope.ServiceProvider.GetRequiredService<IResultsRepository>();
		IBenchmarkRepository benchmarkRepo = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
		ILlmClient llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();
		List<ContextWindowTest> allTests = (await testDefRepo.GetContextWindowTestsAsync()).ToList();
		List<ContextWindowTest> tests;
		if (!string.IsNullOrEmpty(config.ContextWindowTestType))
		{
			tests = allTests.Where((ContextWindowTest t) => t.Name.Equals(config.ContextWindowTestType, StringComparison.OrdinalIgnoreCase)).ToList();
			if (tests.Count == 0)
			{
				Log(run.Id, "warning", "No test found matching type '" + config.ContextWindowTestType + "'. Available: " + string.Join(", ", allTests.Select((ContextWindowTest t) => t.Name)));
			}
			else
			{
				Log(run.Id, "info", "Using context window test: " + config.ContextWindowTestType);
			}
		}
		else if (string.IsNullOrEmpty(config.ContextWindowTestIds))
		{
			tests = ((config.ContextWindowMaxTests <= 0) ? allTests : allTests.OrderBy((ContextWindowTest _) => Random.Shared.Next()).Take(config.ContextWindowMaxTests).ToList());
		}
		else
		{
			HashSet<int> selectedIds = (from s in config.ContextWindowTestIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
				select int.TryParse(s.Trim(), out var result2) ? result2 : (-1) into id
				where id > 0
				select id).ToHashSet();
			tests = allTests.Where((ContextWindowTest t) => selectedIds.Contains(t.Id)).ToList();
			Log(run.Id, "info", $"Using {tests.Count} selected context window tests (legacy mode)");
		}
		int configTargetTokens = ((config.ContextWindowTargetTokens > 0) ? config.ContextWindowTargetTokens : 32000);
		int configProbeCount = ((config.ContextWindowProbeCount > 0) ? config.ContextWindowProbeCount : 10);
		int configCheckpointCount = ((config.ContextWindowCheckpoints > 0) ? config.ContextWindowCheckpoints : 4);
		if (tests.Count == 0)
		{
			Log(run.Id, "info", "No context window tests in database - using config values");
			tests = new List<ContextWindowTest>
			{
				new ContextWindowTest
				{
					Id = -1,
					Name = $"Context Test ({configTargetTokens / 1000}K)",
					Description = "Auto-generated from config",
					FillerType = "mixed",
					BaseTargetTokens = configTargetTokens,
					BaseCheckpointCount = configCheckpointCount,
					IsActive = true
				}
			};
		}
		Log(run.Id, "info", $"Running {tests.Count} context window tests on {models.Count} models");
		int testIndex = 0;
		int totalTests = models.Count * tests.Count;
		foreach (Model model in models)
		{
			ct.ThrowIfCancellationRequested();
			await WarmupModelAsync(run.Id, llmClient, provider, model.Identifier);
			List<(int maxReliable, int target, double accuracy)> modelResults = new List<(int, int, double)>();
			foreach (ContextWindowTest test in tests)
			{
				ct.ThrowIfCancellationRequested();
				testIndex++;
				UpdateProgress(run.Id, "Context Window Tests", testIndex, totalTests, model.Identifier, test.Name);
				try
				{
					ContextWindowTestResultBundle result = await RunSingleContextWindowTestAsync(run, config, provider, model, test, testDefRepo, llmClient, configProbeCount, ct);
					if (result != null)
					{
						await resultsRepo.SaveContextWindowResultAsync(result.Result, result.Probes);
						Log(run.Id, "info", $"{model.Identifier} → {test.Name}: Max reliable {result.Result.MaxReliableTokens:N0} tokens ({result.Result.DegradationPattern})");
						modelResults.Add(new ValueTuple<int, int, double>(item2: (test.BaseTargetTokens > 0) ? test.BaseTargetTokens : configTargetTokens, item1: result.Result.MaxReliableTokens.GetValueOrDefault(), item3: result.Result.CheckpointAccuracy.GetValueOrDefault()));
					}
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex2)
				{
					Log(run.Id, "error", $"{model.Identifier} → {test.Name}: FAILED - {ex2.Message}");
				}
				await IncrementCompletedTests(run.Id, benchmarkRepo);
			}
			if (modelResults.Count > 0)
			{
				double avgReliability = modelResults.Average(((int maxReliable, int target, double accuracy) r) => (r.target > 0) ? ((double)r.maxReliable / (double)r.target) : 0.0);
				double avgAccuracy = modelResults.Average(((int maxReliable, int target, double accuracy) r) => r.accuracy);
				BenchmarkRunModel runModel = await benchmarkRepo.GetRunModelAsync(run.Id, model.Id);
				if (runModel != null)
				{
					runModel.ContextWindowAvgReliability = avgReliability;
					runModel.ContextWindowAvgAccuracy = avgAccuracy;
					runModel.ContextWindowTestCount = modelResults.Count;
					await benchmarkRepo.UpdateRunModelAsync(runModel);
				}
				Log(run.Id, "info", $"{model.Identifier} → Context Window Summary: {avgReliability:P0} reliable, {avgAccuracy:P0} accuracy ({modelResults.Count} tests)");
			}
		}
		Log(run.Id, "success", $"Context window tests complete: {testIndex} tests run");
	}

	private async Task<ContextWindowTestResultBundle?> RunSingleContextWindowTestAsync(BenchmarkRun run, TestSuiteConfig config, Provider provider, Model model, ContextWindowTest test, ITestDefinitionRepository testDefRepo, ILlmClient llmClient, int probeCount, CancellationToken ct)
	{
		int targetTokens = ((test.BaseTargetTokens > 0) ? test.BaseTargetTokens : config.ContextWindowTargetTokens);
		List<ContextWindowCheckpoint> checkpoints;
		if (test.Id > 0)
		{
			List<ContextWindowCheckpoint> dbCheckpoints = (await testDefRepo.GetContextWindowCheckpointsAsync(test.Id)).ToList();
			if (dbCheckpoints.Count > 0)
			{
				checkpoints = (from contextWindowCheckpoint in dbCheckpoints
					select new ContextWindowCheckpoint
					{
						Id = contextWindowCheckpoint.Id,
						TestId = contextWindowCheckpoint.TestId,
						TargetTokenPosition = (contextWindowCheckpoint.RelativePosition.HasValue ? ((int)(contextWindowCheckpoint.RelativePosition.Value * (double)targetTokens)) : contextWindowCheckpoint.TargetTokenPosition),
						SecretWord = contextWindowCheckpoint.SecretWord,
						CarrierSentence = (contextWindowCheckpoint.CarrierSentence ?? GenerateCarrierSentence(contextWindowCheckpoint.SecretWord)),
						SortOrder = contextWindowCheckpoint.SortOrder
					} into contextWindowCheckpoint
					orderby contextWindowCheckpoint.TargetTokenPosition
					select contextWindowCheckpoint).ToList();
			}
			else
			{
				int checkpointCount = ((test.BaseCheckpointCount > 0) ? test.BaseCheckpointCount : config.ContextWindowCheckpoints);
				checkpoints = GenerateCheckpointsByComplexity(checkpointCount, targetTokens, test.NeedleComplexity);
				Log(run.Id, "info", $"[CTX] Test '{test.Name}' has no DB checkpoints, auto-generating {checkpointCount} ({test.NeedleComplexity})");
			}
		}
		else
		{
			int checkpointCount2 = ((test.BaseCheckpointCount > 0) ? test.BaseCheckpointCount : config.ContextWindowCheckpoints);
			checkpoints = GenerateCheckpointsByComplexity(checkpointCount2, targetTokens, test.NeedleComplexity);
		}
		string fullContext = BuildContextDocument(test, checkpoints, targetTokens);
		int[] probePoints = (from i in Enumerable.Range(1, probeCount)
			select targetTokens * i / probeCount).ToArray();
		List<ContextWindowProbe> allProbes = new List<ContextWindowProbe>();
		List<FailureDetail> failureDetails = new List<FailureDetail>();
		double totalMs = 0.0;
		int lastGoodTokens = 0;
		int hallucinationCount = 0;
		int forgotCount = 0;
		int confusedCount = 0;
		int apiErrorCount = 0;
		int consecutiveFailures = 0;
		HashSet<string> checkedCheckpoints = new HashSet<string>();
		Dictionary<string, bool> checkpointResults = new Dictionary<string, bool>();
		Log(run.Id, "info", $"[CTX] {model.Identifier}: Target={targetTokens:N0} tokens, Checkpoints={checkpoints.Count}, Probes={probePoints.Length}");
		foreach (ContextWindowCheckpoint cp in checkpoints)
		{
			Log(run.Id, "info", $"[CTX]   Checkpoint at {cp.TargetTokenPosition:N0} tokens: {cp.SecretWord.Substring(0, Math.Min(20, cp.SecretWord.Length))}...");
		}
		int[] array = probePoints;
		foreach (int probeTokens in array)
		{
			ct.ThrowIfCancellationRequested();
			string truncatedContext = TruncateToApproximateTokens(fullContext, probeTokens);
			List<ContextWindowCheckpoint> checkpointsToCheck = checkpoints.Where((ContextWindowCheckpoint contextWindowCheckpoint) => contextWindowCheckpoint.TargetTokenPosition <= probeTokens && !checkedCheckpoints.Contains(contextWindowCheckpoint.SecretWord)).ToList();
			bool allCorrectAtThisLevel = true;
			List<ContextWindowCheckpoint> previousCheckpoints = checkpoints.Where((ContextWindowCheckpoint contextWindowCheckpoint) => checkedCheckpoints.Contains(contextWindowCheckpoint.SecretWord) && checkpointResults.GetValueOrDefault(contextWindowCheckpoint.SecretWord, defaultValue: false)).ToList();
			Log(run.Id, "info", $"[CTX] Probe {probeTokens:N0}: NewChecks={checkpointsToCheck.Count}, ReChecks={previousCheckpoints.Count}");
			foreach (ContextWindowCheckpoint checkpoint in checkpointsToCheck)
			{
				(string response, double ms) tuple = await QuerySecretAsync(llmClient, provider, model.Identifier, truncatedContext, checkpoint.SecretWord);
				string response = tuple.response;
				double perf = tuple.ms;
				totalMs += perf;
				string expectedValue = (checkpoint.SecretWord.Contains(':') ? checkpoint.SecretWord.Split(':')[1] : checkpoint.SecretWord);
				string checkpointName = (checkpoint.SecretWord.Contains(':') ? checkpoint.SecretWord.Split(':')[0] : checkpoint.SecretWord.Substring(0, Math.Min(12, checkpoint.SecretWord.Length)));
				string responseLower = response.ToLower();
				bool containsValue = response.Contains(expectedValue, StringComparison.OrdinalIgnoreCase);
				bool isRefusal = responseLower.Contains("i cannot") || responseLower.Contains("i can't") || responseLower.Contains("unable to") || responseLower.Contains("don't know") || responseLower.Contains("not found") || responseLower.Contains("no value");
				bool found = containsValue && !isRefusal;
				bool hallucinated = false;
				checkedCheckpoints.Add(checkpoint.SecretWord);
				checkpointResults[checkpoint.SecretWord] = found;
				Log(run.Id, "info", "[CTX]   Check '" + checkpointName + "': " + (found ? ("FOUND (" + expectedValue + ")") : "MISSING"));
				if (!found)
				{
					allCorrectAtThisLevel = false;
					string failureType;
					if (response == "<error>" || response.StartsWith("Error:") || response.Contains("error"))
					{
						failureType = "API_ERROR";
						apiErrorCount++;
					}
					else if (responseLower.Contains("i cannot") || responseLower.Contains("i can't") || responseLower.Contains("i don't") || responseLower.Contains("i'm not able") || responseLower.Contains("cannot provide") || responseLower.Contains("unable to"))
					{
						failureType = "REFUSAL";
						confusedCount++;
					}
					else if (responseLower.Contains("no secret") || responseLower.Contains("not find") || responseLower.Contains("don't see") || responseLower.Contains("no code") || responseLower.Contains("not present") || responseLower.Contains("doesn't contain"))
					{
						failureType = "NOT_FOUND";
						forgotCount++;
					}
					else if (response.Length > 10 && !responseLower.Contains("don't") && !responseLower.Contains("can't") && !responseLower.Contains("not sure"))
					{
						hallucinated = true;
						failureType = "HALLUCINATION";
						hallucinationCount++;
					}
					else if (responseLower.Contains("remember") || responseLower.Contains("sure") || responseLower.Contains("think"))
					{
						failureType = "UNCERTAIN";
						confusedCount++;
					}
					else
					{
						failureType = "UNKNOWN";
						forgotCount++;
					}
					failureDetails.Add(new FailureDetail(probeTokens, checkpoint.SecretWord, response, failureType));
				}
				allProbes.Add(new ContextWindowProbe
				{
					CheckpointId = ((checkpoint.Id > 0) ? new int?(checkpoint.Id) : ((int?)null)),
					TokenPosition = probeTokens,
					Found = found,
					Hallucinated = hallucinated,
					Response = ((response.Length > 500) ? (response.Substring(0, 500) + "...") : response),
					TotalMs = perf
				});
			}
			foreach (ContextWindowCheckpoint checkpoint2 in previousCheckpoints)
			{
				(string response, double ms) tuple2 = await QuerySecretAsync(llmClient, provider, model.Identifier, truncatedContext, checkpoint2.SecretWord);
				string response2 = tuple2.response;
				double perf2 = tuple2.ms;
				totalMs += perf2;
				string expectedValue2 = (checkpoint2.SecretWord.Contains(':') ? checkpoint2.SecretWord.Split(':')[1] : checkpoint2.SecretWord);
				if (!response2.Contains(expectedValue2, StringComparison.OrdinalIgnoreCase))
				{
					allCorrectAtThisLevel = false;
					checkpointResults[checkpoint2.SecretWord] = false;
					string responseLower2 = response2.ToLower();
					string failureType2 = ((responseLower2.Contains("don't know") || responseLower2.Contains("not find")) ? "DEGRADED" : "LOST");
					forgotCount++;
					failureDetails.Add(new FailureDetail(probeTokens, checkpoint2.SecretWord, response2, failureType2));
					allProbes.Add(new ContextWindowProbe
					{
						CheckpointId = ((checkpoint2.Id > 0) ? new int?(checkpoint2.Id) : ((int?)null)),
						TokenPosition = probeTokens,
						Found = false,
						Hallucinated = false,
						Response = ((response2.Length > 500) ? (response2.Substring(0, 500) + "...") : response2),
						TotalMs = perf2
					});
				}
			}
			if (failureDetails.Count == 0 && allCorrectAtThisLevel)
			{
				lastGoodTokens = probeTokens;
				consecutiveFailures = 0;
				Log(run.Id, "info", $"[CTX] Probe {probeTokens:N0}: CLEAN → lastGoodTokens={lastGoodTokens:N0}");
				continue;
			}
			consecutiveFailures++;
			Log(run.Id, "info", $"[CTX] Probe {probeTokens:N0}: {(allCorrectAtThisLevel ? "Prior failures exist" : "FAILED")} → lastGoodTokens stays at {lastGoodTokens:N0} (consecutive failures: {consecutiveFailures})");
			if (consecutiveFailures >= 3)
			{
				Log(run.Id, "info", $"[CTX] Early termination: {consecutiveFailures} consecutive failures, stopping test");
				break;
			}
		}
		int totalCheckpoints = checkpoints.Count;
		int foundCheckpoints = checkpointResults.Count<KeyValuePair<string, bool>>((KeyValuePair<string, bool> kv) => kv.Value);
		double accuracy = ((totalCheckpoints > 0) ? ((double)foundCheckpoints / (double)totalCheckpoints) : 0.0);
		double targetRatio = ((targetTokens > 0) ? ((double)lastGoodTokens / (double)targetTokens) : 0.0);
		string pattern = ((targetRatio >= 0.9) ? "graceful" : ((targetRatio >= 0.6) ? "moderate" : ((targetRatio >= 0.3) ? "sudden" : "catastrophic")));
		Log(run.Id, "info", $"[CTX] FINAL: lastGoodTokens={lastGoodTokens:N0}, targetTokens={targetTokens:N0}, ratio={targetRatio:P0}");
		Log(run.Id, "info", $"[CTX] FINAL: checkpoints={totalCheckpoints}, found={foundCheckpoints}, accuracy={accuracy:P0}");
		Log(run.Id, "info", $"[CTX] FINAL: failureDetails.Count={failureDetails.Count}, pattern={pattern}");
		if (failureDetails.Count > 0)
		{
			List<string> failureTypes = (from f in failureDetails
				group f by f.FailureType into g
				select $"{g.Key}:{g.Count()}").ToList();
			Log(run.Id, "warning", $"[CTX] {model.Identifier} failed at {lastGoodTokens:N0}/{targetTokens:N0} tokens ({targetRatio:P0}). Failures: {string.Join(", ", failureTypes)}");
		}
		else if (accuracy >= 1.0)
		{
			Log(run.Id, "success", $"[CTX] {model.Identifier} passed all checkpoints up to {lastGoodTokens:N0} tokens ({targetRatio:P0} of target)");
		}
		string autopsy = GenerateAutopsy(model.Identifier, test.Name, lastGoodTokens, targetTokens, accuracy, hallucinationCount, forgotCount, confusedCount, pattern, test.BuriedInstruction != null, failureDetails);
		ContextWindowTestResult result = new ContextWindowTestResult
		{
			RunId = run.Id,
			ModelId = model.Id,
			TestId = ((test.Id > 0) ? test.Id : 0),
			MaxReliableTokens = lastGoodTokens,
			CheckpointAccuracy = accuracy,
			DegradationPattern = pattern,
			AutopsyText = autopsy,
			TotalMs = totalMs,
			CreatedAt = DateTime.UtcNow
		};
		return new ContextWindowTestResultBundle(result, allProbes);
	}

	private List<ContextWindowCheckpoint> GenerateCheckpointsByComplexity(int count, int maxTokens, string complexity)
	{
		if (1 == 0)
		{
		}
		List<ContextWindowCheckpoint> result = complexity switch
		{
			"chained" => GenerateChainedCheckpoints(count, maxTokens), 
			"composite" => GenerateCompositeCheckpoints(count, maxTokens), 
			"contradictory" => GenerateContradictoryCheckpoints(count, maxTokens), 
			_ => GenerateStealthCheckpoints(count, maxTokens), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private List<ContextWindowCheckpoint> GenerateStealthCheckpoints(int count, int maxTokens)
	{
		List<ContextWindowCheckpoint> list = new List<ContextWindowCheckpoint>();
		string[] array = new string[10] { "PHOENIX", "ATLAS", "NOVA", "VEGA", "ORION", "TITAN", "APEX", "NEXUS", "PRIME", "OMEGA" };
		string[] array2 = new string[5] { "The access code {0} is {1}.", "System variable {0} = {1}.", "The value of {0} is {1}.", "Configuration key {0} has value {1}.", "Secret identifier {0} equals {1}." };
		for (int i = 0; i < count; i++)
		{
			string text = array[i % array.Length] + ((i / array.Length > 0) ? $"_{i / array.Length + 1}" : "");
			string text2 = Random.Shared.Next(1000, 9999).ToString();
			string secretWord = text + ":" + text2;
			int targetTokenPosition = (i + 1) * (maxTokens / (count + 2));
			string carrierSentence = string.Format(array2[i % array2.Length], text, text2);
			list.Add(new ContextWindowCheckpoint
			{
				TargetTokenPosition = targetTokenPosition,
				SecretWord = secretWord,
				CarrierSentence = carrierSentence,
				SortOrder = i
			});
		}
		return list;
	}

	private List<ContextWindowCheckpoint> GenerateChainedCheckpoints(int count, int maxTokens)
	{
		List<ContextWindowCheckpoint> list = new List<ContextWindowCheckpoint>();
		int num = Math.Max(1, count / 3);
		for (int i = 0; i < num; i++)
		{
			string text = $"AGENT_{Random.Shared.Next(100, 999)}";
			string text2 = $"CODE_{Random.Shared.Next(1000, 9999)}";
			string text3 = $"KEY_{Random.Shared.Next(1000, 9999)}";
			string text4 = "FINAL_" + Guid.NewGuid().ToString("N").Substring(0, 6)
				.ToUpper();
			int targetTokenPosition = (i * 3 + 1) * (maxTokens / (num * 3 + 2));
			int targetTokenPosition2 = (i * 3 + 2) * (maxTokens / (num * 3 + 2));
			int targetTokenPosition3 = (i * 3 + 3) * (maxTokens / (num * 3 + 2));
			list.Add(new ContextWindowCheckpoint
			{
				TargetTokenPosition = targetTokenPosition,
				SecretWord = text + "→" + text2,
				CarrierSentence = $"IMPORTANT: The access code for {text} is {text2}. Remember this mapping.",
				SortOrder = i * 3
			});
			list.Add(new ContextWindowCheckpoint
			{
				TargetTokenPosition = targetTokenPosition2,
				SecretWord = text2 + "→" + text3,
				CarrierSentence = $"SECURITY NOTE: Code {text2} unlocks key {text3}. This is a two-step authentication.",
				SortOrder = i * 3 + 1
			});
			list.Add(new ContextWindowCheckpoint
			{
				TargetTokenPosition = targetTokenPosition3,
				SecretWord = text3 + "→" + text4,
				CarrierSentence = $"CLASSIFIED: Key {text3} reveals the final secret: {text4}. Guard this information.",
				SortOrder = i * 3 + 2
			});
		}
		return list;
	}

	private List<ContextWindowCheckpoint> GenerateCompositeCheckpoints(int count, int maxTokens)
	{
		List<ContextWindowCheckpoint> list = new List<ContextWindowCheckpoint>();
		int num = Math.Max(1, count / 2);
		string[] array = new string[8] { "RED", "BLUE", "GREEN", "GOLD", "SILVER", "BLACK", "WHITE", "PURPLE" };
		string[] array2 = new string[8] { "FALCON", "EAGLE", "HAWK", "RAVEN", "PHOENIX", "DRAGON", "WOLF", "BEAR" };
		string[] array3 = new string[8] { "BUDAPEST", "VIENNA", "PRAGUE", "BERLIN", "PARIS", "ROME", "MADRID", "LISBON" };
		for (int i = 0; i < num; i++)
		{
			string text = array[i % array.Length];
			string text2 = array2[i % array2.Length];
			string text3 = array3[i % array3.Length];
			int targetTokenPosition = (i * 2 + 1) * (maxTokens / (num * 2 + 2));
			int targetTokenPosition2 = (i * 2 + 2) * (maxTokens / (num * 2 + 2));
			list.Add(new ContextWindowCheckpoint
			{
				TargetTokenPosition = targetTokenPosition,
				SecretWord = "AGENT_" + text + "=" + text2,
				CarrierSentence = $"PERSONNEL FILE: Agent {text}'s operational codename is {text2}. This codename is used in all field communications.",
				SortOrder = i * 2
			});
			list.Add(new ContextWindowCheckpoint
			{
				TargetTokenPosition = targetTokenPosition2,
				SecretWord = text2 + "→" + text3,
				CarrierSentence = $"MISSION BRIEF: Operative {text2} is assigned to {text3}. Target location confirmed.",
				SortOrder = i * 2 + 1
			});
		}
		return list;
	}

	private List<ContextWindowCheckpoint> GenerateContradictoryCheckpoints(int count, int maxTokens)
	{
		List<ContextWindowCheckpoint> list = new List<ContextWindowCheckpoint>();
		int num = Math.Max(1, count / 2);
		for (int i = 0; i < num; i++)
		{
			string text = $"VAULT_{Random.Shared.Next(100, 999)}";
			string text2 = $"{Random.Shared.Next(1000, 9999)}";
			string text3 = $"{Random.Shared.Next(1000, 9999)}";
			int targetTokenPosition = (int)((double)maxTokens * (0.1 + (double)i * 0.2 / (double)num));
			int targetTokenPosition2 = (int)((double)maxTokens * (0.7 + (double)i * 0.2 / (double)num));
			list.Add(new ContextWindowCheckpoint
			{
				TargetTokenPosition = targetTokenPosition,
				SecretWord = text + ":EARLY=" + text2,
				CarrierSentence = $"INITIAL REPORT: The combination for {text} is {text2}. This was set during installation.",
				SortOrder = i * 2
			});
			list.Add(new ContextWindowCheckpoint
			{
				TargetTokenPosition = targetTokenPosition2,
				SecretWord = text + ":LATE=" + text3,
				CarrierSentence = $"UPDATE: The combination for {text} has been changed to {text3}. Previous codes are now invalid.",
				SortOrder = i * 2 + 1
			});
		}
		return list;
	}

	private string GenerateCarrierSentence(string secretWord)
	{
		string[] array = new string[3]
		{
			"Important: The secret code is " + secretWord + ".",
			"Note: Access token " + secretWord + " is required.",
			"Remember this key identifier: " + secretWord + "."
		};
		return array[Random.Shared.Next(array.Length)];
	}

	private string BuildContextDocumentSimple(List<ContextWindowCheckpoint> checkpoints, int targetTokens)
	{
		StringBuilder stringBuilder = new StringBuilder();
		int i = 0;
		int j = 0;
		string fillerContent;
		for (; i < targetTokens; i += EstimateTokens(fillerContent))
		{
			for (; j < checkpoints.Count && i >= checkpoints[j].TargetTokenPosition - 500; j++)
			{
				stringBuilder.AppendLine(checkpoints[j].CarrierSentence);
				stringBuilder.AppendLine();
				i += 50;
			}
			fillerContent = GetFillerContent("mixed");
			stringBuilder.AppendLine(fillerContent);
			stringBuilder.AppendLine();
		}
		return stringBuilder.ToString();
	}

	private string BuildContextDocument(ContextWindowTest test, List<ContextWindowCheckpoint> checkpoints, int targetTokens)
	{
		StringBuilder stringBuilder = new StringBuilder();
		int i = 0;
		int j = 0;
		if (test.BuriedInstruction != null)
		{
			int num = 0;
			while (num < 3)
			{
				stringBuilder.AppendLine(GetFillerContent(test.FillerType));
				stringBuilder.AppendLine();
				num++;
				i += 150;
			}
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder2);
			handler.AppendLiteral("CRITICAL DIRECTIVE: ");
			handler.AppendFormatted(test.BuriedInstruction);
			stringBuilder2.AppendLine(ref handler);
			stringBuilder.AppendLine();
			i += 50;
		}
		string fillerContent;
		for (; i < targetTokens; i += EstimateTokens(fillerContent))
		{
			for (; j < checkpoints.Count && i >= checkpoints[j].TargetTokenPosition - 500; j++)
			{
				stringBuilder.AppendLine(checkpoints[j].CarrierSentence);
				stringBuilder.AppendLine();
				i += 50;
			}
			fillerContent = GetFillerContent(test.FillerType);
			stringBuilder.AppendLine(fillerContent);
			stringBuilder.AppendLine();
		}
		return stringBuilder.ToString();
	}

	private string GetFillerContent(string type)
	{
		string[] array = new string[3] { "public class DataProcessor { private readonly ILogger _logger; public async Task<Result> ProcessAsync(Data input) { try { var validated = await ValidateAsync(input); return await TransformAsync(validated); } catch (Exception ex) { _logger.LogError(ex, \"Processing failed\"); throw; } } }", "function calculateMetrics(data) { const sum = data.reduce((a, b) => a + b, 0); const avg = sum / data.length; const variance = data.map(x => Math.pow(x - avg, 2)).reduce((a, b) => a + b) / data.length; return { sum, avg, variance, stdDev: Math.sqrt(variance) }; }", "SELECT u.name, COUNT(o.id) as order_count, SUM(o.total) as revenue FROM users u LEFT JOIN orders o ON u.id = o.user_id WHERE o.created_at >= DATE_SUB(NOW(), INTERVAL 30 DAY) GROUP BY u.id HAVING order_count > 5;" };
		string[] array2 = new string[3] { "The morning sun cast long shadows across the empty street. A solitary figure emerged from the corner cafe, coffee in hand, lost in thought. The city was just beginning to wake, the distant hum of traffic growing steadily louder.", "Technology advances at a relentless pace, each innovation building upon the last. What seemed impossible yesterday becomes commonplace tomorrow. Yet with each leap forward, we must pause to consider the implications.", "In the depths of winter, when frost painted patterns on every window, the old house stood silent. Its inhabitants had long since departed, leaving only memories etched into the walls." };
		string[] array3 = new string[3] { "The TCP three-way handshake establishes a connection through SYN, SYN-ACK, and ACK packets. This process ensures both parties agree on initial sequence numbers and are ready to exchange data.", "In distributed systems, the CAP theorem states that a system can provide at most two of three guarantees: Consistency, Availability, and Partition tolerance.", "Machine learning models require careful feature engineering and preprocessing. Normalization scales features to similar ranges, preventing dominance by large values." };
		string text = type.ToLower();
		if (1 == 0)
		{
		}
		string result;
		switch (text)
		{
		case "code":
			result = array[Random.Shared.Next(array.Length)];
			break;
		case "prose":
			result = array2[Random.Shared.Next(array2.Length)];
			break;
		case "technical":
			result = array3[Random.Shared.Next(array3.Length)];
			break;
		default:
		{
			int num = Random.Shared.Next(3);
			if (1 == 0)
			{
			}
			string text2 = num switch
			{
				0 => array[Random.Shared.Next(array.Length)], 
				1 => array2[Random.Shared.Next(array2.Length)], 
				_ => array3[Random.Shared.Next(array3.Length)], 
			};
			if (1 == 0)
			{
			}
			result = text2;
			break;
		}
		}
		if (1 == 0)
		{
		}
		return result;
	}

	private int EstimateTokens(string text)
	{
		return text.Length / 4;
	}

	private string TruncateToApproximateTokens(string text, int maxTokens)
	{
		int num = maxTokens * 4;
		if (text.Length <= num)
		{
			return text;
		}
		return text.Substring(0, Math.Min(text.Length, num));
	}

	private bool CheckBuriedInstructionFollowed(string instruction, string? response)
	{
		if (string.IsNullOrEmpty(response))
		{
			return false;
		}
		string responseLower = response.ToLower();
		string text = instruction.ToLower();
		if (text.Contains("pirate"))
		{
			string[] source = new string[11]
			{
				"arr", "matey", "ye ", "ahoy", "treasure", "ship", "sail", "captain", "scallywag", "landlubber",
				"aye"
			};
			int num = source.Count((string ind) => responseLower.Contains(ind));
			return num >= 2;
		}
		if (text.Contains("haiku"))
		{
			string[] array = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
			if (array.Length >= 2 && array.Length <= 5)
			{
				int num2 = array.Count((string l) => l.Trim().Length < 50);
				return num2 >= 2;
			}
			return false;
		}
		if (text.Contains("dr.") || text.Contains("physicist") || text.Contains("2147"))
		{
			string[] source2 = new string[10] { "quantum", "physics", "future", "2147", "elara", "vance", "dr.", "technology", "temporal", "dimensions" };
			int num3 = source2.Count((string ind) => responseLower.Contains(ind));
			return num3 >= 1;
		}
		List<string> list = (from w in instruction.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			where w.Length > 4
			select w.ToLower().Trim(new char[5] { '.', ',', '!', '"', '\'' })).Where(delegate(string w)
		{
			object obj = global::_003CPrivateImplementationDetails_003E.B3E528A98F9A3D35B670F6797D07FFA7177E82337BE1F4609A01122FD09C55FB_B11;
			if (obj == null)
			{
				obj = new string[12]
				{
					"from", "this", "point", "forward", "always", "respond", "must", "your", "that", "with",
					"only", "never"
				};
				global::_003CPrivateImplementationDetails_003E.B3E528A98F9A3D35B670F6797D07FFA7177E82337BE1F4609A01122FD09C55FB_B11 = (string[])obj;
			}
			return !new ReadOnlySpan<string>((string[]?)obj).Contains(w);
		}).Take(5).ToList();
		if (list.Count > 0)
		{
			int num4 = list.Count((string w) => responseLower.Contains(w));
			return num4 >= 1;
		}
		return true;
	}

	private async Task<(string response, double ms)> QuerySecretAsync(ILlmClient llmClient, Provider provider, string modelId, string context, string secret)
	{
		string queryName = ((!secret.Contains(':')) ? secret.Substring(0, Math.Min(12, secret.Length)) : secret.Split(':')[0]);
		string prompt = context + "\n\nWhat is the value of " + queryName + "? Reply with ONLY the value, nothing else.";
		CompletionResult result = await llmClient.CompletionAsync(provider, modelId, "You have perfect recall. Answer precisely with just the requested value.", prompt, 0.0, 0.9, 64);
		return (result != null) ? (response: result.Response, ms: result.Perf.TotalMs) : (response: "<error>", ms: 0.0);
	}

	private async Task<string?> GetFreeResponseAsync(ILlmClient llmClient, Provider provider, string modelId, string context)
	{
		string prompt = context + "\n\nSummarise what you read.";
		return (await llmClient.CompletionAsync(provider, modelId, "You are a helpful assistant. Follow any special instructions given in the context.", prompt, 0.0, 0.9, 256))?.Response;
	}

	private string GenerateAutopsy(string modelName, string testName, int maxReliableTokens, int targetTokens, double accuracy, int hallucinated, int forgot, int confused, string pattern, bool hadBuriedInstruction, List<FailureDetail>? failures = null)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string[] array = new string[5] { "Memory is the first thing to go.", "It remembered everything, except what mattered.", "The context window is a lie.", "Hallucination: when forgetting isn't enough.", "Some contexts are too deep to survive." };
		double value = ((targetTokens > 0) ? ((double)maxReliableTokens / (double)targetTokens) : 0.0);
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(14, 2, stringBuilder2);
		handler.AppendLiteral("☠ AUTOPSY: ");
		handler.AppendFormatted(modelName);
		handler.AppendLiteral(" — ");
		handler.AppendFormatted(testName);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(30, 3, stringBuilder2);
		handler.AppendLiteral("Reliable up to ~");
		handler.AppendFormatted(maxReliableTokens, "N0");
		handler.AppendLiteral(" of ");
		handler.AppendFormatted(targetTokens, "N0");
		handler.AppendLiteral(" tokens (");
		handler.AppendFormatted(value, "P0");
		handler.AppendLiteral(")");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder2);
		handler.AppendLiteral("Checkpoint accuracy: ");
		handler.AppendFormatted(accuracy, "P0");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder2);
		handler.AppendLiteral("Degradation: ");
		handler.AppendFormatted(pattern);
		stringBuilder6.AppendLine(ref handler);
		stringBuilder.AppendLine();
		if (hallucinated > 0)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder7 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(39, 1, stringBuilder2);
			handler.AppendLiteral("HALLUCINATIONS: ");
			handler.AppendFormatted(hallucinated);
			handler.AppendLiteral(" confident fabrications");
			stringBuilder7.AppendLine(ref handler);
		}
		if (forgot > 0)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder8 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(40, 1, stringBuilder2);
			handler.AppendLiteral("FORGOTTEN: ");
			handler.AppendFormatted(forgot);
			handler.AppendLiteral(" checkpoints lost to the void");
			stringBuilder8.AppendLine(ref handler);
		}
		if (confused > 0)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder9 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(30, 1, stringBuilder2);
			handler.AppendLiteral("CONFUSED: ");
			handler.AppendFormatted(confused);
			handler.AppendLiteral(" uncertain responses");
			stringBuilder9.AppendLine(ref handler);
		}
		if (failures != null && failures.Count > 0)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("─── FAILURE DETAILS ───");
			foreach (FailureDetail item in failures.Take(5))
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder10 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(25, 2, stringBuilder2);
				handler.AppendLiteral("@ ");
				handler.AppendFormatted(item.TokenPosition, "N0");
				handler.AppendLiteral(" tokens | Looking for: ");
				handler.AppendFormatted(item.SecretWord);
				stringBuilder10.AppendLine(ref handler);
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder11 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
				handler.AppendLiteral("  Type: ");
				handler.AppendFormatted(item.FailureType);
				stringBuilder11.AppendLine(ref handler);
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder12 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder2);
				handler.AppendLiteral("  Response: \"");
				handler.AppendFormatted(TruncateForAutopsy(item.Response, 150));
				handler.AppendLiteral("\"");
				stringBuilder12.AppendLine(ref handler);
				stringBuilder.AppendLine();
			}
			if (failures.Count > 5)
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder13 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(22, 1, stringBuilder2);
				handler.AppendLiteral("... and ");
				handler.AppendFormatted(failures.Count - 5);
				handler.AppendLiteral(" more failures");
				stringBuilder13.AppendLine(ref handler);
			}
		}
		if (hadBuriedInstruction && pattern == "catastrophic")
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("☠ Forgot its secret identity. Total collapse.");
		}
		stringBuilder.AppendLine();
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder14 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder2);
		handler.AppendLiteral("\"");
		handler.AppendFormatted(array[Random.Shared.Next(array.Length)]);
		handler.AppendLiteral("\"");
		stringBuilder14.AppendLine(ref handler);
		return stringBuilder.ToString();
	}

	private static string TruncateForAutopsy(string text, int maxLen)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "<empty>";
		}
		string text2 = text.Replace("\n", " ").Replace("\r", "").Trim();
		if (text2.Length <= maxLen)
		{
			return text2;
		}
		return text2.Substring(0, maxLen) + "...";
	}

	private async Task RunMcpToolTestsAsync(BenchmarkRun run, TestSuiteConfig config, Provider provider, List<Model> models, CancellationToken ct)
	{
		using IServiceScope scope = _scopeFactory.CreateScope();
		ITestDefinitionRepository testDefRepo = scope.ServiceProvider.GetRequiredService<ITestDefinitionRepository>();
		IResultsRepository resultsRepo = scope.ServiceProvider.GetRequiredService<IResultsRepository>();
		IBenchmarkRepository benchmarkRepo = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
		ILlmClient llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();
		IEchoMcpClient mcpClient = scope.ServiceProvider.GetRequiredService<IEchoMcpClient>();
		mcpClient.Configure(config.EchoMcpBaseUrl, config.EchoMcpToken);
		mcpClient.ConfigureTransport(config.McpTransportType ?? "sse", config.McpServerUrl ?? config.EchoMcpBaseUrl, config.McpServerCommand, config.McpServerArgs);
		McpToolTestService mcpTestService = new McpToolTestService();
		if (!(await mcpClient.CheckHealthAsync()))
		{
			Log(run.Id, "warning", "EchoMCP not available, skipping MCP tool tests");
			return;
		}
		Log(run.Id, "info", "EchoMCP connected successfully");
		List<McpToolTest> allTests = (await testDefRepo.GetMcpToolTestsAsync()).ToList();
		List<McpToolTest> tests = ((config.MaxMcpToolTests <= 0 || allTests.Count <= config.MaxMcpToolTests) ? allTests : allTests.OrderBy((McpToolTest _) => Random.Shared.Next()).Take(config.MaxMcpToolTests).ToList());
		if (!tests.Any())
		{
			Log(run.Id, "warning", "No MCP tool tests available");
			return;
		}
		Log(run.Id, "info", $"Running {tests.Count} MCP tool tests on {models.Count} models");
		Dictionary<string, string> dynamicSchemas = new Dictionary<string, string>();
		if (config.FetchSchemasFromEchoMcp)
		{
			foreach (string toolName in tests.Select((McpToolTest t) => t.ToolName).Distinct())
			{
				string schema = await mcpClient.GetToolSchemaAsync(toolName);
				if (!string.IsNullOrEmpty(schema))
				{
					dynamicSchemas[toolName] = schema;
					Console.WriteLine("[MCP] Fetched schema for " + toolName);
				}
			}
		}
		int totalTests = tests.Count * models.Count;
		int completed = 0;
		foreach (Model model in models)
		{
			ct.ThrowIfCancellationRequested();
			if (llmClient.IsModelFlagged(model.Identifier))
			{
				Log(run.Id, "warning", "Skipping flagged model: " + model.Identifier);
				completed += tests.Count;
				UpdateProgress(run.Id, "MCP Tool Tests", completed, totalTests, model.Identifier, "Skipped (flagged)");
				continue;
			}
			await llmClient.WarmUpModelAsync(provider, model.Identifier);
			foreach (McpToolTest test in tests)
			{
				ct.ThrowIfCancellationRequested();
				McpToolTestResult result = new McpToolTestResult
				{
					RunId = run.Id,
					ModelId = model.Id,
					TestId = test.Id,
					CreatedAt = DateTime.UtcNow
				};
				try
				{
					string dynSchema;
					string schema2 = ((!dynamicSchemas.TryGetValue(test.ToolName, out dynSchema)) ? test.ToolSchema : dynSchema);
					CompletionResult completion = await llmClient.CompletionAsync(userPrompt: mcpTestService.BuildPrompt(test, schema2), systemPrompt: mcpTestService.GetSystemPrompt(), provider: provider, modelIdentifier: model.Identifier, temperature: 0.1, topP: 0.9, maxTokens: 512);
					if (completion == null)
					{
						result.JsonValid = false;
						result.JsonParseError = "No response from model";
						result.Passed = false;
					}
					else
					{
						result.ModelResponse = completion.Response;
						result.TotalMs = completion.Perf.TotalMs;
						result.TokensPerSec = completion.Perf.TokensPerSec;
						result.PromptTokens = completion.Perf.PromptTokens;
						result.CompletionTokens = completion.Perf.CompletionTokens;
						ToolCallParseResult parseResult = mcpTestService.ParseToolCall(completion.Response);
						result.JsonValid = parseResult.Success;
						result.ParsedToolCall = parseResult.RawJson;
						result.JsonParseError = parseResult.Error;
						if (parseResult.Success)
						{
							ToolCallValidation validation = mcpTestService.ValidateToolCall(parseResult.ToolCall, test);
							result.CorrectTool = true;
							result.CorrectCommand = validation.CorrectCommand;
							result.ParamsValid = validation.ParamsValid;
							if (test.ExecuteTool && validation.IsValid)
							{
								result.ToolExecuted = true;
								string cmd = parseResult.ToolCall.GetProperty("cmd").GetString() ?? "";
								object parameters = null;
								if (parseResult.ToolCall.TryGetProperty("params", out var paramsEl))
								{
									parameters = JsonSerializer.Deserialize<object>(paramsEl.GetRawText());
								}
								McpExecutionResult execResult = await mcpClient.ExecuteToolAsync(test.ToolName, cmd, parameters);
								result.ExecutionSuccess = execResult.Success;
								result.ToolResponse = execResult.Response;
								result.ExecutionError = execResult.Error;
								result.ExecutionMs = execResult.ExecutionMs;
								if (execResult.Success && execResult.Response != null)
								{
									ResponseValidation respValidation = mcpTestService.ValidateToolResponse(execResult.Response, test);
									result.ResponseValidated = respValidation.Valid;
									result.ValidationReason = respValidation.Reason;
								}
								else
								{
									result.ResponseValidated = false;
									result.ValidationReason = execResult.Error ?? "Execution failed";
								}
								result.Passed = result.CorrectCommand && result.ParamsValid && result.ExecutionSuccess == true && result.ResponseValidated == true;
								paramsEl = default(JsonElement);
							}
							else if (!test.ExecuteTool)
							{
								result.ToolExecuted = false;
								result.Passed = result.CorrectCommand && result.ParamsValid;
							}
							else
							{
								result.ToolExecuted = false;
								result.Passed = false;
								result.ValidationReason = string.Join("; ", validation.Errors);
							}
						}
						else
						{
							result.CorrectTool = false;
							result.CorrectCommand = false;
							result.ParamsValid = false;
							result.Passed = false;
						}
					}
					dynSchema = null;
				}
				catch (Exception ex)
				{
					result.JsonValid = false;
					result.JsonParseError = ex.Message;
					result.Passed = false;
					Log(run.Id, "error", "MCP test error for " + model.Identifier + ": " + ex.Message);
				}
				await resultsRepo.SaveMcpToolResultAsync(result);
				await IncrementCompletedTests(run.Id, benchmarkRepo);
				completed++;
				UpdateProgress(run.Id, "MCP Tool Tests", completed, totalTests, model.Identifier, $"{test.ToolName}/{test.Command}: {(result.Passed ? "Pass" : "Fail")}");
			}
		}
		Log(run.Id, "success", "MCP tool tests complete");
	}

	private async Task RunGenerationTestsAsync(BenchmarkRun run, TestSuiteConfig config, Provider provider, List<Model> models, CancellationToken ct)
	{
		using IServiceScope scope = _scopeFactory.CreateScope();
		ISeedRepository seedRepo = scope.ServiceProvider.GetRequiredService<ISeedRepository>();
		IConfigRepository configRepo = scope.ServiceProvider.GetRequiredService<IConfigRepository>();
		IResultsRepository resultsRepo = scope.ServiceProvider.GetRequiredService<IResultsRepository>();
		IBenchmarkRepository benchmarkRepo = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
		ILlmClient llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();
		List<Seed> allSeeds = (await seedRepo.GetAllAsync()).ToList();
		List<CategorySetting> categorySettings = (await configRepo.GetCategorySettingsAsync(config.Id)).ToList();
		if (!allSeeds.Any())
		{
			Log(run.Id, "warning", "No seeds available for generation tests");
			return;
		}
		List<Seed> augmentedSeeds = allSeeds.Where((Seed s) => s.IsAugmented).ToList();
		List<Seed> baseSeeds = allSeeds.Where((Seed s) => !s.IsAugmented).ToList();
		List<Seed> seeds;
		if (augmentedSeeds.Any())
		{
			seeds = augmentedSeeds.OrderBy((Seed _) => Random.Shared.Next()).Take(config.TargetSeedCount).ToList();
			Log(run.Id, "info", $"Randomly selected {seeds.Count} augmented seeds (of {augmentedSeeds.Count} available)");
		}
		else
		{
			seeds = baseSeeds.OrderBy((Seed _) => Random.Shared.Next()).Take(config.TargetSeedCount).ToList();
			Log(run.Id, "info", $"Randomly selected {seeds.Count} base seeds (of {baseSeeds.Count} available)");
		}
		Log(run.Id, "info", $"Running generation tests: {seeds.Count} seeds × {models.Count} models");
		int totalTests = seeds.Count * models.Count;
		int completed = 0;
		foreach (Model model in models)
		{
			ct.ThrowIfCancellationRequested();
			await WarmupModelAsync(run.Id, llmClient, provider, model.Identifier);
			foreach (Seed seed in seeds)
			{
				ct.ThrowIfCancellationRequested();
				CategorySetting categorySetting = categorySettings.FirstOrDefault((CategorySetting c) => c.Category == seed.Category);
				string systemPrompt = categorySetting?.SystemPrompt ?? "You are a helpful assistant.";
				double temperature = seed.Temperature ?? categorySetting?.Temperature ?? config.GlobalTemperature;
				int maxTokens = seed.MaxTokens ?? categorySetting?.MaxTokens ?? config.GlobalMaxTokens;
				CompletionResult result = await llmClient.CompletionAsync(provider, model.Identifier, systemPrompt, seed.Instruction, temperature, config.GlobalTopP, maxTokens);
				if (result != null)
				{
					await resultsRepo.SaveGenerationResultAsync(new GenerationResult
					{
						RunId = run.Id,
						ModelId = model.Id,
						SeedId = seed.Id,
						Category = seed.Category,
						Response = result.Response,
						Temperature = temperature,
						TopP = config.GlobalTopP,
						MaxTokens = maxTokens,
						TotalMs = result.Perf.TotalMs,
						TokensPerSec = result.Perf.TokensPerSec,
						PromptTokens = result.Perf.PromptTokens,
						CompletionTokens = result.Perf.CompletionTokens,
						CreatedAt = DateTime.UtcNow
					});
				}
				await IncrementCompletedTests(run.Id, benchmarkRepo);
				completed++;
				string seedPreview = ((seed.Instruction.Length > 50) ? (seed.Instruction.Substring(0, 50) + "...") : seed.Instruction);
				UpdateProgress(run.Id, "Generation Tests", completed, totalTests, model.Identifier, seed.Category + ": " + seedPreview);
			}
		}
	}

	private async Task RunJudgingAsync(BenchmarkRun run, TestSuiteConfig config, Provider provider, Model judgeModel, CancellationToken ct)
	{
		using IServiceScope scope = _scopeFactory.CreateScope();
		IResultsRepository resultsRepo = scope.ServiceProvider.GetRequiredService<IResultsRepository>();
		ISeedRepository seedRepo = scope.ServiceProvider.GetRequiredService<ISeedRepository>();
		ILlmClient llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();
		List<GenerationResult> results = (await resultsRepo.GetGenerationResultsAsync(run.Id)).ToList();
		Dictionary<int, Seed> seeds = (await seedRepo.GetAllAsync()).ToDictionary((Seed seed2) => seed2.Id);
		if (!results.Any())
		{
			Log(run.Id, "info", "No generation results to judge");
			return;
		}
		Log(run.Id, "info", $"Judging {results.Count} generation results with {judgeModel.Identifier}");
		int completed = 0;
		foreach (GenerationResult result in results)
		{
			ct.ThrowIfCancellationRequested();
			completed++;
			UpdateProgress(run.Id, "Judging", completed, results.Count, judgeModel.Identifier, $"Result #{result.Id}");
			Seed s;
			Seed seed = (seeds.TryGetValue(result.SeedId, out s) ? s : null);
			CompletionResult judgeResponse = await llmClient.CompletionAsync(userPrompt: $"Evaluate this AI response:\r\n\r\nCategory: {result.Category}\r\nPrompt: {seed?.Instruction ?? "Unknown"}\r\n\r\nResponse:\r\n{result.Response}\r\n\r\nRate this response from 1-10 considering:\r\n- Accuracy and correctness\r\n- Code quality (if applicable)\r\n- Clarity of reasoning\r\n- Helpfulness\r\n\r\nRespond in this exact JSON format:\r\n{{\r\n  \"score\": <1-10>,\r\n  \"reasoning\": \"<brief explanation>\"\r\n}}", provider: provider, modelIdentifier: judgeModel.Identifier, systemPrompt: "You are an expert evaluator of AI responses. Score responses objectively based on quality, accuracy, and usefulness.", temperature: 0.3, topP: 0.9, maxTokens: 300);
			if (judgeResponse != null)
			{
				(double Score, string Reasoning)? rating = ParseJudgeResponse(judgeResponse.Response, judgeModel.Id);
				if (rating.HasValue)
				{
					await resultsRepo.SaveRatingAsync(new GenerationRating
					{
						ResultId = result.Id,
						JudgeModelId = judgeModel.Id,
						Score = rating.Value.Score,
						Reasoning = rating.Value.Reasoning,
						IsBaseJudge = true,
						CreatedAt = DateTime.UtcNow
					});
					result.AvgScore = rating.Value.Score;
					result.IsHighQuality = rating.Value.Score >= config.HighQualityThreshold;
					await resultsRepo.UpdateGenerationResultAsync(result);
				}
			}
			s = null;
		}
		Log(run.Id, "success", $"Judging complete: {completed} results scored");
	}

	private (double Score, string Reasoning)? ParseJudgeResponse(string response, int judgeModelId)
	{
		try
		{
			string text = ExtractJson(response);
			if (text == null)
			{
				return null;
			}
			JsonDocument jsonDocument = JsonDocument.Parse(text);
			JsonElement rootElement = jsonDocument.RootElement;
			return (GetScoreFromJson(rootElement, "score"), rootElement.GetProperty("reasoning").GetString() ?? "");
		}
		catch (Exception ex)
		{
			Console.WriteLine("[Orchestrator] Failed to parse judge response: " + ex.Message);
			return null;
		}
	}

	public async Task CancelRunAsync(int runId)
	{
		if (_cancellationTokens.TryGetValue(runId, out CancellationTokenSource cts))
		{
			cts.Cancel();
		}
		using IServiceScope scope = _scopeFactory.CreateScope();
		IBenchmarkRepository benchmarkRepo = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
		await benchmarkRepo.UpdateRunStatusAsync(runId, "cancelled");
	}

	public async Task RescoreRunAsync(int runId, CancellationToken cancellationToken = default(CancellationToken))
	{
		Console.WriteLine($"[Orchestrator] Starting re-score for run {runId}");
		using IServiceScope scope = _scopeFactory.CreateScope();
		IBenchmarkRepository benchmarkRepo = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
		IResultsRepository resultsRepo = scope.ServiceProvider.GetRequiredService<IResultsRepository>();
		ITestDefinitionRepository testDefRepo = scope.ServiceProvider.GetRequiredService<ITestDefinitionRepository>();
		ISeedRepository seedRepo = scope.ServiceProvider.GetRequiredService<ISeedRepository>();
		IProviderRepository providerRepo = scope.ServiceProvider.GetRequiredService<IProviderRepository>();
		IModelRepository modelRepo = scope.ServiceProvider.GetRequiredService<IModelRepository>();
		ILlmClient llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();
		BenchmarkRun run = (await benchmarkRepo.GetRunByIdAsync(runId)) ?? throw new ArgumentException($"Run {runId} not found");
		Provider provider = (await providerRepo.GetByIdAsync(run.ProviderId)) ?? throw new ArgumentException($"Provider {run.ProviderId} not found");
		List<Model> models = (await modelRepo.GetTestableByProviderAsync(provider.Id)).ToList();
		if (!models.Any())
		{
			throw new InvalidOperationException("No testable models available for judging");
		}
		Model judgeModel = models.First();
		RunProgress progress = new RunProgress
		{
			RunId = runId,
			Stage = "Re-scoring"
		};
		_activeRuns[runId] = progress;
		try
		{
			Log(runId, "info", "Starting re-score with judge: " + judgeModel.Identifier);
			await RescoreReasoningResultsAsync(runId, provider, judgeModel, llmClient, resultsRepo, testDefRepo, cancellationToken);
			await RescoreConversationResultsAsync(runId, provider, judgeModel, llmClient, resultsRepo, cancellationToken);
			await RescoreGenerationResultsAsync(runId, provider, judgeModel, llmClient, resultsRepo, seedRepo, cancellationToken);
			Log(runId, "success", "Re-scoring complete");
		}
		finally
		{
			_activeRuns.Remove(runId);
		}
	}

	private async Task RescoreReasoningResultsAsync(int runId, Provider provider, Model judgeModel, ILlmClient llmClient, IResultsRepository resultsRepo, ITestDefinitionRepository testDefRepo, CancellationToken ct)
	{
		List<ReasoningTestResult> results = (await resultsRepo.GetReasoningResultsAsync(runId)).ToList();
		if (!results.Any())
		{
			return;
		}
		Dictionary<int, ReasoningTest> tests = (await testDefRepo.GetReasoningTestsAsync(activeOnly: false)).ToDictionary((ReasoningTest t) => t.Id);
		Log(runId, "info", $"Re-scoring {results.Count} reasoning results");
		int completed = 0;
		foreach (ReasoningTestResult result in results)
		{
			ct.ThrowIfCancellationRequested();
			completed++;
			UpdateProgress(runId, "Re-scoring Reasoning", completed, results.Count, judgeModel.Identifier);
			if (tests.TryGetValue(result.TestId, out var test))
			{
				(double Overall, double CorrectAnswer, double LogicalSteps, double Clarity, string Reasoning)? scores = await JudgeReasoningResponseAsync(llmClient, provider, judgeModel.Identifier, test.Prompt, test.CorrectAnswer, result.Response);
				if (scores.HasValue)
				{
					result.OverallScore = scores.Value.Overall;
					result.CorrectAnswerScore = scores.Value.CorrectAnswer;
					result.LogicalStepsScore = scores.Value.LogicalSteps;
					result.ClarityScore = scores.Value.Clarity;
					result.JudgeReasoning = scores.Value.Reasoning;
					result.JudgeModelId = judgeModel.Id;
					await UpdateReasoningResultAsync(resultsRepo, result);
				}
				test = null;
			}
		}
	}

	private async Task RescoreConversationResultsAsync(int runId, Provider provider, Model judgeModel, ILlmClient llmClient, IResultsRepository resultsRepo, CancellationToken ct)
	{
		List<ConversationTestResult> results = (await resultsRepo.GetConversationResultsAsync(runId)).ToList();
		if (!results.Any())
		{
			Log(runId, "info", "No conversation results to re-score");
			return;
		}
		Log(runId, "info", $"Re-scoring {results.Count} conversation results");
		int completed = 0;
		int scored = 0;
		foreach (ConversationTestResult result in results)
		{
			ct.ThrowIfCancellationRequested();
			completed++;
			UpdateProgress(runId, "Re-scoring Conversations", completed, results.Count, judgeModel.Identifier);
			List<ConversationExchange> exchanges = (await resultsRepo.GetConversationExchangesAsync(result.Id)).ToList();
			if (!exchanges.Any())
			{
				Console.WriteLine($"[Orchestrator] No exchanges found for conversation result {result.Id}");
				continue;
			}
			(double Overall, double TopicCoherence, double ConversationalTone, double ContextRetention, double Helpfulness, string Reasoning)? scores = await JudgeConversationAsync(conversation: string.Join("\n\n", exchanges.Select((ConversationExchange e) => "User: " + e.UserMessage + "\nAssistant: " + e.ModelResponse)), llmClient: llmClient, provider: provider, modelId: judgeModel.Identifier);
			if (scores.HasValue)
			{
				result.OverallScore = scores.Value.Overall;
				result.TopicCoherence = scores.Value.TopicCoherence;
				result.ConversationalTone = scores.Value.ConversationalTone;
				result.ContextRetention = scores.Value.ContextRetention;
				result.Helpfulness = scores.Value.Helpfulness;
				result.JudgeReasoning = scores.Value.Reasoning;
				result.JudgeModelId = judgeModel.Id;
				await UpdateConversationResultAsync(resultsRepo, result);
				scored++;
			}
		}
		Log(runId, "info", $"Conversation re-scoring complete: {scored}/{results.Count} scored successfully");
	}

	private async Task RescoreGenerationResultsAsync(int runId, Provider provider, Model judgeModel, ILlmClient llmClient, IResultsRepository resultsRepo, ISeedRepository seedRepo, CancellationToken ct)
	{
		List<GenerationResult> results = (await resultsRepo.GetGenerationResultsAsync(runId)).ToList();
		if (!results.Any())
		{
			return;
		}
		Dictionary<int, Seed> seeds = (await seedRepo.GetAllAsync()).ToDictionary((Seed seed2) => seed2.Id);
		Log(runId, "info", $"Re-scoring {results.Count} generation results");
		int completed = 0;
		foreach (GenerationResult result in results)
		{
			ct.ThrowIfCancellationRequested();
			completed++;
			UpdateProgress(runId, "Re-scoring Generation", completed, results.Count, judgeModel.Identifier);
			Seed s;
			Seed seed = (seeds.TryGetValue(result.SeedId, out s) ? s : null);
			CompletionResult judgeResponse = await llmClient.CompletionAsync(userPrompt: $"Evaluate this AI response:\r\n\r\nCategory: {result.Category}\r\nPrompt: {seed?.Instruction ?? "Unknown"}\r\n\r\nResponse:\r\n{result.Response}\r\n\r\nRate this response from 1-10 considering:\r\n- Accuracy and correctness\r\n- Code quality (if applicable)\r\n- Clarity of reasoning\r\n- Helpfulness\r\n\r\nRespond in this exact JSON format:\r\n{{\r\n  \"score\": <1-10>,\r\n  \"reasoning\": \"<brief explanation>\"\r\n}}", provider: provider, modelIdentifier: judgeModel.Identifier, systemPrompt: "You are an expert evaluator of AI responses. Score responses objectively based on quality, accuracy, and usefulness.", temperature: 0.3, topP: 0.9, maxTokens: 300);
			if (judgeResponse != null)
			{
				(double Score, string Reasoning)? rating = ParseJudgeResponse(judgeResponse.Response, judgeModel.Id);
				if (rating.HasValue)
				{
					await resultsRepo.SaveRatingAsync(new GenerationRating
					{
						ResultId = result.Id,
						JudgeModelId = judgeModel.Id,
						Score = rating.Value.Score,
						Reasoning = rating.Value.Reasoning,
						IsBaseJudge = false,
						CreatedAt = DateTime.UtcNow
					});
					result.AvgScore = rating.Value.Score;
					result.IsHighQuality = rating.Value.Score >= 7.5;
					await resultsRepo.UpdateGenerationResultAsync(result);
				}
			}
			s = null;
		}
	}

	private async Task UpdateReasoningResultAsync(IResultsRepository resultsRepo, ReasoningTestResult result)
	{
		using IServiceScope scope = _scopeFactory.CreateScope();
		IDbConnectionFactory connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
		using DbConnection connection = connectionFactory.CreateConnection();
		await connection.ExecuteAsync("\r\n            UPDATE ReasoningTestResults SET\r\n                OverallScore = @OverallScore,\r\n                CorrectAnswerScore = @CorrectAnswerScore,\r\n                LogicalStepsScore = @LogicalStepsScore,\r\n                ClarityScore = @ClarityScore,\r\n                JudgeReasoning = @JudgeReasoning,\r\n                JudgeModelId = @JudgeModelId\r\n            WHERE Id = @Id", result);
	}

	private async Task UpdateConversationResultAsync(IResultsRepository resultsRepo, ConversationTestResult result)
	{
		using IServiceScope scope = _scopeFactory.CreateScope();
		IDbConnectionFactory connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
		using DbConnection connection = connectionFactory.CreateConnection();
		await connection.ExecuteAsync("\r\n            UPDATE ConversationTestResults SET\r\n                OverallScore = @OverallScore,\r\n                TopicCoherence = @TopicCoherence,\r\n                ConversationalTone = @ConversationalTone,\r\n                ContextRetention = @ContextRetention,\r\n                Helpfulness = @Helpfulness,\r\n                JudgeReasoning = @JudgeReasoning,\r\n                JudgeModelId = @JudgeModelId\r\n            WHERE Id = @Id", result);
	}

	public RunProgress? GetCurrentProgress(int runId)
	{
		RunProgress value;
		return _activeRuns.TryGetValue(runId, out value) ? value : null;
	}

	private void UpdateProgress(int runId, string stage, int current, int total, string? currentModel = null, string? currentTest = null)
	{
		if (_activeRuns.TryGetValue(runId, out RunProgress value))
		{
			value.Stage = stage;
			value.CurrentTestIndex = current;
			value.TotalTests = total;
			value.CurrentModel = currentModel;
			value.CurrentTest = currentTest;
			value.PercentComplete = ((total > 0) ? ((double)current / (double)total * 100.0) : 0.0);
			this.OnProgressUpdate?.Invoke(value);
		}
	}

	private async Task LogAsync(int runId, string level, string message, string? modelName = null)
	{
		DateTime timestamp = DateTime.UtcNow;
		LogEvent logEvent = new LogEvent
		{
			RunId = runId,
			Timestamp = timestamp,
			Level = level,
			Message = message,
			ModelName = modelName
		};
		if (_activeRuns.TryGetValue(runId, out RunProgress progress))
		{
			progress.RecentEvents.Add("[" + level + "] " + message);
			if (progress.RecentEvents.Count > 20)
			{
				progress.RecentEvents.RemoveAt(0);
			}
		}
		this.OnLogEvent?.Invoke(logEvent);
		try
		{
			using IServiceScope scope = _scopeFactory.CreateScope();
			IBenchmarkRepository benchmarkRepo = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
			await benchmarkRepo.AddRunLogAsync(new RunLog
			{
				RunId = runId,
				Level = level,
				Message = message,
				ModelName = modelName,
				Timestamp = timestamp
			});
		}
		catch (Exception ex)
		{
			Console.WriteLine("[Orchestrator] Failed to persist log: " + ex.Message);
		}
	}

	private async Task WarmupModelAsync(int runId, ILlmClient llmClient, Provider provider, string modelIdentifier)
	{
		Log(runId, "info", "Warming up " + modelIdentifier + "...");
		try
		{
			await llmClient.CompletionAsync(provider, modelIdentifier, "You are a helpful assistant.", "Say 'ready' and nothing else.", 0.1, 0.9, 10);
			Log(runId, "info", modelIdentifier + " ready");
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Log(runId, "warning", "Warmup failed for " + modelIdentifier + ": " + ex2.Message);
		}
	}

	private void Log(int runId, string level, string message, string? modelName = null)
	{
		LogAsync(runId, level, message, modelName);
	}

	private async Task IncrementCompletedTests(int runId, IBenchmarkRepository benchmarkRepo)
	{
		if (_completedTests.TryGetValue(runId, out var count))
		{
			_completedTests[runId] = count + 1;
			await benchmarkRepo.UpdateRunProgressAsync(runId, _completedTests[runId], 0);
		}
	}
}
