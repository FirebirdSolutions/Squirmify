using System.Text;
using System.Text.Json;
using ModelEvaluator.Models;
using Spectre.Console;
using SharpToken;

namespace ModelEvaluator.Services;

/// <summary>
/// Represents a checkpoint in the context for verifying model memory
/// </summary>
public class ContextCheckpoint
{
    public int TargetTokenPosition { get; set; }
    public string SecretWord { get; set; } = "";
    public string CarrierSentence { get; set; } = "";
}

/// <summary>
/// Configuration for a context window stress test
/// </summary>
public class ContextWindowTest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ContextCheckpoint> Checkpoints { get; set; } = new();
    public string FillerType { get; set; } = "mixed";
    public int TargetTokens { get; set; }
    public string? BuriedInstruction { get; set; }
}

public class CheckpointVerdict
{
    public string SecretWord { get; set; } = "";
    public int Position { get; set; }
    public bool Correct { get; set; }
    public string ModelSaid { get; set; } = "";
    public string FailureType { get; set; } = ""; // forgot | hallucinated | confused | partial
}

public class ContextProbeResult
{
    public int ApproximateTokens { get; set; }
    public List<CheckpointVerdict> Verdicts { get; set; } = new();
    public bool FollowedBuriedInstruction { get; set; }
    public string? DeathQuote { get; set; }
}

public class ContextWindowTestResult
{
    public string ModelName { get; set; } = "";
    public string TestName { get; set; } = "";
    public List<ContextProbeResult> Probes { get; set; } = new();
    public int MaxReliableTokens { get; set; }
    public int FirstHallucinationAt { get; set; } = -1;
    public double CheckpointAccuracy { get; set; }
    public PerfMetrics AggregatePerf { get; set; } = new();
    public string? Autopsy { get; set; }
}

public class ContextWindowSummary
{
    public string ModelName { get; set; } = "";
    public int AvgMaxReliableTokens { get; set; }
    public int AvgFirstHallucinationAt { get; set; }
    public double AvgCheckpointAccuracy { get; set; }
    public Dictionary<string, int> TestSpecificReliability { get; set; } = new();
    public string DegradationPattern { get; set; } = "";
}

/// <summary>
/// Service for stress-testing model context windows and exposing real vs claimed capabilities
/// </summary>
public class ContextWindowTestService
{
    private readonly ModelService _modelService;
    private readonly GptEncoding _tikToken;
    private ContextWindowTestsConfig? _config;
    private SystemPromptsConfig? _systemPrompts;
    private string[] _codeSnippets = Array.Empty<string>();
    private string[] _proseSnippets = Array.Empty<string>();
    private string[] _technicalSnippets = Array.Empty<string>();
    private string[] _checkpointTemplates = Array.Empty<string>();

    public ContextWindowTestService(ModelService modelService)
    {
        _modelService = modelService;
        _tikToken = GptEncoding.GetEncoding("cl100k_base");
    }

    /// <summary>
    /// Load configuration from external file
    /// </summary>
    private async Task EnsureConfigLoadedAsync()
    {
        if (_config != null) return;

        _config = await ConfigLoader.LoadContextWindowTestsAsync();
        _systemPrompts = await ConfigLoader.LoadSystemPromptsAsync();

        // Load filler content
        _codeSnippets = _config.FillerContent.Code.Count > 0
            ? _config.FillerContent.Code.ToArray()
            : GetDefaultCodeSnippets();

        _proseSnippets = _config.FillerContent.Prose.Count > 0
            ? _config.FillerContent.Prose.ToArray()
            : GetDefaultProseSnippets();

        _technicalSnippets = _config.FillerContent.Technical.Count > 0
            ? _config.FillerContent.Technical.ToArray()
            : GetDefaultTechnicalSnippets();

        _checkpointTemplates = _config.CheckpointTemplates.Count > 0
            ? _config.CheckpointTemplates.ToArray()
            : GetDefaultCheckpointTemplates();
    }

