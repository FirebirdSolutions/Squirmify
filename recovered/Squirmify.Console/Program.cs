using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Squirmify.Console;
using Squirmify.Core;
using Squirmify.Core.DTOs;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;
using Squirmify.Services;

[CompilerGenerated]
internal class Program
{
	private static async Task _003CMain_003E_0024(string[] args)
	{
		string dataDir = SquirmifyPaths.ResolveDataDirectory(args);
		string connectionString = SquirmifyPaths.GetConnectionString(dataDir);
		Console.WriteLine("[Squirmify] Data directory: " + dataDir);
		IHost host = Host.CreateDefaultBuilder(args).ConfigureServices(delegate(IServiceCollection services)
		{
			services.AddSquirmifyServices(connectionString);
		}).Build();
		await host.Services.InitializeDatabaseAsync();
		if (args.Length != 0)
		{
			await HandleCliCommand(args, host);
			return;
		}
		AnsiConsole.Write(new FigletText("Squirmify").Color(Color.Blue));
		AnsiConsole.MarkupLine("[dim]LLM Benchmarking Platform[/]");
		AnsiConsole.WriteLine();
		IProviderRepository providerRepo = host.Services.GetRequiredService<IProviderRepository>();
		IConfigRepository configRepo = host.Services.GetRequiredService<IConfigRepository>();
		IBenchmarkOrchestrator orchestrator = host.Services.GetRequiredService<IBenchmarkOrchestrator>();
		orchestrator.OnProgressUpdate += delegate
		{
		};
		orchestrator.OnLogEvent += delegate(LogEvent logEvent)
		{
			string level = logEvent.Level;
			if (1 == 0)
			{
			}
			string text = ((level == "error") ? "red" : ((!(level == "warning")) ? "dim" : "yellow"));
			if (1 == 0)
			{
			}
			string value = text;
			AnsiConsole.MarkupLine($"[{value}]{logEvent.Message}[/]");
		};
		while (true)
		{
			switch (AnsiConsole.Prompt(new SelectionPrompt<string>().Title("What would you like to do?").AddChoices("Start New Benchmark", "Manage Providers", "Manage Configurations", "View Results", "Migrate JSON Tests to Database", "Exit")))
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
		static async Task AddProviderHeadlessAsync(string[] array, IHost host2)
		{
			string name = null;
			string url = null;
			string token = null;
			for (int i = 1; i < array.Length; i++)
			{
				switch (array[i])
				{
				case "--name":
					if (i + 1 < array.Length)
					{
						int num = i + 1;
						i = num;
						name = array[num];
					}
					break;
				case "--url":
					if (i + 1 < array.Length)
					{
						int num = i + 1;
						i = num;
						url = array[num];
					}
					break;
				case "--token":
					if (i + 1 < array.Length)
					{
						int num = i + 1;
						i = num;
						token = array[num];
					}
					break;
				}
			}
			if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
			{
				Console.WriteLine("Usage: add-provider --name <name> --url <url> [--token <token>]");
			}
			else
			{
				IProviderRepository providerRepo2 = host2.Services.GetRequiredService<IProviderRepository>();
				Provider provider = new Provider
				{
					Name = name,
					BaseUrl = url,
					AuthToken = token
				};
				int id = await providerRepo2.CreateAsync(provider);
				Console.WriteLine($"Added provider '{name}' with ID {id}");
			}
		}
		static async Task HandleCliCommand(string[] array, IHost host2)
		{
			string command = array[0].ToLowerInvariant();
			switch (command)
			{
			case "migrate":
			{
				object obj;
				if (array.Length <= 1)
				{
					_003C_003Ey__InlineArray6<string> buffer = default(_003C_003Ey__InlineArray6<string>);
					buffer[0] = AppContext.BaseDirectory;
					buffer[1] = "..";
					buffer[2] = "..";
					buffer[3] = "..";
					buffer[4] = "..";
					buffer[5] = "config";
					obj = Path.Combine(buffer);
				}
				else
				{
					obj = array[1];
				}
				string configPath = (string)obj;
				configPath = Path.GetFullPath(configPath);
				if (!Directory.Exists(configPath))
				{
					AnsiConsole.MarkupLine("[red]Config directory not found: " + configPath + "[/]");
					AnsiConsole.MarkupLine("[dim]Usage: squirmify migrate [config-path][/]");
				}
				else
				{
					AnsiConsole.MarkupLine("[blue]Migrating tests from:[/] " + configPath);
					ITestDefinitionRepository testRepo = host2.Services.GetRequiredService<ITestDefinitionRepository>();
					DataMigrator migrator = new DataMigrator(testRepo, configPath);
					int total = await migrator.MigrateAllAsync();
					AnsiConsole.MarkupLine($"[green]✓ Migrated {total} tests total[/]");
				}
				break;
			}
			case "providers":
				await ListProvidersHeadlessAsync(host2);
				break;
			case "configs":
				await ListConfigsHeadlessAsync(host2);
				break;
			case "add-provider":
				await AddProviderHeadlessAsync(array, host2);
				break;
			case "run":
				await RunBenchmarkHeadlessAsync(array, host2);
				break;
			case "runs":
				await ListRunsHeadlessAsync(array, host2);
				break;
			case "status":
				await ShowRunStatusHeadlessAsync(array, host2);
				break;
			case "help":
			case "--help":
			case "-h":
				PrintHelp();
				break;
			default:
				AnsiConsole.MarkupLine("[red]Unknown command: " + command + "[/]");
				PrintHelp();
				break;
			}
		}
		static async Task ListConfigsHeadlessAsync(IHost host2)
		{
			IConfigRepository configRepo2 = host2.Services.GetRequiredService<IConfigRepository>();
			IEnumerable<TestSuiteConfig> configs = await configRepo2.GetAllConfigsAsync();
			if (!configs.Any())
			{
				Console.WriteLine("No configurations found. Create one via the Web UI.");
			}
			else
			{
				Console.WriteLine("ID\tName\t\t\tPrompt\tQualify\tConversation\tContext");
				Console.WriteLine("--\t----\t\t\t------\t-------\t------------\t-------");
				foreach (TestSuiteConfig c in configs)
				{
					Console.WriteLine($"{c.Id}\t{c.Name,-20}\t{c.RunPromptTests}\t{c.RunQualificationTests}\t{c.RunConversationTests}\t\t{c.RunContextWindowTests}");
				}
			}
		}
		static async Task ListProvidersHeadlessAsync(IHost host2)
		{
			IProviderRepository providerRepo2 = host2.Services.GetRequiredService<IProviderRepository>();
			IEnumerable<Provider> providers = await providerRepo2.GetAllAsync();
			if (!providers.Any())
			{
				Console.WriteLine("No providers configured. Use 'add-provider' to add one.");
			}
			else
			{
				Console.WriteLine("ID\tName\t\t\tURL");
				Console.WriteLine("--\t----\t\t\t---");
				foreach (Provider p in providers)
				{
					Console.WriteLine($"{p.Id}\t{p.Name,-20}\t{p.BaseUrl}");
				}
			}
		}
		static async Task ListRunsHeadlessAsync(string[] array, IHost host2)
		{
			IBenchmarkRepository benchmarkRepo = host2.Services.GetRequiredService<IBenchmarkRepository>();
			int count = 10;
			for (int i = 1; i < array.Length; i++)
			{
				if (array[i] == "--count" && i + 1 < array.Length && int.TryParse(array[i + 1], out var c))
				{
					count = c;
				}
			}
			IEnumerable<BenchmarkRun> runs = await benchmarkRepo.GetRecentRunsAsync(count);
			if (!runs.Any())
			{
				Console.WriteLine("No benchmark runs found.");
			}
			else
			{
				Console.WriteLine("ID\tStatus\t\tName\t\t\tProgress\tStarted");
				Console.WriteLine("--\t------\t\t----\t\t\t--------\t-------");
				foreach (BenchmarkRun r in runs)
				{
					string progress = ((r.TotalTests > 0) ? $"{r.CompletedTests}/{r.TotalTests}" : "-");
					string started = r.StartedAt?.ToString("MM-dd HH:mm") ?? "-";
					Console.WriteLine($"{r.Id}\t{r.Status,-12}\t{r.Name ?? "Unnamed",-20}\t{progress,-12}\t{started}");
				}
			}
		}
		async Task ManageConfigsAsync()
		{
			List<TestSuiteConfig> configs = (await configRepo.GetAllConfigsAsync()).ToList();
			Table table = new Table();
			table.AddColumn("Name");
			table.AddColumn("Prompt Tests");
			table.AddColumn("Qualification");
			table.AddColumn("Conversation");
			table.AddColumn("Context Window");
			foreach (TestSuiteConfig config in configs)
			{
				table.AddRow(config.Name, config.RunPromptTests ? "[green]✓[/]" : "[dim]✗[/]", config.RunQualificationTests ? "[green]✓[/]" : "[dim]✗[/]", config.RunConversationTests ? "[green]✓[/]" : "[dim]✗[/]", config.RunContextWindowTests ? "[green]✓[/]" : "[dim]✗[/]");
			}
			AnsiConsole.Write(table);
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("[dim]Configuration editing available in web UI[/]");
		}
		async Task ManageProvidersAsync()
		{
			while (true)
			{
				List<Provider> providers = (await providerRepo.GetAllAsync()).ToList();
				List<string> choices = new List<string> { "Add New Provider" };
				choices.AddRange(providers.Select((Provider p) => "Edit: " + p.Name));
				choices.Add("Back");
				string choice = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Manage Providers").AddChoices(choices));
				if (choice == "Back")
				{
					break;
				}
				if (choice == "Add New Provider")
				{
					string name = AnsiConsole.Ask<string>("Provider name:");
					string url = AnsiConsole.Ask<string>("Base URL (e.g., http://localhost:1234/v1):");
					bool useAuth = AnsiConsole.Confirm("Use authentication?", defaultValue: false);
					string authToken = (useAuth ? AnsiConsole.Ask<string>("Auth token:") : null);
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
					string providerName = choice.Replace("Edit: ", "");
					Provider provider = providers.First((Provider p) => p.Name == providerName);
					string action = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Edit " + provider.Name).AddChoices("Toggle Active", "Delete", "Back"));
					if (action == "Toggle Active")
					{
						provider.IsActive = !provider.IsActive;
						await providerRepo.UpdateAsync(provider);
						AnsiConsole.MarkupLine("[green]✓ Provider is now " + (provider.IsActive ? "active" : "inactive") + "[/]");
					}
					else if (action == "Delete" && AnsiConsole.Confirm("Delete " + provider.Name + "?", defaultValue: false))
					{
						await providerRepo.DeleteAsync(provider.Id);
						AnsiConsole.MarkupLine("[green]✓ Provider deleted[/]");
					}
				}
			}
		}
		async Task MigrateTestsAsync()
		{
			string configPath = AnsiConsole.Ask<string>("Path to config directory (e.g., E:\\source\\Repos\\Squirmify\\src\\config):");
			if (!Directory.Exists(configPath))
			{
				AnsiConsole.MarkupLine("[red]Directory not found![/]");
			}
			else
			{
				ITestDefinitionRepository testRepo = host.Services.GetRequiredService<ITestDefinitionRepository>();
				DataMigrator migrator = new DataMigrator(testRepo, configPath);
				await AnsiConsole.Status().StartAsync("Migrating tests...", async delegate(StatusContext ctx)
				{
					ctx.Status("Migrating instruction tests...");
					int instructionCount = await migrator.MigrateInstructionTestsAsync();
					AnsiConsole.MarkupLine($"[green]✓ Migrated {instructionCount} instruction tests[/]");
					ctx.Status("Migrating reasoning tests...");
					int reasoningCount = await migrator.MigrateReasoningTestsAsync();
					AnsiConsole.MarkupLine($"[green]✓ Migrated {reasoningCount} reasoning tests[/]");
					ctx.Status("Migrating conversation tests...");
					int conversationCount = await migrator.MigrateConversationTestsAsync();
					AnsiConsole.MarkupLine($"[green]✓ Migrated {conversationCount} conversation tests[/]");
				});
				AnsiConsole.MarkupLine("[green]Migration complete![/]");
			}
		}
		static void PrintHelp()
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
		static async Task RunBenchmarkHeadlessAsync(string[] array, IHost host2)
		{
			int? providerId = null;
			int? configId = null;
			string runName = null;
			for (int i = 1; i < array.Length; i++)
			{
				switch (array[i])
				{
				case "--provider":
					if (i + 1 < array.Length)
					{
						int num = i + 1;
						i = num;
						if (int.TryParse(array[num], out var pid))
						{
							providerId = pid;
						}
					}
					break;
				case "--config":
					if (i + 1 < array.Length)
					{
						int num = i + 1;
						i = num;
						if (int.TryParse(array[num], out var cid))
						{
							configId = cid;
						}
					}
					break;
				case "--name":
					if (i + 1 < array.Length)
					{
						int num = i + 1;
						i = num;
						runName = array[num];
					}
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
			IBenchmarkOrchestrator orchestrator2 = host2.Services.GetRequiredService<IBenchmarkOrchestrator>();
			orchestrator2.OnLogEvent += delegate(LogEvent logEvent)
			{
				string level = logEvent.Level;
				if (1 == 0)
				{
				}
				string text = ((level == "error") ? "[ERROR]" : ((!(level == "warning")) ? "[INFO]" : "[WARN]"));
				if (1 == 0)
				{
				}
				string text2 = text;
				Console.WriteLine(text2 + " " + logEvent.Message);
			};
			orchestrator2.OnProgressUpdate += delegate(RunProgress progress)
			{
				Console.WriteLine($"[PROGRESS] {progress.Stage}: {progress.CurrentTestIndex}/{progress.TotalTests} - {progress.CurrentModel}");
			};
			Console.WriteLine($"Starting benchmark run: provider={providerId}, config={configId}, name={runName ?? "(auto)"}");
			try
			{
				int runId = await orchestrator2.StartRunAsync(configId.Value, providerId.Value, runName);
				Console.WriteLine($"Benchmark completed. Run ID: {runId}");
			}
			catch (Exception ex)
			{
				Console.WriteLine("[ERROR] Benchmark failed: " + ex.Message);
			}
		}
		static async Task ShowRunStatusHeadlessAsync(string[] array, IHost host2)
		{
			IBenchmarkRepository benchmarkRepo = host2.Services.GetRequiredService<IBenchmarkRepository>();
			int? runId = null;
			if (array.Length > 1 && int.TryParse(array[1], out var id))
			{
				runId = id;
			}
			BenchmarkRun run = ((!runId.HasValue) ? (await benchmarkRepo.GetRecentRunsAsync(1)).FirstOrDefault() : (await benchmarkRepo.GetRunByIdAsync(runId.Value)));
			if (run == null)
			{
				Console.WriteLine(runId.HasValue ? $"Run {runId} not found." : "No benchmark runs found.");
			}
			else
			{
				Console.WriteLine($"Run #{run.Id}: {run.Name ?? "Unnamed"}");
				Console.WriteLine("Status:    " + run.Status);
				Console.WriteLine($"Progress:  {run.CompletedTests}/{run.TotalTests} tests ({run.ErrorCount} errors)");
				Console.WriteLine("Started:   " + (run.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"));
				Console.WriteLine("Completed: " + (run.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"));
				IEnumerable<BenchmarkRunModel> runModels = await benchmarkRepo.GetRunModelsAsync(run.Id);
				if (runModels.Any())
				{
					Console.WriteLine();
					Console.WriteLine("Models:");
					Console.WriteLine("  ID\tStatus\t\tQualified\tInstruction\tReasoning");
					Console.WriteLine("  --\t------\t\t---------\t-----------\t---------");
					foreach (BenchmarkRunModel rm in runModels)
					{
						string qual = rm.QualificationPassed?.ToString() ?? "-";
						string instr = rm.InstructionPassRate?.ToString("P0") ?? "-";
						string reason = rm.ReasoningAvgScore?.ToString("F1") ?? "-";
						Console.WriteLine($"  {rm.ModelId}\t{rm.Status,-12}\t{qual,-12}\t{instr,-12}\t{reason}");
					}
				}
				List<RunLog> recentLogs = (await benchmarkRepo.GetRunLogsAsync(run.Id)).TakeLast(5).ToList();
				if (recentLogs.Any())
				{
					Console.WriteLine();
					Console.WriteLine("Recent logs:");
					foreach (RunLog log in recentLogs)
					{
						string time = log.Timestamp.ToString("HH:mm:ss");
						Console.WriteLine($"  [{time}] [{log.Level}] {log.Message}");
					}
				}
			}
		}
		async Task StartBenchmarkAsync()
		{
			List<Provider> providers = (await providerRepo.GetActiveAsync()).ToList();
			if (!providers.Any())
			{
				AnsiConsole.MarkupLine("[yellow]No providers configured. Add a provider first.[/]");
			}
			else
			{
				List<TestSuiteConfig> configs = (await configRepo.GetAllConfigsAsync()).ToList();
				if (!configs.Any())
				{
					AnsiConsole.MarkupLine("[yellow]No configurations found. Creating default...[/]");
					TestSuiteConfig defaultConfig = new TestSuiteConfig
					{
						Name = "Default",
						Description = "Default test configuration",
						CreatedAt = DateTime.UtcNow
					};
					await configRepo.CreateConfigAsync(defaultConfig);
					configs = (await configRepo.GetAllConfigsAsync()).ToList();
				}
				Provider providerChoice = AnsiConsole.Prompt(new SelectionPrompt<Provider>().Title("Select provider:").UseConverter((Provider p) => p.Name + " (" + p.BaseUrl + ")").AddChoices(providers));
				TestSuiteConfig configChoice = AnsiConsole.Prompt(new SelectionPrompt<TestSuiteConfig>().Title("Select configuration:").UseConverter((TestSuiteConfig c) => c.Name + " - " + (c.Description ?? "No description")).AddChoices(configs));
				if (AnsiConsole.Confirm($"Start benchmark with [green]{configChoice.Name}[/] on [blue]{providerChoice.Name}[/]?"))
				{
					await AnsiConsole.Progress().AutoClear(enabled: false).Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
						.StartAsync(async delegate(ProgressContext ctx)
						{
							ProgressTask task = ctx.AddTask("[green]Running benchmark...[/]");
							orchestrator.OnProgressUpdate += delegate(RunProgress progress)
							{
								task.Description = "[green]" + progress.Stage + "[/] " + progress.CurrentModel;
								task.Value = progress.PercentComplete;
							};
							try
							{
								int runId = await orchestrator.StartRunAsync(configChoice.Id, providerChoice.Id);
								task.Value = 100.0;
								AnsiConsole.MarkupLine($"[green]✓ Benchmark complete! Run ID: {runId}[/]");
							}
							catch (Exception ex)
							{
								Exception ex2 = ex;
								task.Description = "[red]Failed[/]";
								AnsiConsole.MarkupLine("[red]Error: " + ex2.Message + "[/]");
							}
						});
				}
			}
		}
		async Task ViewResultsAsync()
		{
			IBenchmarkRepository benchmarkRepo = host.Services.GetRequiredService<IBenchmarkRepository>();
			List<BenchmarkRun> runs = (await benchmarkRepo.GetRecentRunsAsync()).ToList();
			if (!runs.Any())
			{
				AnsiConsole.MarkupLine("[yellow]No benchmark runs yet[/]");
			}
			else
			{
				Table table = new Table();
				table.AddColumn("ID");
				table.AddColumn("Status");
				table.AddColumn("Models");
				table.AddColumn("Completed");
				table.AddColumn("Started");
				foreach (BenchmarkRun run in runs)
				{
					string status = run.Status;
					if (1 == 0)
					{
					}
					string text = status switch
					{
						"completed" => "green", 
						"running" => "blue", 
						"failed" => "red", 
						"cancelled" => "yellow", 
						_ => "dim", 
					};
					if (1 == 0)
					{
					}
					string statusColor = text;
					table.AddRow(run.Id.ToString(), $"[{statusColor}]{run.Status}[/]", run.TotalModels.ToString(), $"{run.CompletedTests}/{run.TotalTests}", run.StartedAt?.ToString("g") ?? "-");
				}
				AnsiConsole.Write(table);
			}
		}
	}
}
