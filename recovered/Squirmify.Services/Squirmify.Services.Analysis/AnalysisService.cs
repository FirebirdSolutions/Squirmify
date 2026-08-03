using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Squirmify.Core.DTOs;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;

namespace Squirmify.Services.Analysis;

public class AnalysisService : IAnalysisService
{
	private readonly IBenchmarkRepository _benchmarkRepo;

	private readonly IResultsRepository _resultsRepo;

	private readonly ITestDefinitionRepository _testDefRepo;

	private readonly IModelRepository _modelRepo;

	public AnalysisService(IBenchmarkRepository benchmarkRepo, IResultsRepository resultsRepo, ITestDefinitionRepository testDefRepo, IModelRepository modelRepo)
	{
		_benchmarkRepo = benchmarkRepo;
		_resultsRepo = resultsRepo;
		_testDefRepo = testDefRepo;
		_modelRepo = modelRepo;
	}

	public async Task<RunAnalysis> AnalyzeRunAsync(int runId)
	{
		BenchmarkRun run = await _benchmarkRepo.GetRunByIdAsync(runId);
		if (run == null)
		{
			throw new ArgumentException($"Run {runId} not found");
		}
		List<InstructionTestResult> instructionResults = (await _resultsRepo.GetInstructionResultsAsync(runId)).ToList();
		List<ReasoningTestResult> reasoningResults = (await _resultsRepo.GetReasoningResultsAsync(runId)).ToList();
		List<ConversationTestResult> conversationResults = (await _resultsRepo.GetConversationResultsAsync(runId)).ToList();
		List<GenerationResult> generationResults = (await _resultsRepo.GetGenerationResultsAsync(runId)).ToList();
		List<ContextWindowTestResult> contextResults = (await _resultsRepo.GetContextWindowResultsAsync(runId)).ToList();
		Dictionary<int, InstructionTest> instructionTests = (await _testDefRepo.GetInstructionTestsAsync(activeOnly: false)).ToDictionary((InstructionTest t) => t.Id);
		Dictionary<int, string> models = (await _modelRepo.GetAllAsync()).ToDictionary((Model m) => m.Id, (Model m) => m.DisplayName ?? m.Identifier);
		RunAnalysis analysis = new RunAnalysis
		{
			RunId = runId,
			RunName = run.Name,
			StartedAt = run.StartedAt,
			CompletedAt = run.CompletedAt,
			Duration = ((run.CompletedAt.HasValue && run.StartedAt.HasValue) ? (run.CompletedAt.Value - run.StartedAt.Value) : TimeSpan.Zero)
		};
		analysis.Overall = BuildOverallSummary(instructionResults, reasoningResults, conversationResults, generationResults, models);
		analysis.ModelSummaries = BuildModelSummaries(instructionResults, reasoningResults, conversationResults, generationResults, models);
		analysis.InstructionAnalysis = BuildInstructionAnalysis(instructionResults, instructionTests);
		analysis.ReasoningAnalysis = BuildReasoningAnalysis(reasoningResults);
		analysis.ConversationAnalysis = BuildConversationAnalysis(conversationResults);
		RunAnalysis runAnalysis = analysis;
		runAnalysis.ContextWindowAnalysis = await BuildContextWindowAnalysisAsync(runId, contextResults, models);
		analysis.Performance = BuildPerformanceAnalysis(instructionResults, reasoningResults, generationResults, models);
		return analysis;
	}