    private static string[] GetDefaultCodeSnippets() => new[]
    {
        "public class DataProcessor { private readonly ILogger _logger; public async Task<Result> ProcessAsync(Data input) { try { var validated = await ValidateAsync(input); return await TransformAsync(validated); } catch (Exception ex) { _logger.LogError(ex, \"Processing failed\"); throw; } } }",
        "function calculateMetrics(data) { const sum = data.reduce((a, b) => a + b, 0); const avg = sum / data.length; const variance = data.map(x => Math.pow(x - avg, 2)).reduce((a, b) => a + b) / data.length; return { sum, avg, variance, stdDev: Math.sqrt(variance) }; }",
        "def train_model(X, y, epochs=100, learning_rate=0.01): model = NeuralNetwork(layers=[128, 64, 32, 10]) optimizer = Adam(lr=learning_rate) for epoch in range(epochs): predictions = model.forward(X); loss = cross_entropy(predictions, y); gradients = model.backward(loss); optimizer.step(gradients); if epoch % 10 == 0: print(f'Epoch {epoch}, Loss: {loss:.4f}'); return model",
        "SELECT u.name, COUNT(o.id) as order_count, SUM(o.total) as revenue FROM users u LEFT JOIN orders o ON u.id = o.user_id WHERE o.created_at >= DATE_SUB(NOW(), INTERVAL 30 DAY) GROUP BY u.id HAVING order_count > 5 ORDER BY revenue DESC LIMIT 100;",
    };

    private static string[] GetDefaultProseSnippets() => new[]
    {
        "The morning sun cast long shadows across the empty street. A solitary figure emerged from the corner cafe, coffee in hand, lost in thought. The city was just beginning to wake, the distant hum of traffic growing steadily louder. Somewhere a dog barked, and the spell was broken.",
        "In the depths of winter, when the frost painted intricate patterns on every window, the old house stood silent. Its inhabitants had long since departed, leaving only memories etched into the very walls. The floorboards creaked with phantom footsteps, and the wind whistled through cracks like whispered secrets.",
        "Technology advances at a relentless pace, each innovation building upon the last. What seemed impossible yesterday becomes commonplace tomorrow. Yet with each leap forward, we must pause to consider the implications. Progress without wisdom is merely motion without direction.",
        "The mountain peak rose above the clouds, a silent sentinel watching over the valley below. Climbers spoke of it with reverence, their voices hushed as if in a cathedral. To reach its summit was to touch the sky itself, to stand at the edge of the world and gaze into infinity.",
    };

    private static string[] GetDefaultTechnicalSnippets() => new[]
    {
        "The TCP three-way handshake establishes a connection through SYN, SYN-ACK, and ACK packets. This process ensures both parties agree on initial sequence numbers and are ready to exchange data. Flow control is managed through sliding window protocols, while congestion control algorithms like Reno and Cubic prevent network saturation.",
        "In distributed systems, the CAP theorem states that a system can provide at most two of three guarantees: Consistency, Availability, and Partition tolerance. Most modern systems choose AP or CP configurations based on use case requirements. Eventual consistency models provide weaker guarantees but better performance.",
        "Machine learning models require careful feature engineering and preprocessing. Normalization scales features to similar ranges, preventing dominance by large values. One-hot encoding transforms categorical variables into binary vectors. Cross-validation splits data into training and test sets to prevent overfitting and ensure generalization.",
        "Cryptographic hash functions are one-way transformations that produce fixed-size outputs from arbitrary inputs. SHA-256 generates 256-bit hashes used extensively in blockchain and digital signatures. Collision resistance ensures different inputs produce different outputs. Preimage resistance prevents reverse engineering the original input.",
    };

