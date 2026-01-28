using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Squirmify.Data.Database;

public class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly string? _seedsJsonPath;

    public DatabaseInitializer(string connectionString, string? seedsJsonPath = null)
    {
        _connectionString = connectionString;
        _seedsJsonPath = seedsJsonPath;
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Create all tables
        await connection.ExecuteAsync(CreateProvidersTables);
        await connection.ExecuteAsync(CreateConfigTables);
        await connection.ExecuteAsync(CreateTestDefinitionTables);
        await connection.ExecuteAsync(CreateSeedTables);
        await connection.ExecuteAsync(CreateBenchmarkRunTables);
        await connection.ExecuteAsync(CreateResultTables);
        await connection.ExecuteAsync(CreateIndexes);

        // Run migrations for existing databases
        await RunMigrationsAsync(connection);
    }

    private async Task RunMigrationsAsync(SqliteConnection connection)
    {
        // Migration: Add ProviderId to BenchmarkRuns if it doesn't exist
        var columns = await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('BenchmarkRuns')");
        var columnList = columns.ToList();

        if (!columnList.Contains("ProviderId"))
        {
            await connection.ExecuteAsync(
                "ALTER TABLE BenchmarkRuns ADD COLUMN ProviderId INTEGER NOT NULL DEFAULT 0");
        }

        // Migration: Add InstructionPassThreshold to TestSuiteConfigs if it doesn't exist
        var configColumns = await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('TestSuiteConfigs')");
        if (!configColumns.Contains("InstructionPassThreshold"))
        {
            await connection.ExecuteAsync(
                "ALTER TABLE TestSuiteConfigs ADD COLUMN InstructionPassThreshold REAL DEFAULT 0.8");
        }

        // Migration: Add test limit columns
        if (!configColumns.Contains("MaxInstructionTests"))
        {
            await connection.ExecuteAsync(@"
                ALTER TABLE TestSuiteConfigs ADD COLUMN MaxInstructionTests INTEGER DEFAULT 10;
                ALTER TABLE TestSuiteConfigs ADD COLUMN MaxReasoningTests INTEGER DEFAULT 10;
                ALTER TABLE TestSuiteConfigs ADD COLUMN MaxConversationTests INTEGER DEFAULT 10;
            ");
        }

        // Migration: Add context window config columns
        if (!configColumns.Contains("ContextWindowTargetTokens"))
        {
            await connection.ExecuteAsync(@"
                ALTER TABLE TestSuiteConfigs ADD COLUMN ContextWindowTargetTokens INTEGER DEFAULT 32000;
                ALTER TABLE TestSuiteConfigs ADD COLUMN ContextWindowProbeCount INTEGER DEFAULT 10;
                ALTER TABLE TestSuiteConfigs ADD COLUMN ContextWindowCheckpoints INTEGER DEFAULT 4;
            ");
        }

        // Migration: Fix score columns from INTEGER to REAL
        // SQLite doesn't support ALTER COLUMN, so we recreate tables with correct types
        await MigrateScoreColumnsToRealAsync(connection);

        // Migration: Add unique indexes to prevent duplicate results
        await AddUniqueResultIndexesAsync(connection);

        // Migration: Add RelativePosition column to ContextWindowCheckpoints
        var checkpointColumns = await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('ContextWindowCheckpoints')");
        if (!checkpointColumns.Contains("RelativePosition"))
        {
            await connection.ExecuteAsync(
                "ALTER TABLE ContextWindowCheckpoints ADD COLUMN RelativePosition REAL");
        }

        // Migration: Add context window aggregate columns to BenchmarkRunModels
        var runModelColumns = await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('BenchmarkRunModels')");
        if (!runModelColumns.Contains("ContextWindowAvgReliability"))
        {
            await connection.ExecuteAsync(@"
                ALTER TABLE BenchmarkRunModels ADD COLUMN ContextWindowAvgReliability REAL;
                ALTER TABLE BenchmarkRunModels ADD COLUMN ContextWindowAvgAccuracy REAL;
                ALTER TABLE BenchmarkRunModels ADD COLUMN ContextWindowTestCount INTEGER DEFAULT 0;
            ");
        }

        // Migration: Add NeedleComplexity column to ContextWindowTests
        var ctxTestColumns = await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('ContextWindowTests')");
        if (!ctxTestColumns.Contains("NeedleComplexity"))
        {
            await connection.ExecuteAsync(
                "ALTER TABLE ContextWindowTests ADD COLUMN NeedleComplexity TEXT DEFAULT 'single'");
        }

        // Migration: Add ContextWindowTestType column to TestSuiteConfigs
        if (!configColumns.Contains("ContextWindowTestType"))
        {
            await connection.ExecuteAsync(
                "ALTER TABLE TestSuiteConfigs ADD COLUMN ContextWindowTestType TEXT DEFAULT 'Multi-Needle Recall'");
        }

        // Migration: Add IsDisabled, IsAvailable, IsDeleted columns to Models
        var modelColumns = (await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('Models')")).ToList();

        // Migrate from old IsActive to new three-state model
        if (modelColumns.Contains("IsActive") && !modelColumns.Contains("IsDisabled"))
        {
            // Add new columns
            await connection.ExecuteAsync(
                "ALTER TABLE Models ADD COLUMN IsDisabled INTEGER DEFAULT 0");
            await connection.ExecuteAsync(
                "ALTER TABLE Models ADD COLUMN IsAvailable INTEGER DEFAULT 1");
            await connection.ExecuteAsync(
                "ALTER TABLE Models ADD COLUMN IsDeleted INTEGER DEFAULT 0");

            // Migrate: IsActive=0 means either disabled or unavailable
            // Assume unavailable (safer default) - user can re-disable manually if needed
            await connection.ExecuteAsync(
                "UPDATE Models SET IsAvailable = IsActive");
        }
        else
        {
            // Fresh install or already migrated - just ensure columns exist
            if (!modelColumns.Contains("IsDisabled"))
                await connection.ExecuteAsync("ALTER TABLE Models ADD COLUMN IsDisabled INTEGER DEFAULT 0");
            if (!modelColumns.Contains("IsAvailable"))
                await connection.ExecuteAsync("ALTER TABLE Models ADD COLUMN IsAvailable INTEGER DEFAULT 1");
            if (!modelColumns.Contains("IsDeleted"))
                await connection.ExecuteAsync("ALTER TABLE Models ADD COLUMN IsDeleted INTEGER DEFAULT 0");
        }

        // Migration: Add constraint fields to InstructionTests
        var instrTestColumns = (await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('InstructionTests')")).ToList();
        if (!instrTestColumns.Contains("ExcludePatterns"))
            await connection.ExecuteAsync("ALTER TABLE InstructionTests ADD COLUMN ExcludePatterns TEXT");
        if (!instrTestColumns.Contains("AllowedValues"))
            await connection.ExecuteAsync("ALTER TABLE InstructionTests ADD COLUMN AllowedValues TEXT");
        if (!instrTestColumns.Contains("ExpectedCount"))
            await connection.ExecuteAsync("ALTER TABLE InstructionTests ADD COLUMN ExpectedCount INTEGER");

        // Migration: Add MCP tool test config fields
        var configCols = (await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('TestSuiteConfigs')")).ToList();
        if (!configCols.Contains("RunMcpToolTests"))
            await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN RunMcpToolTests INTEGER DEFAULT 0");
        if (!configCols.Contains("MaxMcpToolTests"))
            await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN MaxMcpToolTests INTEGER DEFAULT 10");
        if (!configCols.Contains("EchoMcpBaseUrl"))
            await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN EchoMcpBaseUrl TEXT");
        if (!configCols.Contains("EchoMcpToken"))
            await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN EchoMcpToken TEXT");
        if (!configCols.Contains("FetchSchemasFromEchoMcp"))
            await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN FetchSchemasFromEchoMcp INTEGER DEFAULT 1");
        if (!configCols.Contains("McpTransportType"))
            await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN McpTransportType TEXT DEFAULT 'sse'");
        if (!configCols.Contains("McpServerUrl"))
            await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN McpServerUrl TEXT");
        if (!configCols.Contains("McpServerCommand"))
            await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN McpServerCommand TEXT");
        if (!configCols.Contains("McpServerArgs"))
            await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN McpServerArgs TEXT");

        // Migration: Add TokensPerSec, PromptTokens, CompletionTokens to ConversationTestResults
        var convResultCols = (await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('ConversationTestResults')")).ToList();
        if (!convResultCols.Contains("TokensPerSec"))
            await connection.ExecuteAsync("ALTER TABLE ConversationTestResults ADD COLUMN TokensPerSec REAL");
        if (!convResultCols.Contains("PromptTokens"))
            await connection.ExecuteAsync("ALTER TABLE ConversationTestResults ADD COLUMN PromptTokens INTEGER");
        if (!convResultCols.Contains("CompletionTokens"))
            await connection.ExecuteAsync("ALTER TABLE ConversationTestResults ADD COLUMN CompletionTokens INTEGER");

        // Import seeds from base_seeds.jsonl if table is empty
        await ImportSeedsIfEmptyAsync(connection);

        // Import context window tests from context_window_tests.json if table is empty
        await ImportContextWindowTestsIfEmptyAsync(connection);

        // Import test definitions from config/tests/*.json if tables are empty
        await ImportInstructionTestsIfEmptyAsync(connection);
        await ImportReasoningTestsIfEmptyAsync(connection);
        await ImportConversationTestsIfEmptyAsync(connection);
        await ImportMcpToolTestsIfEmptyAsync(connection);
    }

    private async Task ImportSeedsIfEmptyAsync(SqliteConnection connection)
    {
        var seedCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Seeds");
        if (seedCount > 0) return;

        // Try to find base_seeds.jsonl (the source base seeds)
        var baseSeedsPath = _seedsJsonPath;
        if (string.IsNullOrEmpty(baseSeedsPath))
        {
            // Look in common locations for base_seeds.jsonl
            var searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "base_seeds.jsonl"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "config", "base_seeds.jsonl"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "config", "base_seeds.jsonl"),
                "src/config/base_seeds.jsonl"
            };

            baseSeedsPath = searchPaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
        }

        if (string.IsNullOrEmpty(baseSeedsPath) || !File.Exists(baseSeedsPath))
        {
            Console.WriteLine("[DatabaseInitializer] No base_seeds.jsonl found, skipping seed import");
            return;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(baseSeedsPath);
            var importedCount = 0;

            // Use transaction for faster bulk insert
            using var transaction = connection.BeginTransaction();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var seed = JsonSerializer.Deserialize<BaseSeedItem>(line, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (seed == null || string.IsNullOrEmpty(seed.Instruction)) continue;

                    // First tag is the primary category
                    var category = seed.Tags?.FirstOrDefault() ?? "instruction";

                    await connection.ExecuteAsync("""
                        INSERT INTO Seeds (Category, Instruction, IsAugmented, CreatedAt)
                        VALUES (@Category, @Instruction, 0, datetime('now'))
                        """, new { Category = category, Instruction = seed.Instruction }, transaction);

                    // Also add tags
                    var seedId = await connection.ExecuteScalarAsync<int>("SELECT last_insert_rowid()", transaction: transaction);
                    if (seed.Tags != null)
                    {
                        foreach (var tag in seed.Tags)
                        {
                            await connection.ExecuteAsync("""
                                INSERT OR IGNORE INTO SeedTags (SeedId, Tag)
                                VALUES (@SeedId, @Tag)
                                """, new { SeedId = seedId, Tag = tag }, transaction);
                        }
                    }

                    importedCount++;
                }
                catch
                {
                    // Skip malformed lines
                }
            }

            transaction.Commit();
            Console.WriteLine($"[DatabaseInitializer] Imported {importedCount} base seeds from base_seeds.jsonl");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseInitializer] Failed to import seeds: {ex.Message}");
        }
    }

    // DTO for base_seeds.jsonl parsing (JSONL format)
    private class BaseSeedItem
    {
        public string Instruction { get; set; } = string.Empty;
        public List<string>? Tags { get; set; }
    }

    private async Task ImportContextWindowTestsIfEmptyAsync(SqliteConnection connection)
    {
        var testCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ContextWindowTests");
        if (testCount > 0) return;

        // Look for context_window_tests.json in common locations
        var searchPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tests", "context_window_tests.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "config", "tests", "context_window_tests.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "config", "tests", "context_window_tests.json"),
            "src/config/tests/context_window_tests.json"
        };

        var seedsPath = searchPaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(seedsPath))
        {
            Console.WriteLine("[DatabaseInitializer] No context_window_tests.json found, skipping context window test import");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(seedsPath);
            var seedData = JsonSerializer.Deserialize<ContextWindowTestSeedFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (seedData?.Tests == null || seedData.Tests.Count == 0)
            {
                Console.WriteLine("[DatabaseInitializer] context_window_tests.json is empty or invalid");
                return;
            }

            var importedCount = 0;
            using var transaction = connection.BeginTransaction();

            foreach (var test in seedData.Tests)
            {
                // Insert the test
                var testId = await connection.ExecuteScalarAsync<int>("""
                    INSERT INTO ContextWindowTests (Name, Description, FillerType, BaseTargetTokens, BaseCheckpointCount, BuriedInstruction, IsActive, CreatedAt)
                    VALUES (@Name, @Description, @FillerType, @BaseTargetTokens, @BaseCheckpointCount, @BuriedInstruction, 1, datetime('now'));
                    SELECT last_insert_rowid();
                    """, new
                {
                    test.Name,
                    test.Description,
                    test.FillerType,
                    test.BaseTargetTokens,
                    BaseCheckpointCount = test.BaseCheckpointCount ?? test.Checkpoints?.Count ?? 0,
                    test.BuriedInstruction
                }, transaction);

                // Insert checkpoints if defined
                if (test.Checkpoints != null)
                {
                    var sortOrder = 0;
                    foreach (var cp in test.Checkpoints)
                    {
                        await connection.ExecuteAsync("""
                            INSERT INTO ContextWindowCheckpoints (TestId, TargetTokenPosition, RelativePosition, SecretWord, CarrierSentence, SortOrder)
                            VALUES (@TestId, @TargetTokenPosition, @RelativePosition, @SecretWord, @CarrierSentence, @SortOrder)
                            """, new
                        {
                            TestId = testId,
                            TargetTokenPosition = cp.RelativePosition.HasValue
                                ? (int)(cp.RelativePosition.Value * test.BaseTargetTokens)
                                : 0,
                            cp.RelativePosition,
                            cp.SecretWord,
                            cp.CarrierSentence,
                            SortOrder = sortOrder++
                        }, transaction);
                    }
                }

                importedCount++;
            }

            transaction.Commit();

            Console.WriteLine($"[DatabaseInitializer] Imported {importedCount} context window tests from context_window_tests.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseInitializer] Failed to import context window tests: {ex.Message}");
        }
    }

    // DTOs for context_window_tests.json parsing
    private class ContextWindowTestSeedFile
    {
        public List<ContextWindowTestSeedItem> Tests { get; set; } = new();
    }

    private class ContextWindowTestSeedItem
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string FillerType { get; set; } = "mixed";
        public int BaseTargetTokens { get; set; }
        public int? BaseCheckpointCount { get; set; }
        public string? BuriedInstruction { get; set; }
        public List<ContextWindowCheckpointSeedItem>? Checkpoints { get; set; }
    }

    private class ContextWindowCheckpointSeedItem
    {
        public double? RelativePosition { get; set; }
        public string SecretWord { get; set; } = string.Empty;
        public string? CarrierSentence { get; set; }
    }

    // DTOs for instruction_tests.json parsing
    private class InstructionTestSeedFile
    {
        public List<InstructionTestSeedItem> Tests { get; set; } = new();
    }

    private class InstructionTestSeedItem
    {
        public string Prompt { get; set; } = string.Empty;
        public string? ExpectedResult { get; set; }
        public string ValidationType { get; set; } = "exact";
        public bool StrictOrder { get; set; }
        public string Category { get; set; } = "general";
        public List<string>? ExcludePatterns { get; set; }
        public List<string>? AllowedValues { get; set; }
        public int? ExpectedCount { get; set; }
    }

    // DTOs for reasoning_tests.json parsing
    private class ReasoningTestSeedFile
    {
        public List<ReasoningTestSeedItem> Tests { get; set; } = new();
    }

    private class ReasoningTestSeedItem
    {
        public string Category { get; set; } = "general";
        public string? Description { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
    }

    // DTOs for conversation_tests.json parsing
    private class ConversationTestSeedFile
    {
        public List<ConversationTestSeedItem> Tests { get; set; } = new();
    }

    private class ConversationTestSeedItem
    {
        public string Category { get; set; } = "general";
        public string? Description { get; set; }
        public string? SystemPrompt { get; set; }
        public List<ConversationTurnSeedItem> Turns { get; set; } = new();
        public List<string> JudgingCriteria { get; set; } = new();
    }

    private class ConversationTurnSeedItem
    {
        public string UserMessage { get; set; } = string.Empty;
        public string? ExpectedTheme { get; set; }
    }

    // DTOs for mcp_tool_tests.json parsing
    private class McpToolTestSeedFile
    {
        public List<McpToolTestSeedItem> Tests { get; set; } = new();
        public string? SystemPrompt { get; set; }
    }

    private class McpToolTestSeedItem
    {
        public string Category { get; set; } = "general";
        public string Description { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string? ToolSchema { get; set; }
        public string ScenarioPrompt { get; set; } = string.Empty;
        public object? ExpectedParams { get; set; }
        public string ResponseValidationType { get; set; } = "success";
        public List<string>? ExpectedResponsePatterns { get; set; }
        public bool ExecuteTool { get; set; } = true;
    }

    private async Task ImportInstructionTestsIfEmptyAsync(SqliteConnection connection)
    {
        var testCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM InstructionTests");
        if (testCount > 0) return;

        var searchPaths = new[]
        {
            // Output directory tests/ subdirectory (from csproj copy)
            Path.Combine(AppContext.BaseDirectory, "tests", "instruction_tests.json"),
            // From bin/Debug/net10.0 up to solution root then into src/config/tests
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "config", "tests", "instruction_tests.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "config", "tests", "instruction_tests.json"),
            "src/config/tests/instruction_tests.json"
        };

        var seedsPath = searchPaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(seedsPath))
        {
            Console.WriteLine("[DatabaseInitializer] No instruction_tests.json found, skipping import");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(seedsPath);
            var seedData = JsonSerializer.Deserialize<InstructionTestSeedFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (seedData?.Tests == null || seedData.Tests.Count == 0)
            {
                Console.WriteLine("[DatabaseInitializer] instruction_tests.json is empty or invalid");
                return;
            }

            var importedCount = 0;
            using var transaction = connection.BeginTransaction();

            foreach (var test in seedData.Tests)
            {
                // Serialize list fields to JSON
                string? excludeJson = test.ExcludePatterns != null ? JsonSerializer.Serialize(test.ExcludePatterns) : null;
                string? allowedJson = test.AllowedValues != null ? JsonSerializer.Serialize(test.AllowedValues) : null;

                await connection.ExecuteAsync("""
                    INSERT INTO InstructionTests (Category, Prompt, ExpectedResult, ValidationType, StrictOrder, ExcludePatterns, AllowedValues, ExpectedCount, IsActive, CreatedAt)
                    VALUES (@Category, @Prompt, @ExpectedResult, @ValidationType, @StrictOrder, @ExcludePatterns, @AllowedValues, @ExpectedCount, 1, datetime('now'))
                    """, new
                {
                    test.Category,
                    test.Prompt,
                    ExpectedResult = test.ExpectedResult ?? "",
                    test.ValidationType,
                    StrictOrder = test.StrictOrder ? 1 : 0,
                    ExcludePatterns = excludeJson,
                    AllowedValues = allowedJson,
                    test.ExpectedCount
                }, transaction);
                importedCount++;
            }

            transaction.Commit();

            Console.WriteLine($"[DatabaseInitializer] Imported {importedCount} instruction tests from instruction_tests.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseInitializer] Failed to import instruction tests: {ex.Message}");
        }
    }

    private async Task ImportReasoningTestsIfEmptyAsync(SqliteConnection connection)
    {
        var testCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ReasoningTests");
        if (testCount > 0) return;

        var searchPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tests", "reasoning_tests.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "config", "tests", "reasoning_tests.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "config", "tests", "reasoning_tests.json"),
            "src/config/tests/reasoning_tests.json"
        };

        var seedsPath = searchPaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(seedsPath))
        {
            Console.WriteLine("[DatabaseInitializer] No reasoning_tests.json found, skipping import");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(seedsPath);
            var seedData = JsonSerializer.Deserialize<ReasoningTestSeedFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (seedData?.Tests == null || seedData.Tests.Count == 0)
            {
                Console.WriteLine("[DatabaseInitializer] reasoning_tests.json is empty or invalid");
                return;
            }

            var importedCount = 0;
            using var transaction = connection.BeginTransaction();

            foreach (var test in seedData.Tests)
            {
                await connection.ExecuteAsync("""
                    INSERT INTO ReasoningTests (Category, Description, Prompt, CorrectAnswer, IsActive, CreatedAt)
                    VALUES (@Category, @Description, @Prompt, @CorrectAnswer, 1, datetime('now'))
                    """, new
                {
                    test.Category,
                    test.Description,
                    test.Prompt,
                    test.CorrectAnswer
                }, transaction);
                importedCount++;
            }

            transaction.Commit();
            Console.WriteLine($"[DatabaseInitializer] Imported {importedCount} reasoning tests from reasoning_tests.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseInitializer] Failed to import reasoning tests: {ex.Message}");
        }
    }

    private async Task ImportConversationTestsIfEmptyAsync(SqliteConnection connection)
    {
        var testCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ConversationTests");
        if (testCount > 0) return;

        var searchPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tests", "conversation_tests.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "config", "tests", "conversation_tests.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "config", "tests", "conversation_tests.json"),
            "src/config/tests/conversation_tests.json"
        };

        var seedsPath = searchPaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(seedsPath))
        {
            Console.WriteLine("[DatabaseInitializer] No conversation_tests.json found, skipping import");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(seedsPath);
            var seedData = JsonSerializer.Deserialize<ConversationTestSeedFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (seedData?.Tests == null || seedData.Tests.Count == 0)
            {
                Console.WriteLine("[DatabaseInitializer] conversation_tests.json is empty or invalid");
                return;
            }

            var importedCount = 0;
            using var transaction = connection.BeginTransaction();

            foreach (var test in seedData.Tests)
            {
                // Insert the test
                var testId = await connection.ExecuteScalarAsync<int>("""
                    INSERT INTO ConversationTests (Category, Description, SystemPrompt, IsActive, CreatedAt)
                    VALUES (@Category, @Description, @SystemPrompt, 1, datetime('now'));
                    SELECT last_insert_rowid();
                    """, new
                {
                    test.Category,
                    test.Description,
                    test.SystemPrompt
                }, transaction);

                // Insert turns
                var turnNumber = 1;
                foreach (var turn in test.Turns)
                {
                    await connection.ExecuteAsync("""
                        INSERT INTO ConversationTurns (TestId, TurnNumber, UserMessage, ExpectedTheme)
                        VALUES (@TestId, @TurnNumber, @UserMessage, @ExpectedTheme)
                        """, new
                    {
                        TestId = testId,
                        TurnNumber = turnNumber++,
                        turn.UserMessage,
                        turn.ExpectedTheme
                    }, transaction);
                }

                // Insert judging criteria
                var sortOrder = 0;
                foreach (var criterion in test.JudgingCriteria)
                {
                    await connection.ExecuteAsync("""
                        INSERT INTO ConversationJudgingCriteria (TestId, Criterion, SortOrder)
                        VALUES (@TestId, @Criterion, @SortOrder)
                        """, new
                    {
                        TestId = testId,
                        Criterion = criterion,
                        SortOrder = sortOrder++
                    }, transaction);
                }

                importedCount++;
            }

            transaction.Commit();
            Console.WriteLine($"[DatabaseInitializer] Imported {importedCount} conversation tests from conversation_tests.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseInitializer] Failed to import conversation tests: {ex.Message}");
        }
    }

    private async Task ImportMcpToolTestsIfEmptyAsync(SqliteConnection connection)
    {
        var testCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM McpToolTests");
        if (testCount > 0) return;

        var searchPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tests", "mcp_tool_tests.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "config", "tests", "mcp_tool_tests.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "config", "tests", "mcp_tool_tests.json"),
            "src/config/tests/mcp_tool_tests.json"
        };

        var seedsPath = searchPaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(seedsPath))
        {
            Console.WriteLine("[DatabaseInitializer] No mcp_tool_tests.json found, skipping import");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(seedsPath);
            var seedData = JsonSerializer.Deserialize<McpToolTestSeedFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (seedData?.Tests == null || seedData.Tests.Count == 0)
            {
                Console.WriteLine("[DatabaseInitializer] mcp_tool_tests.json is empty or invalid");
                return;
            }

            var importedCount = 0;
            using var transaction = connection.BeginTransaction();

            foreach (var test in seedData.Tests)
            {
                // Serialize ExpectedParams and ExpectedResponsePatterns to JSON
                string? expectedParamsJson = test.ExpectedParams != null
                    ? JsonSerializer.Serialize(test.ExpectedParams)
                    : null;
                string? expectedPatternsJson = test.ExpectedResponsePatterns != null
                    ? JsonSerializer.Serialize(test.ExpectedResponsePatterns)
                    : null;

                await connection.ExecuteAsync("""
                    INSERT INTO McpToolTests (Category, Description, ToolName, Command, ToolSchema, ScenarioPrompt,
                        ExpectedParams, ResponseValidationType, ExpectedResponsePatterns, ExecuteTool, IsActive, CreatedAt)
                    VALUES (@Category, @Description, @ToolName, @Command, @ToolSchema, @ScenarioPrompt,
                        @ExpectedParams, @ResponseValidationType, @ExpectedResponsePatterns, @ExecuteTool, 1, datetime('now'))
                    """, new
                {
                    test.Category,
                    test.Description,
                    test.ToolName,
                    test.Command,
                    test.ToolSchema,
                    test.ScenarioPrompt,
                    ExpectedParams = expectedParamsJson,
                    test.ResponseValidationType,
                    ExpectedResponsePatterns = expectedPatternsJson,
                    ExecuteTool = test.ExecuteTool ? 1 : 0
                }, transaction);
                importedCount++;
            }

            transaction.Commit();
            Console.WriteLine($"[DatabaseInitializer] Imported {importedCount} MCP tool tests from mcp_tool_tests.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseInitializer] Failed to import MCP tool tests: {ex.Message}");
        }
    }

    private async Task MigrateScoreColumnsToRealAsync(SqliteConnection connection)
    {
        // Check if ReasoningTestResults has INTEGER scores (check column type via pragma)
        var reasoningCols = await connection.QueryAsync<dynamic>(
            "SELECT name, type FROM pragma_table_info('ReasoningTestResults')");
        var overallScoreCol = reasoningCols.FirstOrDefault(c => c.name == "OverallScore");

        // If OverallScore exists and is INTEGER, migrate the table
        if (overallScoreCol != null && overallScoreCol.type?.ToString()?.ToUpper() == "INTEGER")
        {
            // Disable FK enforcement during migration to avoid constraint issues
            await connection.ExecuteAsync("PRAGMA foreign_keys = OFF");
            await connection.ExecuteAsync(@"
                -- Migrate ReasoningTestResults (no FK constraints to avoid migration issues)
                CREATE TABLE ReasoningTestResults_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RunId INTEGER NOT NULL,
                    ModelId INTEGER NOT NULL,
                    TestId INTEGER NOT NULL,
                    Response TEXT NOT NULL,
                    OverallScore REAL,
                    CorrectAnswerScore REAL,
                    LogicalStepsScore REAL,
                    ClarityScore REAL,
                    JudgeReasoning TEXT,
                    JudgeModelId INTEGER,
                    FirstTokenMs REAL,
                    TotalMs REAL NOT NULL,
                    TokensPerSec REAL,
                    PromptTokens INTEGER,
                    CompletionTokens INTEGER,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                INSERT INTO ReasoningTestResults_new SELECT * FROM ReasoningTestResults;
                DROP TABLE ReasoningTestResults;
                ALTER TABLE ReasoningTestResults_new RENAME TO ReasoningTestResults;
                CREATE INDEX IF NOT EXISTS idx_reasoning_results_run ON ReasoningTestResults(RunId);
                CREATE INDEX IF NOT EXISTS idx_reasoning_results_model ON ReasoningTestResults(ModelId);
            ");
            await connection.ExecuteAsync("PRAGMA foreign_keys = ON");
        }

        // Check ConversationTestResults
        var convCols = await connection.QueryAsync<dynamic>(
            "SELECT name, type FROM pragma_table_info('ConversationTestResults')");
        var convOverallCol = convCols.FirstOrDefault(c => c.name == "OverallScore");

        if (convOverallCol != null && convOverallCol.type?.ToString()?.ToUpper() == "INTEGER")
        {
            await connection.ExecuteAsync("PRAGMA foreign_keys = OFF");
            await connection.ExecuteAsync(@"
                -- Migrate ConversationTestResults (no FK constraints)
                CREATE TABLE ConversationTestResults_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RunId INTEGER NOT NULL,
                    ModelId INTEGER NOT NULL,
                    TestId INTEGER NOT NULL,
                    OverallScore REAL,
                    TopicCoherence REAL,
                    ConversationalTone REAL,
                    ContextRetention REAL,
                    Helpfulness REAL,
                    JudgeReasoning TEXT,
                    JudgeModelId INTEGER,
                    TotalMs REAL,
                    TokensPerSec REAL,
                    PromptTokens INTEGER,
                    CompletionTokens INTEGER,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                INSERT INTO ConversationTestResults_new
                    (Id, RunId, ModelId, TestId, OverallScore, TopicCoherence, ConversationalTone, ContextRetention, Helpfulness, JudgeReasoning, JudgeModelId, TotalMs, CreatedAt)
                    SELECT Id, RunId, ModelId, TestId, OverallScore, TopicCoherence, ConversationalTone, ContextRetention, Helpfulness, JudgeReasoning, JudgeModelId, TotalMs, CreatedAt FROM ConversationTestResults;
                DROP TABLE ConversationTestResults;
                ALTER TABLE ConversationTestResults_new RENAME TO ConversationTestResults;
                CREATE INDEX IF NOT EXISTS idx_conversation_results_run ON ConversationTestResults(RunId);
            ");
            await connection.ExecuteAsync("PRAGMA foreign_keys = ON");
        }

        // Check GenerationRatings
        var ratingCols = await connection.QueryAsync<dynamic>(
            "SELECT name, type FROM pragma_table_info('GenerationRatings')");
        var scoreCol = ratingCols.FirstOrDefault(c => c.name == "Score");

        if (scoreCol != null && scoreCol.type?.ToString()?.ToUpper() == "INTEGER")
        {
            await connection.ExecuteAsync("PRAGMA foreign_keys = OFF");
            await connection.ExecuteAsync(@"
                -- Migrate GenerationRatings (no FK constraints)
                CREATE TABLE GenerationRatings_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ResultId INTEGER NOT NULL,
                    JudgeModelId INTEGER NOT NULL,
                    Score REAL NOT NULL,
                    Reasoning TEXT,
                    IsBaseJudge INTEGER DEFAULT 0,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                INSERT INTO GenerationRatings_new SELECT * FROM GenerationRatings;
                DROP TABLE GenerationRatings;
                ALTER TABLE GenerationRatings_new RENAME TO GenerationRatings;
                CREATE INDEX IF NOT EXISTS idx_generation_ratings_result ON GenerationRatings(ResultId);
            ");
            await connection.ExecuteAsync("PRAGMA foreign_keys = ON");
        }
    }

    private async Task AddUniqueResultIndexesAsync(SqliteConnection connection)
    {
        // Add unique indexes to prevent duplicate results (RunId + ModelId + TestId/SeedId)
        // This also removes duplicates, keeping the latest (highest Id) result

        // Check if unique indexes already exist
        var indexes = await connection.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='index' AND name='idx_instruction_results_unique'");
        if (indexes.Any()) return; // Already migrated

        // For InstructionTestResults
        await connection.ExecuteAsync(@"
            DELETE FROM InstructionTestResults
            WHERE Id NOT IN (
                SELECT MAX(Id)
                FROM InstructionTestResults
                GROUP BY RunId, ModelId, TestId
            );
            CREATE UNIQUE INDEX IF NOT EXISTS idx_instruction_results_unique
            ON InstructionTestResults(RunId, ModelId, TestId);
        ");

        // For ReasoningTestResults
        await connection.ExecuteAsync(@"
            DELETE FROM ReasoningTestResults
            WHERE Id NOT IN (
                SELECT MAX(Id)
                FROM ReasoningTestResults
                GROUP BY RunId, ModelId, TestId
            );
            CREATE UNIQUE INDEX IF NOT EXISTS idx_reasoning_results_unique
            ON ReasoningTestResults(RunId, ModelId, TestId);
        ");

        // For ConversationTestResults
        await connection.ExecuteAsync(@"
            DELETE FROM ConversationTestResults
            WHERE Id NOT IN (
                SELECT MAX(Id)
                FROM ConversationTestResults
                GROUP BY RunId, ModelId, TestId
            );
            CREATE UNIQUE INDEX IF NOT EXISTS idx_conversation_results_unique
            ON ConversationTestResults(RunId, ModelId, TestId);
        ");

        // For GenerationResults
        await connection.ExecuteAsync(@"
            DELETE FROM GenerationResults
            WHERE Id NOT IN (
                SELECT MAX(Id)
                FROM GenerationResults
                GROUP BY RunId, ModelId, SeedId
            );
            CREATE UNIQUE INDEX IF NOT EXISTS idx_generation_results_unique
            ON GenerationResults(RunId, ModelId, SeedId);
        ");

        // For ContextWindowTestResults
        await connection.ExecuteAsync(@"
            DELETE FROM ContextWindowTestResults
            WHERE Id NOT IN (
                SELECT MAX(Id)
                FROM ContextWindowTestResults
                GROUP BY RunId, ModelId, TestId
            );
            CREATE UNIQUE INDEX IF NOT EXISTS idx_context_results_unique
            ON ContextWindowTestResults(RunId, ModelId, TestId);
        ");
    }

    private const string CreateProvidersTables = """
        CREATE TABLE IF NOT EXISTS Providers (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            BaseUrl TEXT NOT NULL,
            AuthToken TEXT,
            UseAuth INTEGER DEFAULT 0,
            TimeoutMinutes INTEGER DEFAULT 10,
            IsActive INTEGER DEFAULT 1,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
            UpdatedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS Models (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProviderId INTEGER NOT NULL REFERENCES Providers(Id),
            Identifier TEXT NOT NULL,
            DisplayName TEXT,
            IsDisabled INTEGER DEFAULT 0,
            IsAvailable INTEGER DEFAULT 1,
            IsDeleted INTEGER DEFAULT 0,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
            UNIQUE(ProviderId, Identifier)
        );
        """;

    private const string CreateConfigTables = """
        CREATE TABLE IF NOT EXISTS TestSuiteConfigs (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Description TEXT,
            RunPromptTests INTEGER DEFAULT 1,
            RunContextWindowTests INTEGER DEFAULT 0,
            RunConversationTests INTEGER DEFAULT 1,
            RunQualificationTests INTEGER DEFAULT 1,
            MaxInstructionTests INTEGER DEFAULT 10,
            MaxReasoningTests INTEGER DEFAULT 10,
            MaxConversationTests INTEGER DEFAULT 10,
            RunMcpToolTests INTEGER DEFAULT 0,
            MaxMcpToolTests INTEGER DEFAULT 10,
            EchoMcpBaseUrl TEXT,
            EchoMcpToken TEXT,
            FetchSchemasFromEchoMcp INTEGER DEFAULT 1,
            McpTransportType TEXT DEFAULT 'sse',
            McpServerUrl TEXT,
            McpServerCommand TEXT,
            McpServerArgs TEXT,
            HighQualityThreshold REAL DEFAULT 7.5,
            InstructionPassThreshold REAL DEFAULT 0.8,
            TopJudgeCount INTEGER DEFAULT 2,
            ContextWindowLevel TEXT DEFAULT 'shallow',
            ContextWindowTestType TEXT DEFAULT 'Multi-Needle Recall',
            ContextWindowTargetTokens INTEGER DEFAULT 32000,
            ContextWindowProbeCount INTEGER DEFAULT 10,
            ContextWindowCheckpoints INTEGER DEFAULT 4,
            ContextWindowMaxTests INTEGER DEFAULT 5,
            ContextWindowTestIds TEXT,
            DegradationGraceful INTEGER DEFAULT 100000,
            DegradationModerate INTEGER DEFAULT 60000,
            DegradationSudden INTEGER DEFAULT 30000,
            TargetSeedCount INTEGER DEFAULT 50,
            OverwriteSeeds INTEGER DEFAULT 1,
            GlobalTemperature REAL DEFAULT 0.5,
            GlobalTopP REAL DEFAULT 0.9,
            GlobalMaxTokens INTEGER DEFAULT 512,
            MaxParallelRequests INTEGER DEFAULT 1,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
            UpdatedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS CategorySettings (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ConfigId INTEGER NOT NULL REFERENCES TestSuiteConfigs(Id) ON DELETE CASCADE,
            Category TEXT NOT NULL,
            Temperature REAL,
            TopP REAL,
            MaxTokens INTEGER,
            SystemPrompt TEXT,
            Weight REAL DEFAULT 0.25,
            UNIQUE(ConfigId, Category)
        );

        CREATE TABLE IF NOT EXISTS TestTypeLimits (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ConfigId INTEGER NOT NULL REFERENCES TestSuiteConfigs(Id) ON DELETE CASCADE,
            TestType TEXT NOT NULL,
            Category TEXT NOT NULL,
            MaxTests INTEGER NOT NULL,
            Temperature REAL,
            TopP REAL,
            MaxTokens INTEGER,
            PassThreshold REAL,
            MinScore REAL,
            UNIQUE(ConfigId, TestType, Category)
        );
        """;

    private const string CreateTestDefinitionTables = """
        CREATE TABLE IF NOT EXISTS InstructionTests (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Category TEXT NOT NULL,
            Prompt TEXT NOT NULL,
            ExpectedResult TEXT NOT NULL,
            ValidationType TEXT NOT NULL DEFAULT 'exact',
            StrictOrder INTEGER DEFAULT 0,
            IsActive INTEGER DEFAULT 1,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS ReasoningTests (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Category TEXT NOT NULL,
            Description TEXT,
            Prompt TEXT NOT NULL,
            CorrectAnswer TEXT NOT NULL,
            IsActive INTEGER DEFAULT 1,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS ConversationTests (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Category TEXT NOT NULL,
            Description TEXT,
            SystemPrompt TEXT,
            IsActive INTEGER DEFAULT 1,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS ConversationTurns (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            TestId INTEGER NOT NULL REFERENCES ConversationTests(Id) ON DELETE CASCADE,
            TurnNumber INTEGER NOT NULL,
            UserMessage TEXT NOT NULL,
            ExpectedTheme TEXT,
            UNIQUE(TestId, TurnNumber)
        );

        CREATE TABLE IF NOT EXISTS ConversationJudgingCriteria (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            TestId INTEGER NOT NULL REFERENCES ConversationTests(Id) ON DELETE CASCADE,
            Criterion TEXT NOT NULL,
            SortOrder INTEGER DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS ContextWindowTests (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Description TEXT,
            FillerType TEXT DEFAULT 'mixed',
            BaseTargetTokens INTEGER NOT NULL,
            BaseCheckpointCount INTEGER NOT NULL,
            BuriedInstruction TEXT,
            IsActive INTEGER DEFAULT 1,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS ContextWindowCheckpoints (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            TestId INTEGER NOT NULL REFERENCES ContextWindowTests(Id) ON DELETE CASCADE,
            TargetTokenPosition INTEGER NOT NULL,
            SecretWord TEXT NOT NULL,
            CarrierSentence TEXT,
            SortOrder INTEGER DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS McpToolTests (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Category TEXT NOT NULL,
            Description TEXT,
            ToolName TEXT NOT NULL,
            Command TEXT NOT NULL,
            ToolSchema TEXT,
            ScenarioPrompt TEXT NOT NULL,
            ExpectedParams TEXT,
            ResponseValidationType TEXT DEFAULT 'success',
            ExpectedResponsePatterns TEXT,
            ExecuteTool INTEGER DEFAULT 1,
            IsActive INTEGER DEFAULT 1,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );
        """;

    private const string CreateSeedTables = """
        CREATE TABLE IF NOT EXISTS Seeds (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Category TEXT NOT NULL,
            Instruction TEXT NOT NULL,
            Temperature REAL,
            TopP REAL,
            MaxTokens INTEGER,
            IsAugmented INTEGER DEFAULT 0,
            SourceSeedId INTEGER REFERENCES Seeds(Id),
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS SeedTags (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SeedId INTEGER NOT NULL REFERENCES Seeds(Id) ON DELETE CASCADE,
            Tag TEXT NOT NULL,
            UNIQUE(SeedId, Tag)
        );
        """;

    private const string CreateBenchmarkRunTables = """
        CREATE TABLE IF NOT EXISTS BenchmarkRuns (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT,
            ConfigId INTEGER NOT NULL REFERENCES TestSuiteConfigs(Id),
            ProviderId INTEGER NOT NULL REFERENCES Providers(Id),
            Status TEXT DEFAULT 'pending',
            StartedAt TEXT,
            CompletedAt TEXT,
            TotalModels INTEGER DEFAULT 0,
            TotalTests INTEGER DEFAULT 0,
            CompletedTests INTEGER DEFAULT 0,
            ErrorCount INTEGER DEFAULT 0,
            BaseJudgeModelId INTEGER REFERENCES Models(Id),
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS RunLogs (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RunId INTEGER NOT NULL REFERENCES BenchmarkRuns(Id) ON DELETE CASCADE,
            Level TEXT NOT NULL,
            Message TEXT NOT NULL,
            ModelName TEXT,
            Timestamp TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS BenchmarkRunModels (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RunId INTEGER NOT NULL REFERENCES BenchmarkRuns(Id) ON DELETE CASCADE,
            ModelId INTEGER NOT NULL REFERENCES Models(Id),
            Status TEXT DEFAULT 'pending',
            QualificationPassed INTEGER,
            InstructionPassRate REAL,
            InstructionStrictPassRate REAL,
            ReasoningAvgScore REAL,
            IsBaseJudge INTEGER DEFAULT 0,
            IsAutoJudge INTEGER DEFAULT 0,
            UNIQUE(RunId, ModelId)
        );

        CREATE TABLE IF NOT EXISTS BenchmarkAutoJudges (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RunId INTEGER NOT NULL REFERENCES BenchmarkRuns(Id) ON DELETE CASCADE,
            ModelId INTEGER NOT NULL REFERENCES Models(Id),
            SelectionReason TEXT,
            UNIQUE(RunId, ModelId)
        );
        """;

    private const string CreateResultTables = """
        CREATE TABLE IF NOT EXISTS InstructionTestResults (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RunId INTEGER NOT NULL,
            ModelId INTEGER NOT NULL,
            TestId INTEGER NOT NULL,
            Passed INTEGER NOT NULL,
            StrictPass INTEGER NOT NULL,
            LenientPass INTEGER DEFAULT 0,
            Response TEXT,
            FailureReason TEXT,
            FirstTokenMs REAL,
            TotalMs REAL NOT NULL,
            TokensPerSec REAL,
            PromptTokens INTEGER,
            CompletionTokens INTEGER,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS ReasoningTestResults (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RunId INTEGER NOT NULL,
            ModelId INTEGER NOT NULL,
            TestId INTEGER NOT NULL,
            Response TEXT NOT NULL,
            OverallScore REAL,
            CorrectAnswerScore REAL,
            LogicalStepsScore REAL,
            ClarityScore REAL,
            JudgeReasoning TEXT,
            JudgeModelId INTEGER,
            FirstTokenMs REAL,
            TotalMs REAL NOT NULL,
            TokensPerSec REAL,
            PromptTokens INTEGER,
            CompletionTokens INTEGER,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS ConversationTestResults (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RunId INTEGER NOT NULL,
            ModelId INTEGER NOT NULL,
            TestId INTEGER NOT NULL,
            OverallScore REAL,
            TopicCoherence REAL,
            ConversationalTone REAL,
            ContextRetention REAL,
            Helpfulness REAL,
            JudgeReasoning TEXT,
            JudgeModelId INTEGER,
            TotalMs REAL,
            TokensPerSec REAL,
            PromptTokens INTEGER,
            CompletionTokens INTEGER,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS ConversationExchanges (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ResultId INTEGER NOT NULL,
            TurnNumber INTEGER NOT NULL,
            UserMessage TEXT NOT NULL,
            ModelResponse TEXT NOT NULL,
            FirstTokenMs REAL,
            TotalMs REAL,
            TokensPerSec REAL,
            PromptTokens INTEGER,
            CompletionTokens INTEGER,
            UNIQUE(ResultId, TurnNumber)
        );

        CREATE TABLE IF NOT EXISTS ContextWindowTestResults (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RunId INTEGER NOT NULL,
            ModelId INTEGER NOT NULL,
            TestId INTEGER NOT NULL,
            MaxReliableTokens INTEGER,
            CheckpointAccuracy REAL,
            DegradationPattern TEXT,
            AutopsyText TEXT,
            TotalMs REAL,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS ContextWindowProbes (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ResultId INTEGER NOT NULL,
            CheckpointId INTEGER,
            TokenPosition INTEGER NOT NULL,
            Found INTEGER NOT NULL,
            Hallucinated INTEGER DEFAULT 0,
            Response TEXT,
            TotalMs REAL
        );

        CREATE TABLE IF NOT EXISTS GenerationResults (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RunId INTEGER NOT NULL,
            ModelId INTEGER NOT NULL,
            SeedId INTEGER NOT NULL,
            Category TEXT NOT NULL,
            Response TEXT NOT NULL,
            Temperature REAL,
            TopP REAL,
            MaxTokens INTEGER,
            FirstTokenMs REAL,
            TotalMs REAL NOT NULL,
            TokensPerSec REAL,
            PromptTokens INTEGER,
            CompletionTokens INTEGER,
            AvgScore REAL,
            IsHighQuality INTEGER DEFAULT 0,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS GenerationRatings (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ResultId INTEGER NOT NULL,
            JudgeModelId INTEGER NOT NULL,
            Score REAL NOT NULL,
            Reasoning TEXT,
            IsBaseJudge INTEGER DEFAULT 0,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS McpToolTestResults (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RunId INTEGER NOT NULL,
            ModelId INTEGER NOT NULL,
            TestId INTEGER NOT NULL,
            JsonValid INTEGER NOT NULL,
            CorrectTool INTEGER NOT NULL,
            CorrectCommand INTEGER NOT NULL,
            ParamsValid INTEGER NOT NULL,
            ModelResponse TEXT,
            ParsedToolCall TEXT,
            JsonParseError TEXT,
            ToolExecuted INTEGER,
            ExecutionSuccess INTEGER,
            ToolResponse TEXT,
            ExecutionError TEXT,
            ResponseValidated INTEGER,
            ValidationReason TEXT,
            Passed INTEGER NOT NULL,
            TotalMs REAL NOT NULL,
            ExecutionMs REAL,
            TokensPerSec REAL,
            PromptTokens INTEGER,
            CompletionTokens INTEGER,
            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
        );
        """;

    private const string CreateIndexes = """
        CREATE INDEX IF NOT EXISTS idx_models_provider ON Models(ProviderId);
        CREATE INDEX IF NOT EXISTS idx_category_settings_config ON CategorySettings(ConfigId);
        CREATE INDEX IF NOT EXISTS idx_test_type_limits_config ON TestTypeLimits(ConfigId);
        CREATE INDEX IF NOT EXISTS idx_conversation_turns_test ON ConversationTurns(TestId);
        CREATE INDEX IF NOT EXISTS idx_conversation_criteria_test ON ConversationJudgingCriteria(TestId);
        CREATE INDEX IF NOT EXISTS idx_context_checkpoints_test ON ContextWindowCheckpoints(TestId);
        CREATE INDEX IF NOT EXISTS idx_seed_tags_seed ON SeedTags(SeedId);
        CREATE INDEX IF NOT EXISTS idx_runs_status ON BenchmarkRuns(Status);
        CREATE INDEX IF NOT EXISTS idx_runs_config ON BenchmarkRuns(ConfigId);
        CREATE INDEX IF NOT EXISTS idx_run_logs_run ON RunLogs(RunId);
        CREATE INDEX IF NOT EXISTS idx_run_models_run ON BenchmarkRunModels(RunId);
        CREATE INDEX IF NOT EXISTS idx_instruction_results_run ON InstructionTestResults(RunId);
        CREATE INDEX IF NOT EXISTS idx_instruction_results_model ON InstructionTestResults(ModelId);
        CREATE INDEX IF NOT EXISTS idx_reasoning_results_run ON ReasoningTestResults(RunId);
        CREATE INDEX IF NOT EXISTS idx_reasoning_results_model ON ReasoningTestResults(ModelId);
        CREATE INDEX IF NOT EXISTS idx_conversation_results_run ON ConversationTestResults(RunId);
        CREATE INDEX IF NOT EXISTS idx_conversation_exchanges_result ON ConversationExchanges(ResultId);
        CREATE INDEX IF NOT EXISTS idx_context_results_run ON ContextWindowTestResults(RunId);
        CREATE INDEX IF NOT EXISTS idx_context_probes_result ON ContextWindowProbes(ResultId);
        CREATE INDEX IF NOT EXISTS idx_generation_results_run ON GenerationResults(RunId);
        CREATE INDEX IF NOT EXISTS idx_generation_results_model ON GenerationResults(ModelId);
        CREATE INDEX IF NOT EXISTS idx_generation_results_seed ON GenerationResults(SeedId);
        CREATE INDEX IF NOT EXISTS idx_generation_ratings_result ON GenerationRatings(ResultId);
        CREATE INDEX IF NOT EXISTS idx_mcp_tool_results_run ON McpToolTestResults(RunId);
        CREATE INDEX IF NOT EXISTS idx_mcp_tool_results_model ON McpToolTestResults(ModelId);
        CREATE UNIQUE INDEX IF NOT EXISTS idx_mcp_tool_results_unique ON McpToolTestResults(RunId, ModelId, TestId);
        """;
}