	private OverallSummary BuildOverallSummary(List<InstructionTestResult> instruction, List<ReasoningTestResult> reasoning, List<ConversationTestResult> conversation, List<GenerationResult> generation, Dictionary<int, string> models)
	{
		HashSet<int> hashSet = new HashSet<int>();
		hashSet.UnionWith(instruction.Select((InstructionTestResult r) => r.ModelId));
		hashSet.UnionWith(reasoning.Select((ReasoningTestResult r) => r.ModelId));
		hashSet.UnionWith(conversation.Select((ConversationTestResult r) => r.ModelId));
		hashSet.UnionWith(generation.Select((GenerationResult r) => r.ModelId));
		double num = (instruction.Any() ? ((double)instruction.Count((InstructionTestResult r) => r.Passed) / (double)instruction.Count * 100.0) : 0.0);
		double num2 = reasoning.Where((ReasoningTestResult r) => r.OverallScore.HasValue).DefaultIfEmpty().Average((ReasoningTestResult r) => (r?.OverallScore).GetValueOrDefault());
		double num3 = conversation.Where((ConversationTestResult r) => r.OverallScore.HasValue).DefaultIfEmpty().Average((ConversationTestResult r) => (r?.OverallScore).GetValueOrDefault());
		double num4 = generation.Where((GenerationResult r) => r.AvgScore.HasValue).DefaultIfEmpty().Average((GenerationResult r) => (r?.AvgScore).GetValueOrDefault());
		return new OverallSummary
		{
			TotalTests = instruction.Count + reasoning.Count + conversation.Count + generation.Count,
			CompletedTests = instruction.Count + reasoning.Count + conversation.Count + generation.Count,
			ModelCount = hashSet.Count,
			InstructionPassRate = num,
			ReasoningAvgScore = num2,
			ConversationAvgScore = num3,
			GenerationAvgScore = num4,
			HighQualityCount = generation.Count((GenerationResult r) => r.IsHighQuality),
			CompositeScore = (num / 10.0 + num2 + num3 + num4) / 4.0
		};
	}

	private List<ModelSummary> BuildModelSummaries(List<InstructionTestResult> instruction, List<ReasoningTestResult> reasoning, List<ConversationTestResult> conversation, List<GenerationResult> generation, Dictionary<int, string> models)
	{
		HashSet<int> hashSet = new HashSet<int>();
		hashSet.UnionWith(instruction.Select((InstructionTestResult r) => r.ModelId));
		hashSet.UnionWith(reasoning.Select((ReasoningTestResult r) => r.ModelId));
		hashSet.UnionWith(conversation.Select((ConversationTestResult r) => r.ModelId));
		hashSet.UnionWith(generation.Select((GenerationResult r) => r.ModelId));
		return (from m in hashSet.Select(delegate(int modelId)
			{
				List<InstructionTestResult> list = instruction.Where((InstructionTestResult r) => r.ModelId == modelId).ToList();
				List<ReasoningTestResult> list2 = reasoning.Where((ReasoningTestResult r) => r.ModelId == modelId).ToList();
				List<ConversationTestResult> list3 = conversation.Where((ConversationTestResult r) => r.ModelId == modelId).ToList();
				List<GenerationResult> list4 = generation.Where((GenerationResult r) => r.ModelId == modelId).ToList();
				double num = (list.Any() ? ((double)list.Count((InstructionTestResult r) => r.Passed) / (double)list.Count * 100.0) : 0.0);
				double num2 = list2.Where((ReasoningTestResult r) => r.OverallScore.HasValue).DefaultIfEmpty().Average((ReasoningTestResult r) => (r?.OverallScore).GetValueOrDefault());
				double num3 = list3.Where((ConversationTestResult r) => r.OverallScore.HasValue).DefaultIfEmpty().Average((ConversationTestResult r) => (r?.OverallScore).GetValueOrDefault());
				double num4 = list4.Where((GenerationResult r) => r.AvgScore.HasValue).DefaultIfEmpty().Average((GenerationResult r) => (r?.AvgScore).GetValueOrDefault());
				List<double> list5 = new List<double>();
				list5.AddRange(list.Select((InstructionTestResult r) => r.TotalMs));
				list5.AddRange(list2.Select((ReasoningTestResult r) => r.TotalMs));
				list5.AddRange(list4.Select((GenerationResult r) => r.TotalMs));
				List<double?> list6 = new List<double?>();
				list6.AddRange(list.Select((InstructionTestResult r) => r.TokensPerSec));
				list6.AddRange(list2.Select((ReasoningTestResult r) => r.TokensPerSec));
				list6.AddRange(list4.Select((GenerationResult r) => r.TokensPerSec));
				List<double> source = (from r in list
					where r.TokensPerSec.HasValue && r.TokensPerSec.Value > 0.0
					select r.TokensPerSec.Value).ToList();
				List<double> list7 = new List<double>();
				list7.AddRange(from r in list2
					where r.TokensPerSec.HasValue && r.TokensPerSec.Value > 0.0
					select r.TokensPerSec.Value);
				list7.AddRange(from r in list4
					where r.TokensPerSec.HasValue && r.TokensPerSec.Value > 0.0
					select r.TokensPerSec.Value);
				list7.AddRange(from r in list3
					where r.TokensPerSec.HasValue && r.TokensPerSec.Value > 0.0
					select r.TokensPerSec.Value);
				string value;
				return new ModelSummary
				{
					ModelId = modelId,
					ModelName = (models.TryGetValue(modelId, out value) ? value : $"Model #{modelId}"),
					InstructionPassRate = num,
					ReasoningAvgScore = num2,
					ConversationAvgScore = num3,
					GenerationAvgScore = num4,
					CompositeScore = (num / 10.0 + num2 + num3 + num4) / 4.0,
					AvgTokensPerSec = list6.Where((double? t) => t.HasValue && t.Value > 0.0).DefaultIfEmpty().Average((double? t) => t.GetValueOrDefault()),
					InstructionAvgTokensPerSec = (source.Any() ? source.Average() : 0.0),
					GenerationAvgTokensPerSec = (list7.Any() ? list7.Average() : 0.0),
					AvgLatencyMs = (list5.Any() ? list5.Average() : 0.0),
					TotalTests = list.Count + list2.Count + list3.Count + list4.Count,
					PassedTests = list.Count((InstructionTestResult r) => r.Passed)
				};
			})
			orderby m.CompositeScore descending
			select m).ToList();
	}

