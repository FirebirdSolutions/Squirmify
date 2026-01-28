using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Squirmify.Console;
using Squirmify.Core.DTOs;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;
using Squirmify.Services;

// Build host with DI - use shared database in solution root
var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var dbFolder = Path.Combine(solutionRoot, "data");
Directory.CreateDirectory(dbFolder);
var dbPath = Path.Combine(dbFolder, "squirmify.db");
var connectionString = $"Data Source={dbPath}";
Console.WriteLine($"[Database] {dbPath}");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSquirmifyServices(connectionString);
    })
    .Build();

// Initialize database
await host.Services.InitializeDatabaseAsync();

// Handle CLI commands
if (args.Length > 0)
{
    await HandleCliCommand(args, host);
    return;
}

AnsiConsole.Write(new FigletText("Squirmify").Color(Color.Blue));
AnsiConsole.MarkupLine("[dim]LLM Benchmarking Platform[/]");
AnsiConsole.WriteLine();

// Get services
var providerRepo = host.Services.GetRequiredService<IProviderRepository>();
var configRepo = host.Services.GetRequiredService<IConfigRepository>();
var orchestrator = host.Services.GetRequiredService<IBenchmarkOrchestrator>();

// Subscribe to progress updates
orchestrator.OnProgressUpdate += progress =>
{
    // Progress is shown via Spectre.Console status
};

orchestrator.OnLogEvent += logEvent =>
{
    var color = logEvent.Level switch
    {
        "error" => "red",
        "warning" => "yellow",
        _ => "dim"
    };
    AnsiConsole.MarkupLine($"[{color}]{logEvent.Message}[/]");
};

// Main menu
while (true)
{
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("What would you like to do?")
            .AddChoices(new[]
            {
                "Start New Benchmark",
                "Manage Providers",
                "Manage Configurations",
                "View Results",
                "Migrate JSON Tests to Database",
                "Exit"
            }));

    switch (choice)
    {
        case "Start New Benchmark":
            await StartBenchmarkAsync();
            break;
        case "Manage Providers":
            await ManageProvidersAsync();
            break;
        case "Manage Configurations":
            await ManageConfigsAsync();
            break;
        case "View Results":
            await ViewResultsAsync();
            break;
        case "Migrate JSON Tests to Database":
            await MigrateTestsAsync();
            break;
        case "Exit":
            return;
    }
}

async Task StartBenchmarkAsync()
{
    // Get providers
    var providers = (await providerRepo.GetActiveAsync()).ToList();
    if (!providers.Any())
    {
        AnsiConsole.MarkupLine("[yellow]No providers configured. Add a provider first.[/]");
        return;
    }

    // Get configs
    var configs = (await configRepo.GetAllConfigsAsync()).ToList();
    if (!configs.Any())
    {
        AnsiConsole.MarkupLine("[yellow]No configurations found. Creating default...[/]");
        var defaultConfig = new TestSuiteConfig
        {
            Name = "Default",
            Description = "Default test configuration",
            CreatedAt = DateTime.UtcNow
        };
        await configRepo.CreateConfigAsync(defaultConfig);
        configs = (await configRepo.GetAllConfigsAsync()).ToList();
    }

    // Select provider
    var providerChoice = AnsiConsole.Prompt(
        new SelectionPrompt<Provider>()
            .Title("Select provider:")
            .UseConverter(p => $"{p.Name} ({p.BaseUrl})")
            .AddChoices(providers));

    // Select config
    var configChoice = AnsiConsole.Prompt(
        new SelectionPrompt<TestSuiteConfig>()
            .Title("Select configuration:")
            .UseConverter(c => $"{c.Name} - {c.Description ?? "No description"}")
            .AddChoices(configs));

    // Confirm
    if (!AnsiConsole.Confirm($"Start benchmark with [green]{configChoice.Name}[/] on [blue]{providerChoice.Name}[/]?"))
    {
        return;
    }

    // Run benchmark with progress display
    await AnsiConsole.Progress()
        .AutoClear(false)
        .Columns(new ProgressColumn[]
        {
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new PercentageColumn(),
            new SpinnerColumn()
        })
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("[green]Running benchmark...[/]");

            orchestrator.OnProgressUpdate += progress =>
            {
                task.Description = $"[green]{progress.Stage}[/] {progress.CurrentModel ?? ""}";
                task.Value = progress.PercentComplete;
            };

            try
            {
                var runId = await orchestrator.StartRunAsync(configChoice.Id, providerChoice.Id);
                task.Value = 100;
                AnsiConsole.MarkupLine($"[green]✓ Benchmark complete! Run ID: {runId}[/]");
            }
            catch (Exception ex)
            {
                task.Description = "[red]Failed[/]";
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            }
        });
}

