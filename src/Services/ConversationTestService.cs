using System.Text.Json;
using System.Text.RegularExpressions;
using ModelEvaluator.Models;
using Spectre.Console;

namespace ModelEvaluator.Services;

public class ConversationTestService
{
    private readonly ModelService _modelService;
    private List<ConversationTest>? _conversationTests;
    private ConversationTestsConfig? _config;

    public ConversationTestService(ModelService modelService)
    {
        _modelService = modelService;
    }

    /// <summary>
    /// Load conversation tests from external config file with category filtering
    /// </summary>
    private async Task<List<ConversationTest>> GetConversationTestsAsync()
    {
        if (_conversationTests != null) return _conversationTests;

        _config = await ConfigLoader.LoadConversationTestsAsync();

        var allTests = _config.Tests.Select(t => new ConversationTest
        {
            Category = t.Category,
            Description = t.Description,
            SystemPrompt = t.SystemPrompt,
            Turns = t.Turns.Select(turn => new ConversationTurn
            {
                UserMessage = turn.UserMessage,
                ExpectedTheme = turn.ExpectedTheme
            }).ToList(),
            JudgingCriteria = t.JudgingCriteria
        }).ToList();

        // Filter by category limits
        _conversationTests = TestFilterHelper.FilterByCategory(
            allTests,
            t => t.Category,
            Config.ConversationTestMaxPerCategory,
            Config.ConversationTestCategoryLimits);

        AnsiConsole.MarkupLine($"[dim]Selected {_conversationTests.Count} of {allTests.Count} conversation tests based on category limits[/]");

        return _conversationTests;
    }

    /// <summary>
    /// Get the judge system prompt from config
    /// </summary>
    private string GetJudgeSystemPrompt()
    {
        return _config?.JudgeSystemPrompt ?? "You are an expert evaluator of AI conversations. You assess multi-turn conversations for quality, coherence, and natural flow.";
    }

    /// <summary>
    /// Run all conversation tests on all models
    /// </summary>
    public async Task<List<ConversationTestResult>> RunConversationTestsAsync(List<string> models)
    {
        AnsiConsole.MarkupLine("\n[bold cyan]═══ Running Multi-Turn Conversation Tests ═══[/]\n");

        // Load tests from config
        var tests = await GetConversationTestsAsync();
        var results = new List<ConversationTestResult>();
        var totalTests = models.Count * tests.Count;

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Testing conversations[/]", maxValue: totalTests);

                foreach (var model in models)
                {
                    // Warm up
                    var warmupOk = await _modelService.WarmUpModelAsync(model);
                    if (!warmupOk)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ Failed to warm up {model}[/]");
                        task.Increment(tests.Count);
                        continue;
                    }

                    foreach (var test in tests)
                    {
                        task.Description = $"[yellow]{model} → {test.Category}: {test.Description}[/]";

                        var result = await RunSingleConversationAsync(model, test);
                        if (result != null)
                        {
                            results.Add(result);
                        }

                        task.Increment(1);
                    }
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ Completed {results.Count} conversation tests[/]\n");

        return results;
    }

    /// <summary>
    /// Build a context-aware prompt that includes conversation history
    /// </summary>
    private string BuildContextPrompt(List<Message> history, string currentMessage)
    {
        if (!history.Any())
            return currentMessage;

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("Previous conversation:");
        contextBuilder.AppendLine();

        foreach (var msg in history)
        {
            var role = msg.Role == "user" ? "User" : "Assistant";
            contextBuilder.AppendLine($"{role}: {msg.Content}");
        }

        contextBuilder.AppendLine();
        contextBuilder.AppendLine($"User: {currentMessage}");
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Respond naturally, maintaining context from the conversation above.");

        return contextBuilder.ToString();
    }