	private InstructionAnalysis BuildInstructionAnalysis(List<InstructionTestResult> results, Dictionary<int, InstructionTest> tests)
	{
		InstructionAnalysis instructionAnalysis = new InstructionAnalysis
		{
			TotalTests = results.Count,
			PassedTests = results.Count((InstructionTestResult r) => r.Passed),
			StrictPassedTests = results.Count((InstructionTestResult r) => r.StrictPass),
			PassRate = (results.Any() ? ((double)results.Count((InstructionTestResult r) => r.Passed) / (double)results.Count * 100.0) : 0.0),
			StrictPassRate = (results.Any() ? ((double)results.Count((InstructionTestResult r) => r.StrictPass) / (double)results.Count * 100.0) : 0.0)
		};
		List<CategoryBreakdown> byCategory = (from r in results
			group r by tests.TryGetValue(r.TestId, out InstructionTest value) ? value.Category : "unknown" into g
			select new CategoryBreakdown
			{
				Category = (g.Key ?? "unknown"),
				Total = g.Count(),
				Passed = g.Count((InstructionTestResult r) => r.Passed),
				PassRate = (double)g.Count((InstructionTestResult r) => r.Passed) / (double)g.Count() * 100.0,
				AvgLatencyMs = g.Average((InstructionTestResult r) => r.TotalMs)
			} into c
			orderby c.PassRate descending
			select c).ToList();
		instructionAnalysis.ByCategory = byCategory;
		instructionAnalysis.ByValidationType = (from r in results
			group r by tests.TryGetValue(r.TestId, out InstructionTest value) ? value.ValidationType : "unknown" into g
			select new ValidationTypeBreakdown
			{
				ValidationType = (g.Key ?? "unknown"),
				Total = g.Count(),
				Passed = g.Count((InstructionTestResult r) => r.Passed),
				PassRate = (double)g.Count((InstructionTestResult r) => r.Passed) / (double)g.Count() * 100.0
			} into v
			orderby v.Total descending
			select v).ToList();
		List<FailureReason> topFailures = (from r in results
			where !r.Passed && !string.IsNullOrEmpty(r.FailureReason)
			group r by NormalizeFailureReason(r.FailureReason) into g
			select new FailureReason
			{
				Reason = g.Key,
				Count = g.Count(),
				Percentage = (double)g.Count() / (double)results.Count((InstructionTestResult r) => !r.Passed) * 100.0
			} into f
			orderby f.Count descending
			select f).Take(10).ToList();
		instructionAnalysis.TopFailures = topFailures;
		return instructionAnalysis;
	}