async Task ManageProvidersAsync()
{
    while (true)
    {
        var providers = (await providerRepo.GetAllAsync()).ToList();

        var choices = new List<string> { "Add New Provider" };
        choices.AddRange(providers.Select(p => $"Edit: {p.Name}"));
        choices.Add("Back");

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Manage Providers")
                .AddChoices(choices));

        if (choice == "Back") break;

        if (choice == "Add New Provider")
        {
            var name = AnsiConsole.Ask<string>("Provider name:");
            var url = AnsiConsole.Ask<string>("Base URL (e.g., http://localhost:1234/v1):");
            var useAuth = AnsiConsole.Confirm("Use authentication?", false);
            var authToken = useAuth ? AnsiConsole.Ask<string>("Auth token:") : null;

            await providerRepo.CreateAsync(new Provider
            {
                Name = name,
                BaseUrl = url,
                UseAuth = useAuth,
                AuthToken = authToken,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            AnsiConsole.MarkupLine("[green]✓ Provider added[/]");
        }
        else if (choice.StartsWith("Edit:"))
        {
            var providerName = choice.Replace("Edit: ", "");
            var provider = providers.First(p => p.Name == providerName);

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"Edit {provider.Name}")
                    .AddChoices("Toggle Active", "Delete", "Back"));

            if (action == "Toggle Active")
            {
                provider.IsActive = !provider.IsActive;
                await providerRepo.UpdateAsync(provider);
                AnsiConsole.MarkupLine($"[green]✓ Provider is now {(provider.IsActive ? "active" : "inactive")}[/]");
            }
            else if (action == "Delete")
            {
                if (AnsiConsole.Confirm($"Delete {provider.Name}?", false))
                {
                    await providerRepo.DeleteAsync(provider.Id);
                    AnsiConsole.MarkupLine("[green]✓ Provider deleted[/]");
                }
            }
        }
    }
}

async Task ManageConfigsAsync()
{
    var configs = (await configRepo.GetAllConfigsAsync()).ToList();

    var table = new Table();
    table.AddColumn("Name");
    table.AddColumn("Prompt Tests");
    table.AddColumn("Qualification");
    table.AddColumn("Conversation");
    table.AddColumn("Context Window");

    foreach (var config in configs)
    {
        table.AddRow(
            config.Name,
            config.RunPromptTests ? "[green]✓[/]" : "[dim]✗[/]",
            config.RunQualificationTests ? "[green]✓[/]" : "[dim]✗[/]",
            config.RunConversationTests ? "[green]✓[/]" : "[dim]✗[/]",
            config.RunContextWindowTests ? "[green]✓[/]" : "[dim]✗[/]"
        );
    }

    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Configuration editing available in web UI[/]");
}

async Task ViewResultsAsync()
{
    var benchmarkRepo = host.Services.GetRequiredService<IBenchmarkRepository>();
    var runs = (await benchmarkRepo.GetRecentRunsAsync(10)).ToList();

    if (!runs.Any())
    {
        AnsiConsole.MarkupLine("[yellow]No benchmark runs yet[/]");
        return;
    }

    var table = new Table();
    table.AddColumn("ID");
    table.AddColumn("Status");
    table.AddColumn("Models");
    table.AddColumn("Completed");
    table.AddColumn("Started");

    foreach (var run in runs)
    {
        var statusColor = run.Status switch
        {
            "completed" => "green",
            "running" => "blue",
            "failed" => "red",
            "cancelled" => "yellow",
            _ => "dim"
        };

        table.AddRow(
            run.Id.ToString(),
            $"[{statusColor}]{run.Status}[/]",
            run.TotalModels.ToString(),
            $"{run.CompletedTests}/{run.TotalTests}",
            run.StartedAt?.ToString("g") ?? "-"
        );
    }

    AnsiConsole.Write(table);
}

