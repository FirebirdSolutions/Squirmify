using System.Diagnostics;
using System.Text.Json;
using ModelEvaluator.Models;
using ModelEvaluator.Services;
using Spectre.Console;

namespace ModelEvaluator;

class Program
{
    private static int _idCounter = 0;

    static async Task Main(string[] args)
    {
        var sw = Stopwatch.StartNew();

        AnsiConsole.Write(new FigletText("LLM Squirmify")
            .Centered()
            .Color(Color.Cyan1));

        AnsiConsole.WriteLine();

        try
        {
            // Initialize services
            var modelService = new ModelService();
            var seedService = new SeedService();
            var testService = new TestService(modelService);
            var judgingService = new JudgingService(modelService);

            // Ensure output directory exists
            Directory.CreateDirectory(Config.OutputDir);

            // ═══ Score-Only Mode ═══
            if (Config.ScoreOnly)
            {
                await RunScoreOnlyModeAsync(modelService, judgingService);
                return;
            }

            // ═══ Step 1: Load Models ═══
            AnsiConsole.MarkupLine("[bold cyan]═══ Step 1: Loading Models ═══[/]\n");

            var models = await modelService.LoadModelsAsync(null);

            if (!models.Any())
            {
                AnsiConsole.MarkupLine("[red]✗ No models available. Exiting.[/]");
                return;
            }

            // Track qualified models and base judge (set by qualification tests or defaults)
            var qualifiedModels = models;
            string baseJudge = "";
            List<InstructionTestResult> testResults = new();
            List<ReasoningTestSummary> reasoningSummaries = new();

            if (Config.RunQualificationTests)
            {
                // ═══ Step 2: Run Instruction Tests ═══
                AnsiConsole.MarkupLine("[bold cyan]═══ Step 2: Instruction Following Tests ═══[/]\n");
                testResults = await testService.RunInstructionTestsAsync(models);

                if (!testResults.Any())
                {
                    AnsiConsole.MarkupLine("[red]✗ All models failed instruction tests. Exiting.[/]");
                    return;
                }

                // ═══ Step 2.5: Run Reasoning Tests ═══
                AnsiConsole.MarkupLine("[bold cyan]═══ Step 2.5: Reasoning Tests ═══[/]\n");

                var reasoningService = new ReasoningTestService(modelService);

                // Run reasoning tests on models that passed instruction tests
                qualifiedModels = models
                    .Where(m => testResults.Any(t => t.ModelName == m && t.PassRate >= 0.8))
                    .ToList();

                var reasoningResponses = await reasoningService.RunReasoningTestsAsync(qualifiedModels);

                // Score reasoning with a temp judge (use best instruction follower for now)
                var tempJudge = testResults
                    .Where(r => r.PassRate >= 0.8)
                    .OrderByDescending(r => r.PassRate)
                    .First()
                    .ModelName;

                await reasoningService.ScoreReasoningTestsAsync(tempJudge, reasoningResponses);

                // Generate summaries
                reasoningSummaries = reasoningService.GenerateReasoningSummaries(reasoningResponses);
                reasoningService.DisplayReasoningResults(reasoningSummaries);

                // Save results
                var reasoningFile = Path.Combine(Config.OutputDir, "reasoning_results.json");
                await reasoningService.SaveReasoningResultsAsync(reasoningResponses, reasoningFile);

                // ═══ Step 3: Select SMART Base Judge ═══
                if (!string.IsNullOrEmpty(Config.OverrideBaseJudge))
                {
                    baseJudge = Config.OverrideBaseJudge;
                    AnsiConsole.MarkupLine($"\n[bold yellow]⚠ Using override base judge: {baseJudge}[/]\n");
                }
                else
                {
                    baseJudge = testService.SelectBaseJudge(
                        testResults,
                        reasoningSummaries
                    );
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[bold yellow]⚠ Skipping qualification tests (instruction/reasoning)[/]\n");

                // Use override judge if specified, otherwise first model
                if (!string.IsNullOrEmpty(Config.OverrideBaseJudge))
                {
                    baseJudge = Config.OverrideBaseJudge;
                }
                else
                {
                    baseJudge = models.First();
                }
                AnsiConsole.MarkupLine($"[cyan]Using judge: {baseJudge}[/]\n");
            }

            if (Config.RunContextWindowTests)
            {
                // ═══ Step 2.6: Context Window Stress Tests ═══
                AnsiConsole.MarkupLine("[bold cyan]═══ Step 2.6: Context Window Stress Tests ═══[/]\n");

                var contextWindowService = new ContextWindowTestService(modelService);
                var contextResults = await contextWindowService.RunContextWindowTestsAsync(qualifiedModels);

                // Generate and display summaries
                var contextSummaries = contextWindowService.GenerateContextSummaries(contextResults);
                contextWindowService.DisplayContextWindowResults(contextSummaries);

                // Save results
                var contextFile = Path.Combine(Config.OutputDir, "context_window_results.json");
                await contextWindowService.SaveContextWindowResultsAsync(contextResults, contextFile);
            }

            if (Config.RunConversationTests)
            {

                // ═══ Step 2.7: Conversation Tests ═══
                AnsiConsole.MarkupLine("[bold cyan]═══ Step 2.7: Conversation Tests ═══[/]\n");

                var conversationService = new ConversationTestService(modelService);
                var conversationResults = await conversationService.RunConversationTestsAsync(
                    models.Where(m => testResults.Any(t => t.ModelName == m && t.PassRate >= 0.8)).ToList()
                );

                // Score conversations with base judge
                await conversationService.ScoreConversationsAsync(baseJudge, conversationResults);

                // Generate and display summaries
                var conversationSummaries = conversationService.GenerateConversationSummaries(conversationResults);
                conversationService.DisplayConversationResults(conversationSummaries);

                // Save results
                var conversationFile = Path.Combine(Config.OutputDir, "conversation_results.json");
                await conversationService.SaveConversationResultsAsync(conversationResults, conversationFile);

            }

            if (Config.RunPromptTests)
            {
                // ═══ Step 4: Load/Generate Seeds ═══
                AnsiConsole.MarkupLine("[bold cyan]═══ Step 3: Loading & Generating Seeds ═══[/]\n");

                SeedsConfig? seedsConfig = Config.OverwriteSeeds
                    ? null
                    : await seedService.LoadSeedsAsync(Config.GeneratedSeedsFile);

                if (seedsConfig == null)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ No existing seeds found. Generating new seeds...[/]");

                    var baseSeeds = await seedService.LoadBaseSeedsAsync(Config.BaseSeedsFile);

                    if (!baseSeeds.Any())
                    {
                        AnsiConsole.MarkupLine("[red]✗ No base seeds found. Exiting.[/]");
                        return;
                    }

                    seedsConfig = await seedService.GenerateAugmentedSeedsAsync(baseSeeds, Config.TargetSeedCount, Config.CategoryWeights);

                    await seedService.SaveSeedsAsync(seedsConfig, Config.GeneratedSeedsFile);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[green]✓ Loaded {seedsConfig.seeds.Count} seeds from existing file[/]");
                }

                // Filter out flagged models
                var activeModels = models.Where(m => !modelService.IsModelFlagged(m)).ToList();

                if (!activeModels.Any())
                {
                    AnsiConsole.MarkupLine("[red]✗ No active models remaining. Exiting.[/]");
                    return;
                }

                // ═══ Step 5: Run All Models Through Prompts ═══
                AnsiConsole.MarkupLine($"\n[bold cyan]═══ Step 4: Running {activeModels.Count} Models Through {seedsConfig.seeds.Count} Prompts ═══[/]\n");

                var allResults = await RunGenerationPipelineAsync(modelService, seedsConfig, activeModels);

                // ═══ Step 6: Score with Base Judge ═══
                var resultsFile = Path.Combine(Config.OutputDir, "all_results.json");
                await judgingService.ScoreResultsAsync(baseJudge, allResults, resultsFile);

                // ═══ Step 7: Select AutoJudges ═══
                List<string> autoJudges;
                if (Config.OverrideAutoJudges.Any())
                {
                    autoJudges = Config.OverrideAutoJudges;
                    AnsiConsole.MarkupLine($"\n[bold yellow]⚠ Using {autoJudges.Count} override auto judges[/]");
                    foreach (var judge in autoJudges)
                    {
                        AnsiConsole.MarkupLine($"  → {judge}");
                    }
                    AnsiConsole.WriteLine();
                }
                else
                {
                    autoJudges = judgingService.SelectAutoJudges(allResults, activeModels);
                }

                // ═══ Step 8: Re-score with AutoJudges ═══
                if (autoJudges.Any())
                {
                    var finalResultsFile = Path.Combine(Config.OutputDir, "final_results.json");
                    await judgingService.AutoJudgeResultsAsync(autoJudges, allResults, finalResultsFile);

                    // Validate judge selection with peer scores
                    judgingService.ValidateJudgeSelection(autoJudges, allResults);

                }

                // ═══ Step 9: Generate Reports ═══
                AnsiConsole.MarkupLine("\n[bold cyan]═══ Step 5: Generating Reports ═══[/]\n");

                GenerateFinalReport(allResults, activeModels);

                var highQualityFile = Path.Combine(Config.OutputDir, "high_quality_dataset.jsonl");
                await judgingService.ExtractHighQualityDatasetAsync(allResults, highQualityFile);

                sw.Stop();
                AnsiConsole.MarkupLine($"\n[bold green]✓ Pipeline finished in {sw.Elapsed.TotalSeconds:F1}s[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }

        AnsiConsole.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    /// <summary>
    /// Score-only mode: Load existing results and run judging without generation
    /// </summary>
    private static async Task RunScoreOnlyModeAsync(ModelService modelService, JudgingService judgingService)
    {
        AnsiConsole.MarkupLine("[bold magenta]═══ SCORE-ONLY MODE ═══[/]\n");

        // Validate configuration
        if (string.IsNullOrEmpty(Config.ScoreOnlyInputFile))
        {
            AnsiConsole.MarkupLine("[red]✗ scoreOnlyInputFile is required in score-only mode[/]");
            return;
        }

        if (string.IsNullOrEmpty(Config.OverrideBaseJudge))
        {
            AnsiConsole.MarkupLine("[red]✗ overrideBaseJudge is required in score-only mode[/]");
            return;
        }

        var inputPath = Path.IsPathRooted(Config.ScoreOnlyInputFile)
            ? Config.ScoreOnlyInputFile
            : Path.Combine(Config.OutputDir, Config.ScoreOnlyInputFile);

        if (!File.Exists(inputPath))
        {
            AnsiConsole.MarkupLine($"[red]✗ Results file not found: {inputPath}[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[cyan]Loading results from:[/] {inputPath}");
        AnsiConsole.MarkupLine($"[cyan]Base judge:[/] {Config.OverrideBaseJudge}");

        // Load existing results
        var json = await File.ReadAllTextAsync(inputPath);
        var allResults = JsonSerializer.Deserialize<List<GenerationResult>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (allResults == null || !allResults.Any())
        {
            AnsiConsole.MarkupLine("[red]✗ No results found in file[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[green]✓ Loaded {allResults.Count} results[/]\n");

        // Clear existing ratings if re-scoring
        foreach (var result in allResults)
        {
            result.ratings.Clear();
        }

        // Get unique models from results
        var modelsInResults = allResults.Select(r => r.generator).Distinct().ToList();

        // Score with base judge
        var resultsFile = Path.Combine(Config.OutputDir, "all_results_rescored.json");
        await judgingService.ScoreResultsAsync(Config.OverrideBaseJudge, allResults, resultsFile);

        // Select or use override AutoJudges
        List<string> autoJudges;
        if (Config.OverrideAutoJudges.Any())
        {
            autoJudges = Config.OverrideAutoJudges;
            AnsiConsole.MarkupLine($"\n[bold yellow]⚠ Using {autoJudges.Count} override auto judges[/]");
            foreach (var judge in autoJudges)
            {
                AnsiConsole.MarkupLine($"  → {judge}");
            }
            AnsiConsole.WriteLine();
        }
        else
        {
            autoJudges = judgingService.SelectAutoJudges(allResults, modelsInResults);
        }

        // Re-score with AutoJudges
        if (autoJudges.Any())
        {
            var finalResultsFile = Path.Combine(Config.OutputDir, "final_results_rescored.json");
            await judgingService.AutoJudgeResultsAsync(autoJudges, allResults, finalResultsFile);
            judgingService.ValidateJudgeSelection(autoJudges, allResults);
        }

        // Generate Reports
        AnsiConsole.MarkupLine("\n[bold cyan]═══ Generating Reports ═══[/]\n");
        GenerateFinalReport(allResults, modelsInResults);

        var highQualityFile = Path.Combine(Config.OutputDir, "high_quality_dataset_rescored.jsonl");
        await judgingService.ExtractHighQualityDatasetAsync(allResults, highQualityFile);

        AnsiConsole.MarkupLine("\n[bold green]✓ Score-only mode completed[/]");
    }

    private static async Task<List<GenerationResult>> RunGenerationPipelineAsync(
        ModelService modelService,
        SeedsConfig config,
        List<string> models)
    {
        var results = new List<GenerationResult>();
        var totalPrompts = models.Count * config.seeds.Count;

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Generating responses[/]", maxValue: totalPrompts);

                foreach (var model in models)
                {
                    // Warm up model
                    task.Description = $"[yellow]Warming up {model}[/]";
                    await modelService.WarmUpModelAsync(model);

                    foreach (var seed in config.seeds)
                    {
                        task.Description = $"[yellow]{model} → {seed.prompt[..Math.Min(40, seed.prompt.Length)]}...[/]";

                        var systemPrompt = config.system_prompts.TryGetValue(seed.category, out var sysPrompt)
                            ? sysPrompt
                            : config.system_prompt;

                        var response = await modelService.CompletionAsync(
                            model,
                            systemPrompt,
                            seed.prompt,
                            seed.temperature ?? config.temperature,
                            seed.top_p ?? config.top_p,
                            seed.max_tokens ?? config.max_tokens
                        );

                        if (response != null)
                        {
                            var (responseText, perf) = response.Value;

                            results.Add(new GenerationResult
                            {
                                id = Interlocked.Increment(ref _idCounter),
                                seed = seed.prompt,
                                category = seed.category,
                                generator = model,
                                response = responseText,
                                temperature = seed.temperature ?? config.temperature,
                                top_p = seed.top_p ?? config.top_p,
                                max_tokens = seed.max_tokens ?? config.max_tokens,
                                perf = perf
                            });
                        }

                        task.Increment(1);
                    }
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ Generated {results.Count} responses[/]\n");
        return results;
    }

    private static void GenerateFinalReport(List<GenerationResult> results, List<string> models)
    {
        // Model summary table
        var modelSummaries = models.Select(model =>
        {
            var modelResults = results.Where(r => r.generator == model).ToList();
            if (!modelResults.Any()) return null;

            var categoryScores = modelResults
                .GroupBy(r => r.category)
                .ToDictionary(g => g.Key, g => g.Average(r => r.avg_score));

            var bestCategory = categoryScores.Any()
                ? categoryScores.OrderByDescending(kvp => kvp.Value).First().Key
                : "N/A";

            return new ModelSummary
            {
                ModelName = model,
                AvgScore = modelResults.Average(r => r.avg_score),
                AvgTokensPerSec = modelResults.Average(r => r.perf.tokens_per_sec ?? 0),
                AvgLatencyMs = modelResults.Average(r => r.perf.total_ms),
                HighQualityCount = modelResults.Count(r => r.high_quality),
                BestCategory = bestCategory
            };
        })
        .Where(s => s != null)
        .OrderByDescending(s => s!.AvgScore)
        .ThenByDescending(s => s!.AvgTokensPerSec)
        .ToList()!;

        var modelTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Model Performance Summary[/]")
            .AddColumn(new TableColumn("[bold]Model[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Avg Score[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Avg t/s[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]HQ[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Best Cat[/]").Centered());

        foreach (var summary in modelSummaries)
        {
            modelTable.AddRow(
                summary.ModelName,
                summary.AvgScore.ToString("F1"),
                summary.AvgTokensPerSec.ToString("F1"),
                summary.HighQualityCount.ToString(),
                summary.BestCategory
            );
        }

        AnsiConsole.Write(modelTable);
        AnsiConsole.WriteLine();

        // Category summary table
        var categories = results.Select(r => r.category).Distinct().ToList();
        var categoryTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Category Performance[/]")
            .AddColumn(new TableColumn("[bold]Domain[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Avg Score[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Count[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]HQ %[/]").RightAligned());

        foreach (var category in categories)
        {
            var categoryResults = results.Where(r => r.category == category).ToList();
            var hqPercent = categoryResults.Any()
                ? (double)categoryResults.Count(r => r.high_quality) / categoryResults.Count
                : 0;

            categoryTable.AddRow(
                category,
                categoryResults.Average(r => r.avg_score).ToString("F2"),
                categoryResults.Count.ToString(),
                hqPercent.ToString("P1")
            );
        }

        AnsiConsole.Write(categoryTable);
        AnsiConsole.WriteLine();
    }
}