	private string NormalizeFailureReason(string reason)
	{
		if (reason.Contains("Expected") && reason.Contains("but got"))
		{
			return "Output mismatch";
		}
		if (reason.Contains("JSON") || reason.Contains("json"))
		{
			return "Invalid JSON format";
		}
		if (reason.Contains("empty"))
		{
			return "Empty response";
		}
		if (reason.Contains("timeout"))
		{
			return "Request timeout";
		}
		return (reason.Length > 50) ? (reason.Substring(0, 50) + "...") : reason;
	}

	private ReasoningAnalysis BuildReasoningAnalysis(List<ReasoningTestResult> results)
	{
		List<ReasoningTestResult> source = results.Where((ReasoningTestResult r) => r.OverallScore.HasValue).ToList();
		ReasoningAnalysis reasoningAnalysis = new ReasoningAnalysis
		{
			TotalTests = results.Count,
			AvgOverallScore = (source.Any() ? source.Average((ReasoningTestResult r) => r.OverallScore.Value) : 0.0),
			AvgCorrectAnswerScore = source.Where((ReasoningTestResult r) => r.CorrectAnswerScore.HasValue).DefaultIfEmpty().Average((ReasoningTestResult r) => (r?.CorrectAnswerScore).GetValueOrDefault()),
			AvgLogicalStepsScore = source.Where((ReasoningTestResult r) => r.LogicalStepsScore.HasValue).DefaultIfEmpty().Average((ReasoningTestResult r) => (r?.LogicalStepsScore).GetValueOrDefault()),
			AvgClarityScore = source.Where((ReasoningTestResult r) => r.ClarityScore.HasValue).DefaultIfEmpty().Average((ReasoningTestResult r) => (r?.ClarityScore).GetValueOrDefault())
		};
		reasoningAnalysis.ScoreDistribution = new ScoreDistribution
		{
			Score0To2 = source.Count((ReasoningTestResult r) => r.OverallScore < 2.0),
			Score2To4 = source.Count((ReasoningTestResult r) => r.OverallScore >= 2.0 && r.OverallScore < 4.0),
			Score4To6 = source.Count((ReasoningTestResult r) => r.OverallScore >= 4.0 && r.OverallScore < 6.0),
			Score6To8 = source.Count((ReasoningTestResult r) => r.OverallScore >= 6.0 && r.OverallScore < 8.0),
			Score8To10 = source.Count((ReasoningTestResult r) => r.OverallScore >= 8.0)
		};
		return reasoningAnalysis;
	}

	private ConversationAnalysis BuildConversationAnalysis(List<ConversationTestResult> results)
	{
		List<ConversationTestResult> source = results.Where((ConversationTestResult r) => r.OverallScore.HasValue).ToList();
		return new ConversationAnalysis
		{
			TotalTests = results.Count,
			AvgOverallScore = (source.Any() ? source.Average((ConversationTestResult r) => r.OverallScore.Value) : 0.0),
			AvgTopicCoherence = source.Where((ConversationTestResult r) => r.TopicCoherence.HasValue).DefaultIfEmpty().Average((ConversationTestResult r) => (r?.TopicCoherence).GetValueOrDefault()),
			AvgConversationalTone = source.Where((ConversationTestResult r) => r.ConversationalTone.HasValue).DefaultIfEmpty().Average((ConversationTestResult r) => (r?.ConversationalTone).GetValueOrDefault()),
			AvgContextRetention = source.Where((ConversationTestResult r) => r.ContextRetention.HasValue).DefaultIfEmpty().Average((ConversationTestResult r) => (r?.ContextRetention).GetValueOrDefault()),
			AvgHelpfulness = source.Where((ConversationTestResult r) => r.Helpfulness.HasValue).DefaultIfEmpty().Average((ConversationTestResult r) => (r?.Helpfulness).GetValueOrDefault())
		};
	}