async Task MigrateTestsAsync()
{
    var configPath = AnsiConsole.Ask<string>(
        "Path to config directory (e.g., E:\\source\\Repos\\Squirmify\\src\\config):");

    if (!Directory.Exists(configPath))
    {
        AnsiConsole.MarkupLine("[red]Directory not found![/]");
        return;
    }

    var testRepo = host.Services.GetRequiredService<ITestDefinitionRepository>();
    var migrator = new DataMigrator(testRepo, configPath);

    await AnsiConsole.Status()
        .StartAsync("Migrating tests...", async ctx =>
        {
            ctx.Status("Migrating instruction tests...");
            var instructionCount = await migrator.MigrateInstructionTestsAsync();
            AnsiConsole.MarkupLine($"[green]✓ Migrated {instructionCount} instruction tests[/]");

            ctx.Status("Migrating reasoning tests...");
            var reasoningCount = await migrator.MigrateReasoningTestsAsync();
            AnsiConsole.MarkupLine($"[green]✓ Migrated {reasoningCount} reasoning tests[/]");

            ctx.Status("Migrating conversation tests...");
            var conversationCount = await migrator.MigrateConversationTestsAsync();
            AnsiConsole.MarkupLine($"[green]✓ Migrated {conversationCount} conversation tests[/]");
        });

    AnsiConsole.MarkupLine("[green]Migration complete![/]");
}

async Task HandleCliCommand(string[] args, IHost host)
{
    var command = args[0].ToLowerInvariant();

    switch (command)
    {
        case "migrate":
            var configPath = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config");
            configPath = Path.GetFullPath(configPath);

            if (!Directory.Exists(configPath))
            {
                AnsiConsole.MarkupLine($"[red]Config directory not found: {configPath}[/]");
                AnsiConsole.MarkupLine("[dim]Usage: squirmify migrate [config-path][/]");
                return;
            }

            AnsiConsole.MarkupLine($"[blue]Migrating tests from:[/] {configPath}");

            var testRepo = host.Services.GetRequiredService<ITestDefinitionRepository>();
            var migrator = new DataMigrator(testRepo, configPath);

            var total = await migrator.MigrateAllAsync();
            AnsiConsole.MarkupLine($"[green]✓ Migrated {total} tests total[/]");
            break;

        case "providers":
            await ListProvidersHeadlessAsync(host);
            break;

        case "configs":
            await ListConfigsHeadlessAsync(host);
            break;

        case "add-provider":
            await AddProviderHeadlessAsync(args, host);
            break;

        case "run":
            await RunBenchmarkHeadlessAsync(args, host);
            break;

        case "runs":
            await ListRunsHeadlessAsync(args, host);
            break;

        case "status":
            await ShowRunStatusHeadlessAsync(args, host);
            break;

        case "help":
        case "--help":
        case "-h":
            PrintHelp();
            break;

        default:
            AnsiConsole.MarkupLine($"[red]Unknown command: {command}[/]");
            PrintHelp();
            break;
    }
}

async Task ListProvidersHeadlessAsync(IHost host)
{
    var providerRepo = host.Services.GetRequiredService<IProviderRepository>();
    var providers = await providerRepo.GetAllAsync();

    if (!providers.Any())
    {
        Console.WriteLine("No providers configured. Use 'add-provider' to add one.");
        return;
    }

    Console.WriteLine("ID\tName\t\t\tURL");
    Console.WriteLine("--\t----\t\t\t---");
    foreach (var p in providers)
    {
        Console.WriteLine($"{p.Id}\t{p.Name,-20}\t{p.BaseUrl}");
    }
}

