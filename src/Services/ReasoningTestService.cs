using System.Text.Json;
using ModelEvaluator.Models;
using Spectre.Console;

namespace ModelEvaluator.Services;

/// <summary>
/// Represents a reasoning test that will be judged for quality
/// </summary>
public class ReasoningTest
{
    public string Category { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string CorrectAnswer { get; set; } = ""; // What the right answer is (for judge reference)
    public string Description { get; set; } = "";
}

/// <summary>
/// Result of a single reasoning test
/// </summary>
public class ReasoningTestResponse
{
    public string ModelName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Response { get; set; } = "";
    public string CorrectAnswer { get; set; } = "";
    public PerfMetrics Perf { get; set; } = new();
    public ReasoningRating? Rating { get; set; }
}

/// <summary>
/// Judge's rating of a reasoning response
/// </summary>
public class ReasoningRating
{
    public int OverallScore { get; set; } // 1-10
    public int CorrectAnswer { get; set; } // 1-10 (did they get it right?)
    public int LogicalSteps { get; set; } // 1-10 (showed their work?)
    public int Clarity { get; set; } // 1-10 (clear explanation?)
    public string Reasoning { get; set; } = "";
    public string Rater { get; set; } = "";
}

/// <summary>
/// Summary of reasoning test results for a model
/// </summary>
public class ReasoningTestSummary
{
    public string ModelName { get; set; } = "";
    public double AvgOverallScore { get; set; }
    public double AvgCorrectAnswerScore { get; set; }
    public double AvgLogicalStepsScore { get; set; }
    public double AvgClarityScore { get; set; }
    public double AvgTokensPerSec { get; set; }
    public double AvgLatencyMs { get; set; }
    public Dictionary<string, double> CategoryScores { get; set; } = new();
    public int TotalTests { get; set; }
}

public static class ReasoningTests
{
    private static List<ReasoningTest>? _cachedTests;
    private static ReasoningTestsConfig? _cachedConfig;

    /// <summary>
    /// Get tests from external config file with category filtering
    /// </summary>
    public static async Task<List<ReasoningTest>> GetTestsAsync()
    {
        if (_cachedTests != null) return _cachedTests;

        _cachedConfig = await ConfigLoader.LoadReasoningTestsAsync();

        var allTests = _cachedConfig.Tests.Select(t => new ReasoningTest
        {
            Category = t.Category,
            Description = t.Description,
            Prompt = t.Prompt,
            CorrectAnswer = t.CorrectAnswer
        }).ToList();

        // Filter by category limits
        _cachedTests = TestFilterHelper.FilterByCategory(
            allTests,
            t => t.Category,
            Config.ReasoningTestMaxPerCategory,
            Config.ReasoningTestCategoryLimits);

        AnsiConsole.MarkupLine($"[dim]Selected {_cachedTests.Count} of {allTests.Count} reasoning tests based on category limits[/]");

        return _cachedTests;
    }

    /// <summary>
    /// Get the system prompt for reasoning tests
    /// </summary>
    public static string GetSystemPrompt()
    {
        return _cachedConfig?.SystemPrompt ?? "You are a helpful assistant that thinks through problems step by step.";
    }

    /// <summary>
    /// Get the judge system prompt
    /// </summary>
    public static string GetJudgeSystemPrompt()
    {
        return _cachedConfig?.JudgeSystemPrompt ?? "You are an expert evaluator of reasoning and problem-solving. You score responses objectively based on correctness, logic, and clarity.";
    }

    /// <summary>
    /// Get the judge prompt template
    /// </summary>
    public static string GetJudgePromptTemplate()
    {
        return _cachedConfig?.JudgePromptTemplate ?? "";
    }

    /// <summary>
    /// Synchronous version for backwards compatibility
    /// </summary>
    public static List<ReasoningTest> GetTests()
    {
        return GetTestsAsync().GetAwaiter().GetResult();
    }
}

public class ReasoningTestService
{
    private readonly ModelService _modelService;

    public ReasoningTestService(ModelService modelService)
    {
        _modelService = modelService;
    }

    /// <summary>
    /// Run reasoning tests on all models
    /// </summary>
    public async Task<List<ReasoningTestResponse>> RunReasoningTestsAsync(List<string> models)
    {
        AnsiConsole.MarkupLine("\n[bold cyan]═══ Running Reasoning Tests ═══[/]\n");

        var results = new List<ReasoningTestResponse>();
        var tests = await ReasoningTests.GetTestsAsync();
        var systemPrompt = ReasoningTests.GetSystemPrompt();
        var totalTests = models.Count * tests.Count;

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Testing models[/]", maxValue: totalTests);

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

                        var response = await _modelService.CompletionAsync(
                            model,
                            systemPrompt,
                            test.Prompt,
                            0.3, // Low temp for consistent reasoning
                            0.9,
                            600
                        );

                        if (response != null)
                        {
                            var (responseText, perf) = response.Value;
                            
                            results.Add(new ReasoningTestResponse
                            {
                                ModelName = model,
                                Category = test.Category,
                                Prompt = test.Prompt,
                                Response = responseText,
                                CorrectAnswer = test.CorrectAnswer,
                                Perf = perf
                            });
                        }

                        task.Increment(1);
                    }
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ Completed {results.Count} reasoning tests[/]\n");

        return results;
    }