	private async Task<ContextWindowAnalysis> BuildContextWindowAnalysisAsync(int runId, List<ContextWindowTestResult> results, Dictionary<int, string> models)
	{
		if (!results.Any())
		{
			return new ContextWindowAnalysis();
		}
		List<ContextWindowTestResult> withTokens = results.Where((ContextWindowTestResult r) => r.MaxReliableTokens.HasValue).ToList();
		List<ContextWindowTestResult> withAccuracy = results.Where((ContextWindowTestResult r) => r.CheckpointAccuracy.HasValue).ToList();
		ContextWindowAnalysis analysis = new ContextWindowAnalysis
		{
			TotalTests = results.Count,
			MaxReliableTokensAvg = (withTokens.Any() ? ((int)withTokens.Average((ContextWindowTestResult r) => r.MaxReliableTokens.Value)) : 0),
			MaxReliableTokensMax = (withTokens.Any() ? withTokens.Max((ContextWindowTestResult r) => r.MaxReliableTokens.Value) : 0),
			MaxReliableTokensMin = (withTokens.Any() ? withTokens.Min((ContextWindowTestResult r) => r.MaxReliableTokens.Value) : 0),
			AvgCheckpointAccuracy = (withAccuracy.Any() ? withAccuracy.Average((ContextWindowTestResult r) => r.CheckpointAccuracy.Value) : 0.0)
		};
		analysis.DegradationPatterns = (from r in results
			where !string.IsNullOrEmpty(r.DegradationPattern)
			group r by r.DegradationPattern).ToDictionary((IGrouping<string, ContextWindowTestResult> g) => g.Key, (IGrouping<string, ContextWindowTestResult> g) => g.Count());
		analysis.ByModel = (from r in results
			select new ModelContextSummary
			{
				ModelId = r.ModelId,
				ModelName = (models.TryGetValue(r.ModelId, out string value) ? value : $"Model #{r.ModelId}"),
				MaxReliableTokens = r.MaxReliableTokens.GetValueOrDefault(),
				CheckpointAccuracy = r.CheckpointAccuracy.GetValueOrDefault(),
				DegradationPattern = (r.DegradationPattern ?? "unknown")
			} into m
			orderby m.MaxReliableTokens descending
			select m).ToList();
		return analysis;
	}