async Task ListConfigsHeadlessAsync(IHost host)
{
    var configRepo = host.Services.GetRequiredService<IConfigRepository>();
    var configs = await configRepo.GetAllConfigsAsync();

    if (!configs.Any())
    {
        Console.WriteLine("No configurations found. Create one via the Web UI.");
        return;
    }

    Console.WriteLine("ID\tName\t\t\tPrompt\tQualify\tConversation\tContext");
    Console.WriteLine("--\t----\t\t\t------\t-------\t------------\t-------");
    foreach (var c in configs)
    {
        Console.WriteLine($"{c.Id}\t{c.Name,-20}\t{c.RunPromptTests}\t{c.RunQualificationTests}\t{c.RunConversationTests}\t\t{c.RunContextWindowTests}");
    }
}

async Task ListRunsHeadlessAsync(string[] args, IHost host)
{
    var benchmarkRepo = host.Services.GetRequiredService<IBenchmarkRepository>();
    var count = 10;

    // Parse --count argument
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--count" && i + 1 < args.Length && int.TryParse(args[i + 1], out var c))
            count = c;
    }

    var runs = await benchmarkRepo.GetRecentRunsAsync(count);

    if (!runs.Any())
    {
        Console.WriteLine("No benchmark runs found.");
        return;
    }

    Console.WriteLine("ID\tStatus\t\tName\t\t\tProgress\tStarted");
    Console.WriteLine("--\t------\t\t----\t\t\t--------\t-------");
    foreach (var r in runs)
    {
        var progress = r.TotalTests > 0 ? $"{r.CompletedTests}/{r.TotalTests}" : "-";
        var started = r.StartedAt?.ToString("MM-dd HH:mm") ?? "-";
        Console.WriteLine($"{r.Id}\t{r.Status,-12}\t{r.Name ?? "Unnamed",-20}\t{progress,-12}\t{started}");
    }
}

async Task ShowRunStatusHeadlessAsync(string[] args, IHost host)
{
    var benchmarkRepo = host.Services.GetRequiredService<IBenchmarkRepository>();
    int? runId = null;

    // Parse run ID argument
    if (args.Length > 1 && int.TryParse(args[1], out var id))
        runId = id;

    BenchmarkRun? run;
    if (runId.HasValue)
    {
        run = await benchmarkRepo.GetRunByIdAsync(runId.Value);
    }
    else
    {
        // Get most recent run
        var runs = await benchmarkRepo.GetRecentRunsAsync(1);
        run = runs.FirstOrDefault();
    }

    if (run == null)
    {
        Console.WriteLine(runId.HasValue ? $"Run {runId} not found." : "No benchmark runs found.");
        return;
    }

    Console.WriteLine($"Run #{run.Id}: {run.Name ?? "Unnamed"}");
    Console.WriteLine($"Status:    {run.Status}");
    Console.WriteLine($"Progress:  {run.CompletedTests}/{run.TotalTests} tests ({run.ErrorCount} errors)");
    Console.WriteLine($"Started:   {run.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}");
    Console.WriteLine($"Completed: {run.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}");

    // Show model results if available
    var runModels = await benchmarkRepo.GetRunModelsAsync(run.Id);
    if (runModels.Any())
    {
        Console.WriteLine();
        Console.WriteLine("Models:");
        Console.WriteLine("  ID\tStatus\t\tQualified\tInstruction\tReasoning");
        Console.WriteLine("  --\t------\t\t---------\t-----------\t---------");
        foreach (var rm in runModels)
        {
            var qual = rm.QualificationPassed?.ToString() ?? "-";
            var instr = rm.InstructionPassRate?.ToString("P0") ?? "-";
            var reason = rm.ReasoningAvgScore?.ToString("F1") ?? "-";
            Console.WriteLine($"  {rm.ModelId}\t{rm.Status,-12}\t{qual,-12}\t{instr,-12}\t{reason}");
        }
    }

    // Show recent logs
    var logs = await benchmarkRepo.GetRunLogsAsync(run.Id);
    var recentLogs = logs.TakeLast(5).ToList();
    if (recentLogs.Any())
    {
        Console.WriteLine();
        Console.WriteLine("Recent logs:");
        foreach (var log in recentLogs)
        {
            var time = log.Timestamp.ToString("HH:mm:ss");
            Console.WriteLine($"  [{time}] [{log.Level}] {log.Message}");
        }
    }
}