    /// <summary>
    /// Run a single multi-turn conversation
    /// </summary>
    private async Task<ConversationTestResult?> RunSingleConversationAsync(string modelName, ConversationTest test)
    {
        var result = new ConversationTestResult
        {
            ModelName = modelName,
            Category = test.Category,
            Description = test.Description
        };

        var messageHistory = new List<Message>();
        var perfMetrics = new List<PerfMetrics>();

        // Run through each turn
        for (int i = 0; i < test.Turns.Count; i++)
        {
            var turn = test.Turns[i];

            // Add user message to history
            messageHistory.Add(new Message("user", turn.UserMessage));

            // Build context-aware prompt for multi-turn conversations
            var contextPrompt = BuildContextPrompt(messageHistory, turn.UserMessage);
            
            var response = await _modelService.CompletionAsync(
                modelName,
                test.SystemPrompt,
                contextPrompt,
                Config.ConversationTestTemperature,
                Config.ConversationTestTopP,
                600 // Longer max tokens for conversational responses
            );

            if (response == null)
            {
                // Conversation failed mid-way
                return null;
            }

            var (responseText, perf) = response.Value;
            perfMetrics.Add(perf);

            // Add assistant response to history
            messageHistory.Add(new Message("assistant", responseText));

            // Record this exchange
            result.Exchanges.Add(new ConversationExchange
            {
                TurnNumber = i + 1,
                UserMessage = turn.UserMessage,
                ModelResponse = responseText,
                Perf = perf
            });
        }

        // Calculate aggregate performance
        if (perfMetrics.Any())
        {
            result.AggregatePerf = new PerfMetrics
            {
                tokens_per_sec = perfMetrics.Where(p => p.tokens_per_sec.HasValue).Any()
                    ? perfMetrics.Where(p => p.tokens_per_sec.HasValue).Average(p => p.tokens_per_sec!.Value)
                    : null,
                total_ms = perfMetrics.Sum(p => p.total_ms),
                completion_tokens = perfMetrics.Sum(p => p.completion_tokens ?? 0),
                prompt_tokens = perfMetrics.Sum(p => p.prompt_tokens ?? 0)
            };
        }

        return result;
    }