    private static string[] GetDefaultCheckpointTemplates() => new[]
    {
        "The authentication token for phase {0} is {1}.",
        "Project internal codename: {1} - do not disclose.",
        "Temporary access code {1} expires in 24 hours.",
        "Debug constant set to {1} during testing.",
        "The secret phrase required is: {1}"
    };


    /// <summary>
    /// Generates context window stress test scenarios with different patterns and sizes
    /// Applies level multiplier and max test limits from config
    /// </summary>
    /// <returns>List of test configurations</returns>
    public async Task<List<ContextWindowTest>> GenerateTestsAsync()
    {
        await EnsureConfigLoadedAsync();

        var tests = new List<ContextWindowTest>();
        var multiplier = _config!.GetTokenMultiplier();
        var maxTests = Config.ContextWindowTestMaxTests;
        var level = Config.ContextWindowTestLevel;

        AnsiConsole.MarkupLine($"[dim]Context window level: {level} (token multiplier: {multiplier:F1}x)[/]");

        var testCount = 0;
        foreach (var testDef in _config!.Tests)
        {
            if (testCount >= maxTests) break;

            // Apply level multiplier to target tokens
            var baseTokens = testDef.GetEffectiveTargetTokens();
            var scaledTokens = (int)(baseTokens * multiplier);

            var test = new ContextWindowTest
            {
                Name = testDef.Name,
                Description = testDef.Description,
                FillerType = testDef.FillerType,
                TargetTokens = scaledTokens,
                BuriedInstruction = testDef.BuriedInstruction
            };

            // Use predefined checkpoints if provided, otherwise generate
            if (testDef.Checkpoints?.Count > 0)
            {
                test.Checkpoints = testDef.Checkpoints.Select(c => new ContextCheckpoint
                {
                    // Support relative position (0.0-1.0) or absolute
                    TargetTokenPosition = c.RelativePosition.HasValue
                        ? (int)(c.RelativePosition.Value * scaledTokens)
                        : (int)(c.TargetTokenPosition * multiplier),
                    SecretWord = c.SecretWord,
                    CarrierSentence = c.CarrierSentence
                }).ToList();
            }
            else if (testDef.GetEffectiveCheckpointCount().HasValue)
            {
                // Scale checkpoint count with multiplier (fewer checkpoints for smaller tests)
                var checkpointCount = Math.Max(1, (int)(testDef.GetEffectiveCheckpointCount()!.Value * multiplier));
                test.Checkpoints = GenerateStealthCheckpoints(checkpointCount, scaledTokens);
            }

            tests.Add(test);
            testCount++;
        }

        AnsiConsole.MarkupLine($"[dim]Selected {tests.Count} of {_config.Tests.Count} context window tests (max: {maxTests})[/]");

        return tests;
    }

    /// <summary>
    /// Synchronous version for backwards compatibility
    /// </summary>
    public List<ContextWindowTest> GenerateTests()
    {
        return GenerateTestsAsync().GetAwaiter().GetResult();
    }

    // ────────────────────────────── Helper Generators ──────────────────────────────
    private List<ContextCheckpoint> GenerateStealthCheckpoints(int count, int maxTokens)
    {
        var rand = new Random(42);
        var list = new List<ContextCheckpoint>();
        var templates = _checkpointTemplates.Length > 0 ? _checkpointTemplates : GetDefaultCheckpointTemplates();

        for (int i = 0; i < count; i++)
        {
            var word = $"NEEDLE_{rand.Next(1000, 9999)}_{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            var pos = (i + 1) * (maxTokens / (count + 2));
            var sentence = string.Format(templates[rand.Next(templates.Length)], i + 1, word);

            list.Add(new ContextCheckpoint
            {
                TargetTokenPosition = pos,
                SecretWord = word,
                CarrierSentence = sentence
            });
        }
        return list;
    }