async Task AddProviderHeadlessAsync(string[] args, IHost host)
{
    string? name = null, url = null, token = null;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--name" when i + 1 < args.Length:
                name = args[++i];
                break;
            case "--url" when i + 1 < args.Length:
                url = args[++i];
                break;
            case "--token" when i + 1 < args.Length:
                token = args[++i];
                break;
        }
    }

    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
    {
        Console.WriteLine("Usage: add-provider --name <name> --url <url> [--token <token>]");
        return;
    }

    var providerRepo = host.Services.GetRequiredService<IProviderRepository>();
    var provider = new Provider
    {
        Name = name,
        BaseUrl = url,
        AuthToken = token
    };

    var id = await providerRepo.CreateAsync(provider);
    Console.WriteLine($"Added provider '{name}' with ID {id}");
}

async Task RunBenchmarkHeadlessAsync(string[] args, IHost host)
{
    int? providerId = null, configId = null;
    string? runName = null;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--provider" when i + 1 < args.Length:
                if (int.TryParse(args[++i], out var pid)) providerId = pid;
                break;
            case "--config" when i + 1 < args.Length:
                if (int.TryParse(args[++i], out var cid)) configId = cid;
                break;
            case "--name" when i + 1 < args.Length:
                runName = args[++i];
                break;
        }
    }

    if (!providerId.HasValue || !configId.HasValue)
    {
        Console.WriteLine("Usage: run --provider <id> --config <id> [--name <name>]");
        Console.WriteLine("  Use 'providers' to list available providers");
        Console.WriteLine("  Use 'configs' to list available configurations");
        return;
    }

    var orchestrator = host.Services.GetRequiredService<IBenchmarkOrchestrator>();

    orchestrator.OnLogEvent += logEvent =>
    {
        var prefix = logEvent.Level switch
        {
            "error" => "[ERROR]",
            "warning" => "[WARN]",
            _ => "[INFO]"
        };
        Console.WriteLine($"{prefix} {logEvent.Message}");
    };

    orchestrator.OnProgressUpdate += progress =>
    {
        Console.WriteLine($"[PROGRESS] {progress.Stage}: {progress.CurrentTestIndex}/{progress.TotalTests} - {progress.CurrentModel}");
    };

    Console.WriteLine($"Starting benchmark run: provider={providerId}, config={configId}, name={runName ?? "(auto)"}");

    try
    {
        var runId = await orchestrator.StartRunAsync(configId.Value, providerId.Value, runName);
        Console.WriteLine($"Benchmark completed. Run ID: {runId}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Benchmark failed: {ex.Message}");
    }
}

void PrintHelp()
{
    AnsiConsole.Write(new FigletText("Squirmify").Color(Color.Blue));
    AnsiConsole.MarkupLine("[dim]LLM Benchmarking Platform[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[yellow]Commands:[/]");
    AnsiConsole.MarkupLine("  [green]providers[/]                          List configured providers");
    AnsiConsole.MarkupLine("  [green]configs[/]                            List test configurations");
    AnsiConsole.MarkupLine("  [green]runs[/] [[--count <n>]]                 List recent benchmark runs");
    AnsiConsole.MarkupLine("  [green]status[/] [[run-id]]                    Show run status (default: latest)");
    AnsiConsole.MarkupLine("  [green]add-provider[/] --name <n> --url <u>  Add a new provider");
    AnsiConsole.MarkupLine("                       [[--token <t>]]");
    AnsiConsole.MarkupLine("  [green]run[/] --provider <id> --config <id>  Run benchmark (headless)");
    AnsiConsole.MarkupLine("       [[--name <name>]]");
    AnsiConsole.MarkupLine("  [green]migrate[/] <path>                     Migrate JSON tests to database");
    AnsiConsole.MarkupLine("  [green]help[/]                               Show this help message");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Run without arguments for interactive menu[/]");
}