	private PerformanceAnalysis BuildPerformanceAnalysis(List<InstructionTestResult> instruction, List<ReasoningTestResult> reasoning, List<GenerationResult> generation, Dictionary<int, string> models)
	{
		List<double> list = new List<double>();
		list.AddRange(instruction.Select((InstructionTestResult r) => r.TotalMs));
		list.AddRange(reasoning.Select((ReasoningTestResult r) => r.TotalMs));
		list.AddRange(generation.Select((GenerationResult r) => r.TotalMs));
		List<double> list2 = new List<double>();
		list2.AddRange(from r in instruction
			where r.TokensPerSec.HasValue
			select r.TokensPerSec.Value);
		list2.AddRange(from r in reasoning
			where r.TokensPerSec.HasValue
			select r.TokensPerSec.Value);
		list2.AddRange(from r in generation
			where r.TokensPerSec.HasValue
			select r.TokensPerSec.Value);
		List<double> source = (from r in instruction
			where r.TokensPerSec.HasValue && r.TokensPerSec.Value > 0.0
			select r.TokensPerSec.Value).ToList();
		List<double> list3 = new List<double>();
		list3.AddRange(from r in reasoning
			where r.TokensPerSec.HasValue && r.TokensPerSec.Value > 0.0
			select r.TokensPerSec.Value);
		list3.AddRange(from r in generation
			where r.TokensPerSec.HasValue && r.TokensPerSec.Value > 0.0
			select r.TokensPerSec.Value);
		List<double> sortedValues = list.OrderBy((double l) => l).ToList();
		PerformanceAnalysis performanceAnalysis = new PerformanceAnalysis
		{
			AvgTokensPerSec = (list2.Any() ? list2.Average() : 0.0),
			MaxTokensPerSec = (list2.Any() ? list2.Max() : 0.0),
			MinTokensPerSec = (list2.Any() ? list2.Min() : 0.0),
			InstructionAvgTokensPerSec = (source.Any() ? source.Average() : 0.0),
			GenerationAvgTokensPerSec = (list3.Any() ? list3.Average() : 0.0),
			AvgLatencyMs = (list.Any() ? list.Average() : 0.0),
			P50LatencyMs = GetPercentile(sortedValues, 50),
			P95LatencyMs = GetPercentile(sortedValues, 95),
			P99LatencyMs = GetPercentile(sortedValues, 99),
			TotalPromptTokens = instruction.Sum((InstructionTestResult r) => r.PromptTokens.GetValueOrDefault()) + reasoning.Sum((ReasoningTestResult r) => r.PromptTokens.GetValueOrDefault()),
			TotalCompletionTokens = instruction.Sum((InstructionTestResult r) => r.CompletionTokens.GetValueOrDefault()) + reasoning.Sum((ReasoningTestResult r) => r.CompletionTokens.GetValueOrDefault())
		};
		HashSet<int> hashSet = new HashSet<int>();
		hashSet.UnionWith(instruction.Select((InstructionTestResult r) => r.ModelId));
		hashSet.UnionWith(reasoning.Select((ReasoningTestResult r) => r.ModelId));
		hashSet.UnionWith(generation.Select((GenerationResult r) => r.ModelId));
		performanceAnalysis.ByModel = (from m in hashSet.Select(delegate(int modelId)
			{
				List<double> list4 = new List<double>();
				list4.AddRange(from r in instruction
					where r.ModelId == modelId
					select r.TotalMs);
				list4.AddRange(from r in reasoning
					where r.ModelId == modelId
					select r.TotalMs);
				list4.AddRange(from r in generation
					where r.ModelId == modelId
					select r.TotalMs);
				List<double> list5 = new List<double>();
				list5.AddRange(from r in instruction
					where r.ModelId == modelId && r.TokensPerSec.HasValue
					select r.TokensPerSec.Value);
				list5.AddRange(from r in reasoning
					where r.ModelId == modelId && r.TokensPerSec.HasValue
					select r.TokensPerSec.Value);
				list5.AddRange(from r in generation
					where r.ModelId == modelId && r.TokensPerSec.HasValue
					select r.TokensPerSec.Value);
				List<double> source2 = (from r in instruction
					where r.ModelId == modelId && r.TokensPerSec.HasValue && r.TokensPerSec.Value > 0.0
					select r.TokensPerSec.Value).ToList();
				List<double> list6 = new List<double>();
				list6.AddRange(from r in reasoning
					where r.ModelId == modelId && r.TokensPerSec.HasValue && r.TokensPerSec.Value > 0.0
					select r.TokensPerSec.Value);
				list6.AddRange(from r in generation
					where r.ModelId == modelId && r.TokensPerSec.HasValue && r.TokensPerSec.Value > 0.0
					select r.TokensPerSec.Value);
				string value;
				return new ModelPerformance
				{
					ModelId = modelId,
					ModelName = (models.TryGetValue(modelId, out value) ? value : $"Model #{modelId}"),
					AvgTokensPerSec = (list5.Any() ? list5.Average() : 0.0),
					InstructionAvgTokensPerSec = (source2.Any() ? source2.Average() : 0.0),
					GenerationAvgTokensPerSec = (list6.Any() ? list6.Average() : 0.0),
					AvgLatencyMs = (list4.Any() ? list4.Average() : 0.0),
					TotalRequests = list4.Count
				};
			})
			orderby m.AvgTokensPerSec descending
			select m).ToList();
		return performanceAnalysis;
	}

	private double GetPercentile(List<double> sortedValues, int percentile)
	{
		if (!sortedValues.Any())
		{
			return 0.0;
		}
		int val = (int)Math.Ceiling((double)percentile / 100.0 * (double)sortedValues.Count) - 1;
		return sortedValues[Math.Max(0, Math.Min(val, sortedValues.Count - 1))];
	}