    /// <summary>
    /// Runs context window stress tests on the specified models
    /// </summary>
    /// <param name="models">List of model names to test</param>
    /// <returns>Complete test results including degradation analysis</returns>
    public async Task<List<ContextWindowTestResult>> RunContextWindowTestsAsync(List<string> models)
    {
        AnsiConsole.MarkupLine("\n[bold cyan]═══ Running Context Window Autopsy Suite ═══[/]\n");

        // Load tests from config
        var tests = await GenerateTestsAsync();
        var results = new List<ContextWindowTestResult>();
        var total = models.Count * tests.Count;

        await AnsiConsole.Progress()
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Stress-testing context windows[/]", maxValue: total);

                foreach (var model in models)
                {
                    var warmupOk = await _modelService.WarmUpModelAsync(model);
                    if (!warmupOk) { AnsiConsole.MarkupLine($"[red]✗ Warmup failed: {model}[/]"); task.Increment(tests.Count); continue; }

                    foreach (var test in tests)
                    {
                        task.Description = $"[yellow]{model} → {test.Name}[/]";
                        var result = await RunSingleContextTestAsync(model, test);
                        if (result != null)
                        {
                            GenerateAutopsy(result);
                            results.Add(result);
                        }
                        task.Increment(1);
                    }
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ Completed {results.Count} context tests[/]\n");
        return results;
    }

    // ────────────────────────────── 3. Run One Test ──────────────────────────────
    private async Task<ContextWindowTestResult?> RunSingleContextTestAsync(string modelName, ContextWindowTest test)
    {
        var result = new ContextWindowTestResult { ModelName = modelName, TestName = test.Name };
        var fullContext = BuildContextDocument(test);
        var probePoints = new[] { test.TargetTokens / 4, test.TargetTokens / 2, test.TargetTokens * 3 / 4, test.TargetTokens };
        var perfList = new List<PerfMetrics>();
        var lastGood = 0;

        foreach (var tokens in probePoints)
        {
            var context = TruncateToTokens(fullContext, tokens);
            var probe = new ContextProbeResult { ApproximateTokens = tokens };

            foreach (var cp in test.Checkpoints.Where(c => c.TargetTokenPosition <= tokens))
            {
                var (resp, perf) = await QuerySecret(modelName, context, cp.SecretWord);
                perfList.Add(perf);

                var verdict = new CheckpointVerdict
                {
                    SecretWord = cp.SecretWord,
                    Position = cp.TargetTokenPosition,
                    ModelSaid = resp.Trim(),
                    Correct = resp.Trim().Contains(cp.SecretWord, StringComparison.OrdinalIgnoreCase)
                };

                if (!verdict.Correct)
                {
                    if (resp.Length > 5 && !resp.ToLower().Contains("don't remember") && !resp.ToLower().Contains("can't"))
                        verdict.FailureType = "hallucinated";
                    else if (resp.ToLower().Contains("remember") || resp.ToLower().Contains("sure"))
                        verdict.FailureType = "confused";
                    else
                        verdict.FailureType = "forgot";
                }

                probe.Verdicts.Add(verdict);
            }

            // Buried instruction check
            if (test.BuriedInstruction != null)
            {
                var free = await FreeResponse(modelName, context);
                probe.FollowedBuriedInstruction = free?.ToLower().Contains("arr") == true || free?.ToLower().Contains("matey") == true;
            }

            if (probe.Verdicts.All(v => v.Correct) && (test.BuriedInstruction == null || probe.FollowedBuriedInstruction))
                lastGood = tokens;

            result.Probes.Add(probe);
        }

        result.MaxReliableTokens = lastGood;
        result.CheckpointAccuracy = result.Probes.SelectMany(p => p.Verdicts).Average(v => v.Correct ? 1.0 : 0.0);
        if (perfList.Any())
        {
            result.AggregatePerf = new PerfMetrics
            {
                tokens_per_sec = perfList.Average(p => p.tokens_per_sec ?? 0),
                total_ms = perfList.Sum(p => p.total_ms)
            };
        }

        return result;
    }

    // ────────────────────────────── Helper Methods ──────────────────────────────
    private string BuildContextDocument(ContextWindowTest test)
    {
        var sb = new StringBuilder();
        int tokens = 0;
        int cpIdx = 0;

        if (test.BuriedInstruction != null)
        {
            while (tokens < test.TargetTokens / 3) tokens += AppendFiller(sb, test.FillerType);
            sb.AppendLine($"CRITICAL DIRECTIVE: {test.BuriedInstruction}");
            sb.AppendLine();
        }

        while (tokens < test.TargetTokens)
        {
            while (cpIdx < test.Checkpoints.Count && tokens >= test.Checkpoints[cpIdx].TargetTokenPosition - 1000)
            {
                sb.AppendLine(test.Checkpoints[cpIdx].CarrierSentence);
                sb.AppendLine();
                tokens += _tikToken.Encode(test.Checkpoints[cpIdx].CarrierSentence).Count + 10;
                cpIdx++;
            }
            tokens += AppendFiller(sb, test.FillerType);
        }
        return sb.ToString();
    }

    private int AppendFiller(StringBuilder sb, string type)
    {
        var codeSnippets = _codeSnippets.Length > 0 ? _codeSnippets : GetDefaultCodeSnippets();
        var proseSnippets = _proseSnippets.Length > 0 ? _proseSnippets : GetDefaultProseSnippets();
        var technicalSnippets = _technicalSnippets.Length > 0 ? _technicalSnippets : GetDefaultTechnicalSnippets();

        var filler = type switch
        {
            "code" => codeSnippets[Random.Shared.Next(codeSnippets.Length)],
            "prose" => proseSnippets[Random.Shared.Next(proseSnippets.Length)],
            "technical" => technicalSnippets[Random.Shared.Next(technicalSnippets.Length)],
            _ => Random.Shared.Next(3) switch { 0 => codeSnippets[Random.Shared.Next(codeSnippets.Length)], 1 => proseSnippets[Random.Shared.Next(proseSnippets.Length)], _ => technicalSnippets[Random.Shared.Next(technicalSnippets.Length)] }
        };
        sb.AppendLine(filler); sb.AppendLine();
        return _tikToken.Encode(filler).Count + 10;
    }

    private async Task<(string response, PerfMetrics perf)> QuerySecret(string model, string context, string secret)
    {
        var prompt = $"{context}\n\nWhat is the secret code starting with \"{secret.Substring(0, Math.Min(12, secret.Length))}\"? Reply ONLY with the full code.";
        var recallPrompt = _systemPrompts?.GetTestPrompt("contextWindowRecall", "You have perfect recall.") ?? "You have perfect recall.";
        var r = await _modelService.CompletionAsync(model, recallPrompt, prompt, 0.0, 0.9, 64);
        return r ?? ("<error>", new PerfMetrics());
    }

    private async Task<string?> FreeResponse(string model, string context)
    {
        var assistantPrompt = _systemPrompts?.GetTestPrompt("contextWindowTest", "You are a helpful assistant.") ?? "You are a helpful assistant.";
        var r = await _modelService.CompletionAsync(model, assistantPrompt, $"{context}\n\nSummarise what you read.", 0.0, 0.9, 256);
        return r?.response;
    }

    private string TruncateToTokens(string text, int maxTokens)
    {
        var ids = _tikToken.Encode(text);
        if (ids.Count <= maxTokens) return text;
        return _tikToken.Decode(ids.Take(maxTokens).ToList());
    }

    // ────────────────────────────── 4. Autopsy Generation ──────────────────────────────
    private void GenerateAutopsy(ContextWindowTestResult result)
    {
        var worst = result.Probes.OrderBy(p => p.Verdicts.Count(v => v.Correct)).First();
        var sb = new StringBuilder();

        sb.AppendLine($"[bold red]☠ AUTOPSY: {result.ModelName} — {result.TestName}[/]");
        sb.AppendLine($"[red]Died at ~{worst.ApproximateTokens:N0} tokens ({worst.Verdicts.Count(v => v.Correct)}/{worst.Verdicts.Count} correct)[/]");

        var hallucinations = worst.Verdicts.Where(v => v.FailureType == "hallucinated").Take(6);
        if (hallucinations.Any())
        {
            sb.AppendLine("\n[bold magenta]CONFIDENT HALLUCINATIONS[/]");
            foreach (var h in hallucinations)
                sb.AppendLine($"  • Expected [yellow]{h.SecretWord}[/] → Invented [cyan]\"{h.ModelSaid}\"[/]");
        }

        var forgotten = worst.Verdicts.Where(v => v.FailureType is "forgot" or "confused").Take(5);
        if (forgotten.Any())
        {
            sb.AppendLine("\n[bold orange1]GONE FOREVER[/]");
            foreach (var f in forgotten)
                sb.AppendLine($"  • [strikethrough]{f.SecretWord}[/] (~{f.Position:N0} tokens)");
        }

        if (result.TestName.Contains("Buried") && !worst.FollowedBuriedInstruction)
            sb.AppendLine("\n[bold red]☠ Forgot it was a pirate. Total identity collapse.[/]");

        result.Autopsy = sb.ToString();
    }

    /// <summary>
    /// Generates performance summaries from context window test results
    /// </summary>
    /// <param name="results">Raw test results</param>
    /// <returns>Aggregated summaries per model</returns>
    public List<ContextWindowSummary> GenerateContextSummaries(List<ContextWindowTestResult> results)
    {
        return results.GroupBy(r => r.ModelName).Select(g =>
        {
            var rel = g.ToDictionary(r => r.TestName, r => r.MaxReliableTokens);
            var avgRel = rel.Values.Average();
            var pattern = avgRel > Config.ContextWindowDegradationThreshold_Graceful ? "graceful"
                : avgRel > Config.ContextWindowDegradationThreshold_Moderate ? "moderate"
                : avgRel > Config.ContextWindowDegradationThreshold_Sudden ? "sudden"
                : "catastrophic";

            return new ContextWindowSummary
            {
                ModelName = g.Key,
                AvgMaxReliableTokens = (int)avgRel,
                AvgFirstHallucinationAt = (int)g.Average(r => r.FirstHallucinationAt > 0 ? r.FirstHallucinationAt : r.MaxReliableTokens),
                AvgCheckpointAccuracy = g.Average(r => r.CheckpointAccuracy),
                TestSpecificReliability = rel,
                DegradationPattern = pattern
            };
        })
        .OrderByDescending(s => s.AvgMaxReliableTokens)
        .ToList();
    }

    public void DisplayContextWindowResults(List<ContextWindowSummary> summaries)
    {
        AnsiConsole.MarkupLine("\n[bold cyan]═══ Context Window Summary ═══[/]\n");

        var table = new Table().RoundedBorder()
            .AddColumn("Model")
            .AddColumn("Reliable", c => c.RightAligned())
            .AddColumn("Degradation")
            .AddColumn("Accuracy", c => c.RightAligned());

        foreach (var s in summaries)
        {
            var color = s.DegradationPattern == "graceful" ? "green" : s.DegradationPattern == "moderate" ? "yellow" : "red";
            table.AddRow(s.ModelName, $"{s.AvgMaxReliableTokens:N0}", $"[{color}]{s.DegradationPattern}[/]", $"{s.AvgCheckpointAccuracy:P1}");
        }
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    // ────────────────────────────── 6. Save Results ──────────────────────────────
    public async Task SaveContextWindowResultsAsync(List<ContextWindowTestResult> results, string filePath)
    {
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
        AnsiConsole.MarkupLine($"[green]✓ Context results saved → {filePath}[/]");
    }
}