    /// <summary>
    /// Score all conversation results using a judge model
    /// </summary>
    public async Task ScoreConversationsAsync(
        string judgeModel,
        List<ConversationTestResult> results)
    {
        AnsiConsole.MarkupLine($"\n[bold cyan]═══ Scoring Conversations with Judge: {judgeModel} ═══[/]\n");

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Scoring conversations[/]", maxValue: results.Count);

                foreach (var result in results)
                {
                    task.Description = $"[yellow]Scoring {result.ModelName} - {result.Category}[/]";

                    var rating = await ScoreConversationAsync(judgeModel, result);
                    if (rating != null)
                    {
                        result.Rating = rating;
                    }

                    task.Increment(1);
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ Scored {results.Count} conversations[/]\n");
    }

    /// <summary>
    /// Score a single conversation
    /// </summary>
    private async Task<ConversationRating?> ScoreConversationAsync(string judgeModel, ConversationTestResult result)
    {
        var judgePrompt = BuildConversationJudgePrompt(result);
        var judgeSystemPrompt = GetJudgeSystemPrompt();

        var response = await _modelService.CompletionAsync(
            judgeModel,
            judgeSystemPrompt,
            judgePrompt,
            0.3, // Low temperature for consistent judging
            0.9,
            500
        );

        if (response == null)
            return null;

        var (responseText, _) = response.Value;

        return ParseConversationJudgeResponse(responseText, judgeModel);
    }

    private string BuildConversationJudgePrompt(ConversationTestResult result)
    {
        var conversationLog = string.Join("\n\n", result.Exchanges.Select(ex =>
            $"Turn {ex.TurnNumber}:\nUser: {ex.UserMessage}\nAssistant: {ex.ModelResponse}"));

        return $@"Evaluate this multi-turn AI conversation:

Category: {result.Category}
Scenario: {result.Description}

Conversation:
{conversationLog}

Performance:
- Avg Tokens/sec: {result.AggregatePerf.tokens_per_sec:F1}
- Total Time: {result.AggregatePerf.total_ms:F0}ms
- Total Turns: {result.Exchanges.Count}

Rate this conversation from 1-10 on these dimensions:
1. Topic Coherence (stayed on topic, didn't drift)
2. Conversational Tone (natural, appropriate, friendly)
3. Context Retention (remembered earlier turns)
4. Helpfulness (moved conversation forward productively)

Respond in this exact JSON format:
{{
  ""overall_score"": <1-10>,
  ""topic_coherence"": <1-10>,
  ""conversational_tone"": <1-10>,
  ""context_retention"": <1-10>,
  ""helpfulness"": <1-10>,
  ""reasoning"": ""<brief explanation of overall score>""
}}";
    }

    private ConversationRating? ParseConversationJudgeResponse(string response, string judgeModel)
    {
        try
        {
            // Clean markdown if present
            response = Regex.Replace(response, @"```json\s*", "", RegexOptions.IgnoreCase);
            response = Regex.Replace(response, @"```\s*", "", RegexOptions.IgnoreCase);
            response = response.Trim();

            var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;

            return new ConversationRating
            {
                OverallScore = root.GetProperty("overall_score").GetInt32(),
                TopicCoherence = root.GetProperty("topic_coherence").GetInt32(),
                ConversationalTone = root.GetProperty("conversational_tone").GetInt32(),
                ContextRetention = root.GetProperty("context_retention").GetInt32(),
                Helpfulness = root.GetProperty("helpfulness").GetInt32(),
                Reasoning = root.GetProperty("reasoning").GetString() ?? "",
                Rater = judgeModel
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine($"Failed to parse conversation judge response: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generate summary report for conversation tests
    /// </summary>
    public List<ConversationTestSummary> GenerateConversationSummaries(List<ConversationTestResult> results)
    {
        var modelGroups = results.GroupBy(r => r.ModelName);

        var summaries = modelGroups.Select(group =>
        {
            var modelResults = group.Where(r => r.Rating != null).ToList();
            
            if (!modelResults.Any())
                return null;

            var categoryScores = modelResults
                .GroupBy(r => r.Category)
                .ToDictionary(g => g.Key, g => g.Average(r => r.Rating!.OverallScore));

            return new ConversationTestSummary
            {
                ModelName = group.Key,
                AvgOverallScore = modelResults.Average(r => r.Rating!.OverallScore),
                AvgTokensPerSec = modelResults.Average(r => r.AggregatePerf.tokens_per_sec ?? 0),
                AvgLatencyMs = modelResults.Average(r => r.AggregatePerf.total_ms),
                CategoryScores = categoryScores,
                TotalConversations = modelResults.Count
            };
        })
        .Where(s => s != null)
        .OrderByDescending(s => s!.AvgOverallScore)
        .ThenByDescending(s => s!.AvgTokensPerSec)
        .ToList()!;

        return summaries;
    }

    /// <summary>
    /// Display conversation test results
    /// </summary>
    public void DisplayConversationResults(List<ConversationTestSummary> summaries)
    {
        if (!summaries.Any()) return;

        AnsiConsole.MarkupLine("\n[bold cyan]═══ Conversation Test Summary ═══[/]\n");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Model[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Overall Score[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Code[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Support[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Chat[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Instruction[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Avg t/s[/]").RightAligned());

        foreach (var summary in summaries)
        {
            table.AddRow(
                summary.ModelName,
                summary.AvgOverallScore.ToString("F1"),
                summary.CategoryScores.GetValueOrDefault("code", 0).ToString("F1"),
                summary.CategoryScores.GetValueOrDefault("support", 0).ToString("F1"),
                summary.CategoryScores.GetValueOrDefault("chat", 0).ToString("F1"),
                summary.CategoryScores.GetValueOrDefault("instruction", 0).ToString("F1"),
                summary.AvgTokensPerSec.ToString("F1")
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Save conversation results to JSON
    /// </summary>
    public async Task SaveConversationResultsAsync(List<ConversationTestResult> results, string filePath)
    {
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
        AnsiConsole.MarkupLine($"[green]✓ Saved conversation results → {filePath}[/]");
    }
}