	public async Task<RunComparison> CompareRunsAsync(IEnumerable<int> runIds)
	{
		RunComparison comparison = new RunComparison();
		List<int> runIdList = runIds.ToList();
		foreach (int runId in runIdList)
		{
			RunAnalysis analysis = await AnalyzeRunAsync(runId);
			comparison.Runs.Add(new RunComparisonEntry
			{
				RunId = runId,
				RunName = analysis.RunName,
				StartedAt = analysis.StartedAt,
				InstructionPassRate = analysis.Overall.InstructionPassRate,
				ReasoningAvgScore = analysis.Overall.ReasoningAvgScore,
				ConversationAvgScore = analysis.Overall.ConversationAvgScore,
				GenerationAvgScore = analysis.Overall.GenerationAvgScore,
				CompositeScore = analysis.Overall.CompositeScore,
				AvgTokensPerSec = analysis.Performance.AvgTokensPerSec,
				ModelCount = analysis.Overall.ModelCount
			});
		}
		comparison.Runs.SelectMany((RunComparisonEntry _) => runIdList).Distinct();
		Dictionary<int, List<(int RunId, ModelSummary Summary)>> modelAnalyses = new Dictionary<int, List<(int, ModelSummary)>>();
		foreach (int runId2 in runIdList)
		{
			foreach (ModelSummary modelSummary in (await AnalyzeRunAsync(runId2)).ModelSummaries)
			{
				if (!modelAnalyses.ContainsKey(modelSummary.ModelId))
				{
					modelAnalyses[modelSummary.ModelId] = new List<(int, ModelSummary)>();
				}
				modelAnalyses[modelSummary.ModelId].Add((runId2, modelSummary));
			}
		}
		foreach (KeyValuePair<int, List<(int, ModelSummary)>> item in modelAnalyses.Where<KeyValuePair<int, List<(int, ModelSummary)>>>((KeyValuePair<int, List<(int RunId, ModelSummary Summary)>> m) => m.Value.Count > 1))
		{
			item.Deconstruct(out var key, out var value);
			int modelId = key;
			List<(int RunId, ModelSummary Summary)> runSummaries = value;
			ModelComparisonAcrossRuns modelComparison = new ModelComparisonAcrossRuns
			{
				ModelId = modelId,
				ModelName = runSummaries.First().Summary.ModelName,
				RunScores = runSummaries.OrderBy(((int RunId, ModelSummary Summary) r) => r.RunId).Select(delegate((int RunId, ModelSummary Summary) rs, int index)
				{
					double? delta = null;
					if (index > 0)
					{
						double compositeScore = runSummaries[index - 1].Summary.CompositeScore;
						delta = rs.Summary.CompositeScore - compositeScore;
					}
					return new ModelRunScore
					{
						RunId = rs.RunId,
						InstructionPassRate = rs.Summary.InstructionPassRate,
						ReasoningAvgScore = rs.Summary.ReasoningAvgScore,
						ConversationAvgScore = rs.Summary.ConversationAvgScore,
						CompositeScore = rs.Summary.CompositeScore,
						Delta = delta
					};
				}).ToList()
			};
			comparison.ModelComparisons.Add(modelComparison);
		}
		return comparison;
	}

	public async Task<ModelComparisonAcrossRuns> CompareModelAcrossRunsAsync(int modelId, IEnumerable<int> runIds)
	{
		string modelName = (await _modelRepo.GetAllAsync()).FirstOrDefault((Model m) => m.Id == modelId)?.DisplayName ?? $"Model #{modelId}";
		ModelComparisonAcrossRuns comparison = new ModelComparisonAcrossRuns
		{
			ModelId = modelId,
			ModelName = modelName
		};
		double previousScore = 0.0;
		foreach (int runId in runIds.OrderBy((int id) => id))
		{
			ModelSummary modelSummary = (await AnalyzeRunAsync(runId)).ModelSummaries.FirstOrDefault((ModelSummary m) => m.ModelId == modelId);
			if (modelSummary != null)
			{
				double? delta = (comparison.RunScores.Any() ? new double?(modelSummary.CompositeScore - previousScore) : ((double?)null));
				comparison.RunScores.Add(new ModelRunScore
				{
					RunId = runId,
					InstructionPassRate = modelSummary.InstructionPassRate,
					ReasoningAvgScore = modelSummary.ReasoningAvgScore,
					ConversationAvgScore = modelSummary.ConversationAvgScore,
					CompositeScore = modelSummary.CompositeScore,
					Delta = delta
				});
				previousScore = modelSummary.CompositeScore;
			}
		}
		return comparison;
	}
}