    /// <summary>
    /// Score reasoning responses using a judge model
    /// </summary>
    public async Task ScoreReasoningTestsAsync(
        string judgeModel,
        List<ReasoningTestResponse> responses)
    {
        AnsiConsole.MarkupLine($"\n[bold cyan]═══ Scoring Reasoning Tests with Judge: {judgeModel} ═══[/]\n");

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Scoring responses[/]", maxValue: responses.Count);

                foreach (var response in responses)
                {
                    task.Description = $"[yellow]Scoring {response.ModelName} - {response.Category}[/]";

                    var rating = await ScoreReasoningResponseAsync(judgeModel, response);
                    if (rating != null)
                    {
                        response.Rating = rating;
                    }

                    task.Increment(1);
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ Scored {responses.Count} reasoning responses[/]\n");
    }

    /// <summary>
    /// Score a single reasoning response
    /// </summary>
    private async Task<ReasoningRating?> ScoreReasoningResponseAsync(
        string judgeModel,
        ReasoningTestResponse response)
    {
        // Use template from config if available, otherwise use default
        var template = ReasoningTests.GetJudgePromptTemplate();
        string judgePrompt;

        if (!string.IsNullOrEmpty(template))
        {
            judgePrompt = template
                .Replace("{prompt}", response.Prompt)
                .Replace("{correctAnswer}", response.CorrectAnswer)
                .Replace("{response}", response.Response);
        }
        else
        {
            judgePrompt = $@"Evaluate this reasoning response:

Question: {response.Prompt}

Correct Answer (for reference): {response.CorrectAnswer}

Model's Response:
{response.Response}

Rate this response from 1-10 on these dimensions:
1. Correct Answer (did they get the right answer?)
2. Logical Steps (did they show their reasoning/work?)
3. Clarity (was the explanation clear and easy to follow?)

Respond in this exact JSON format:
{{
  ""overall_score"": <1-10>,
  ""correct_answer"": <1-10>,
  ""logical_steps"": <1-10>,
  ""clarity"": <1-10>,
  ""reasoning"": ""<brief explanation of overall score>""
}}";
        }

        var judgeSystemPrompt = ReasoningTests.GetJudgeSystemPrompt();

        var judgeResponse = await _modelService.CompletionAsync(
            judgeModel,
            judgeSystemPrompt,
            judgePrompt,
            0.3,
            0.9,
            400
        );

        if (judgeResponse == null)
            return null;

        var (responseText, _) = judgeResponse.Value;

        return ParseReasoningJudgeResponse(responseText, judgeModel);
    }

    private ReasoningRating? ParseReasoningJudgeResponse(string response, string judgeModel)
    {
        try
        {
            // Clean markdown if present
            response = System.Text.RegularExpressions.Regex.Replace(response, @"```json\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            response = System.Text.RegularExpressions.Regex.Replace(response, @"```\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            response = response.Trim();

            var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;

            return new ReasoningRating
            {
                OverallScore = root.GetProperty("overall_score").GetInt32(),
                CorrectAnswer = root.GetProperty("correct_answer").GetInt32(),
                LogicalSteps = root.GetProperty("logical_steps").GetInt32(),
                Clarity = root.GetProperty("clarity").GetInt32(),
                Reasoning = root.GetProperty("reasoning").GetString() ?? "",
                Rater = judgeModel
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine($"Failed to parse reasoning judge response: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generate summary report for reasoning tests
    /// </summary>
    public List<ReasoningTestSummary> GenerateReasoningSummaries(List<ReasoningTestResponse> responses)
    {
        var modelGroups = responses.GroupBy(r => r.ModelName);

        var summaries = modelGroups.Select(group =>
        {
            var modelResponses = group.Where(r => r.Rating != null).ToList();
            
            if (!modelResponses.Any())
                return null;

            var categoryScores = modelResponses
                .GroupBy(r => r.Category)
                .ToDictionary(g => g.Key, g => g.Average(r => r.Rating!.OverallScore));

            return new ReasoningTestSummary
            {
                ModelName = group.Key,
                AvgOverallScore = modelResponses.Average(r => r.Rating!.OverallScore),
                AvgCorrectAnswerScore = modelResponses.Average(r => r.Rating!.CorrectAnswer),
                AvgLogicalStepsScore = modelResponses.Average(r => r.Rating!.LogicalSteps),
                AvgClarityScore = modelResponses.Average(r => r.Rating!.Clarity),
                AvgTokensPerSec = modelResponses.Average(r => r.Perf.tokens_per_sec ?? 0),
                AvgLatencyMs = modelResponses.Average(r => r.Perf.total_ms),
                CategoryScores = categoryScores,
                TotalTests = modelResponses.Count
            };
        })
        .Where(s => s != null)
        .OrderByDescending(s => s!.AvgOverallScore)
        .ThenByDescending(s => s!.AvgTokensPerSec)
        .ToList()!;

        return summaries;
    }

    /// <summary>
    /// Display reasoning test results
    /// </summary>
    public void DisplayReasoningResults(List<ReasoningTestSummary> summaries)
    {
        if (!summaries.Any()) return;

        AnsiConsole.MarkupLine("\n[bold cyan]═══ Reasoning Test Summary ═══[/]\n");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Model[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Overall[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Correct[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Logic[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Clarity[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Avg t/s[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Status[/]").Centered());

        foreach (var summary in summaries)
        {
            var statusIcon = summary.AvgOverallScore >= 7.0 ? "[green]✓[/]" : "[yellow]~[/]";
            
            table.AddRow(
                summary.ModelName,
                summary.AvgOverallScore.ToString("F1"),
                summary.AvgCorrectAnswerScore.ToString("F1"),
                summary.AvgLogicalStepsScore.ToString("F1"),
                summary.AvgClarityScore.ToString("F1"),
                summary.AvgTokensPerSec.ToString("F1"),
                statusIcon
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Save reasoning results to JSON
    /// </summary>
    public async Task SaveReasoningResultsAsync(List<ReasoningTestResponse> responses, string filePath)
    {
        var json = JsonSerializer.Serialize(responses, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
        AnsiConsole.MarkupLine($"[green]✓ Saved reasoning results → {filePath}[/]");
    }
}
