using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Squirmify.Data.Database;

public class DatabaseInitializer
{
	private class BaseSeedItem
	{
		public string Instruction { get; set; } = string.Empty;

		public List<string>? Tags { get; set; }
	}

	private class ContextWindowTestSeedFile
	{
		public List<ContextWindowTestSeedItem> Tests { get; set; } = new List<ContextWindowTestSeedItem>();
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

	private class InstructionTestSeedFile
	{
		public List<InstructionTestSeedItem> Tests { get; set; } = new List<InstructionTestSeedItem>();
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

	private class ReasoningTestSeedFile
	{
		public List<ReasoningTestSeedItem> Tests { get; set; } = new List<ReasoningTestSeedItem>();
	}

	private class ReasoningTestSeedItem
	{
		public string Category { get; set; } = "general";

		public string? Description { get; set; }

		public string Prompt { get; set; } = string.Empty;

		public string CorrectAnswer { get; set; } = string.Empty;
	}

	private class ConversationTestSeedFile
	{
		public List<ConversationTestSeedItem> Tests { get; set; } = new List<ConversationTestSeedItem>();
	}

	private class ConversationTestSeedItem
	{
		public string Category { get; set; } = "general";

		public string? Description { get; set; }

		public string? SystemPrompt { get; set; }

		public List<ConversationTurnSeedItem> Turns { get; set; } = new List<ConversationTurnSeedItem>();

		public List<string> JudgingCriteria { get; set; } = new List<string>();
	}

	private class ConversationTurnSeedItem
	{
		public string UserMessage { get; set; } = string.Empty;

		public string? ExpectedTheme { get; set; }
	}

	private class McpToolTestSeedFile
	{
		public List<McpToolTestSeedItem> Tests { get; set; } = new List<McpToolTestSeedItem>();

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

	private readonly string _connectionString;

	private readonly string? _seedsJsonPath;

	private const string CreateProvidersTables = "CREATE TABLE IF NOT EXISTS Providers (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Name TEXT NOT NULL,\r\n    BaseUrl TEXT NOT NULL,\r\n    AuthToken TEXT,\r\n    UseAuth INTEGER DEFAULT 0,\r\n    TimeoutMinutes INTEGER DEFAULT 10,\r\n    IsActive INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,\r\n    UpdatedAt TEXT\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS Models (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    ProviderId INTEGER NOT NULL REFERENCES Providers(Id),\r\n    Identifier TEXT NOT NULL,\r\n    DisplayName TEXT,\r\n    IsDisabled INTEGER DEFAULT 0,\r\n    IsAvailable INTEGER DEFAULT 1,\r\n    IsDeleted INTEGER DEFAULT 0,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,\r\n    UNIQUE(ProviderId, Identifier)\r\n);";

	private const string CreateConfigTables = "CREATE TABLE IF NOT EXISTS TestSuiteConfigs (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Name TEXT NOT NULL,\r\n    Description TEXT,\r\n    RunPromptTests INTEGER DEFAULT 1,\r\n    RunContextWindowTests INTEGER DEFAULT 0,\r\n    RunConversationTests INTEGER DEFAULT 1,\r\n    RunQualificationTests INTEGER DEFAULT 1,\r\n    MaxInstructionTests INTEGER DEFAULT 10,\r\n    MaxReasoningTests INTEGER DEFAULT 10,\r\n    MaxConversationTests INTEGER DEFAULT 10,\r\n    RunMcpToolTests INTEGER DEFAULT 0,\r\n    MaxMcpToolTests INTEGER DEFAULT 10,\r\n    EchoMcpBaseUrl TEXT,\r\n    EchoMcpToken TEXT,\r\n    FetchSchemasFromEchoMcp INTEGER DEFAULT 1,\r\n    McpTransportType TEXT DEFAULT 'sse',\r\n    McpServerUrl TEXT,\r\n    McpServerCommand TEXT,\r\n    McpServerArgs TEXT,\r\n    HighQualityThreshold REAL DEFAULT 7.5,\r\n    InstructionPassThreshold REAL DEFAULT 0.8,\r\n    TopJudgeCount INTEGER DEFAULT 2,\r\n    ContextWindowLevel TEXT DEFAULT 'shallow',\r\n    ContextWindowTestType TEXT DEFAULT 'Multi-Needle Recall',\r\n    ContextWindowTargetTokens INTEGER DEFAULT 32000,\r\n    ContextWindowProbeCount INTEGER DEFAULT 10,\r\n    ContextWindowCheckpoints INTEGER DEFAULT 4,\r\n    ContextWindowMaxTests INTEGER DEFAULT 5,\r\n    ContextWindowTestIds TEXT,\r\n    DegradationGraceful INTEGER DEFAULT 100000,\r\n    DegradationModerate INTEGER DEFAULT 60000,\r\n    DegradationSudden INTEGER DEFAULT 30000,\r\n    TargetSeedCount INTEGER DEFAULT 50,\r\n    OverwriteSeeds INTEGER DEFAULT 1,\r\n    GlobalTemperature REAL DEFAULT 0.5,\r\n    GlobalTopP REAL DEFAULT 0.9,\r\n    GlobalMaxTokens INTEGER DEFAULT 512,\r\n    MaxParallelRequests INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,\r\n    UpdatedAt TEXT\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS CategorySettings (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    ConfigId INTEGER NOT NULL REFERENCES TestSuiteConfigs(Id) ON DELETE CASCADE,\r\n    Category TEXT NOT NULL,\r\n    Temperature REAL,\r\n    TopP REAL,\r\n    MaxTokens INTEGER,\r\n    SystemPrompt TEXT,\r\n    Weight REAL DEFAULT 0.25,\r\n    UNIQUE(ConfigId, Category)\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS TestTypeLimits (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    ConfigId INTEGER NOT NULL REFERENCES TestSuiteConfigs(Id) ON DELETE CASCADE,\r\n    TestType TEXT NOT NULL,\r\n    Category TEXT NOT NULL,\r\n    MaxTests INTEGER NOT NULL,\r\n    Temperature REAL,\r\n    TopP REAL,\r\n    MaxTokens INTEGER,\r\n    PassThreshold REAL,\r\n    MinScore REAL,\r\n    UNIQUE(ConfigId, TestType, Category)\r\n);";

	private const string CreateTestDefinitionTables = "CREATE TABLE IF NOT EXISTS InstructionTests (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Category TEXT NOT NULL,\r\n    Prompt TEXT NOT NULL,\r\n    ExpectedResult TEXT NOT NULL,\r\n    ValidationType TEXT NOT NULL DEFAULT 'exact',\r\n    StrictOrder INTEGER DEFAULT 0,\r\n    IsActive INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ReasoningTests (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Category TEXT NOT NULL,\r\n    Description TEXT,\r\n    Prompt TEXT NOT NULL,\r\n    CorrectAnswer TEXT NOT NULL,\r\n    IsActive INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ConversationTests (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Category TEXT NOT NULL,\r\n    Description TEXT,\r\n    SystemPrompt TEXT,\r\n    IsActive INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ConversationTurns (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    TestId INTEGER NOT NULL REFERENCES ConversationTests(Id) ON DELETE CASCADE,\r\n    TurnNumber INTEGER NOT NULL,\r\n    UserMessage TEXT NOT NULL,\r\n    ExpectedTheme TEXT,\r\n    UNIQUE(TestId, TurnNumber)\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ConversationJudgingCriteria (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    TestId INTEGER NOT NULL REFERENCES ConversationTests(Id) ON DELETE CASCADE,\r\n    Criterion TEXT NOT NULL,\r\n    SortOrder INTEGER DEFAULT 0\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ContextWindowTests (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Name TEXT NOT NULL,\r\n    Description TEXT,\r\n    FillerType TEXT DEFAULT 'mixed',\r\n    BaseTargetTokens INTEGER NOT NULL,\r\n    BaseCheckpointCount INTEGER NOT NULL,\r\n    BuriedInstruction TEXT,\r\n    IsActive INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ContextWindowCheckpoints (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    TestId INTEGER NOT NULL REFERENCES ContextWindowTests(Id) ON DELETE CASCADE,\r\n    TargetTokenPosition INTEGER NOT NULL,\r\n    SecretWord TEXT NOT NULL,\r\n    CarrierSentence TEXT,\r\n    SortOrder INTEGER DEFAULT 0\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS McpToolTests (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Category TEXT NOT NULL,\r\n    Description TEXT,\r\n    ToolName TEXT NOT NULL,\r\n    Command TEXT NOT NULL,\r\n    ToolSchema TEXT,\r\n    ScenarioPrompt TEXT NOT NULL,\r\n    ExpectedParams TEXT,\r\n    ResponseValidationType TEXT DEFAULT 'success',\r\n    ExpectedResponsePatterns TEXT,\r\n    ExecuteTool INTEGER DEFAULT 1,\r\n    IsActive INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);";

	private const string CreateSeedTables = "CREATE TABLE IF NOT EXISTS Seeds (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Category TEXT NOT NULL,\r\n    Instruction TEXT NOT NULL,\r\n    Temperature REAL,\r\n    TopP REAL,\r\n    MaxTokens INTEGER,\r\n    IsAugmented INTEGER DEFAULT 0,\r\n    SourceSeedId INTEGER REFERENCES Seeds(Id),\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS SeedTags (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    SeedId INTEGER NOT NULL REFERENCES Seeds(Id) ON DELETE CASCADE,\r\n    Tag TEXT NOT NULL,\r\n    UNIQUE(SeedId, Tag)\r\n);";

	private const string CreateBenchmarkRunTables = "CREATE TABLE IF NOT EXISTS BenchmarkRuns (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Name TEXT,\r\n    ConfigId INTEGER NOT NULL REFERENCES TestSuiteConfigs(Id),\r\n    ProviderId INTEGER NOT NULL REFERENCES Providers(Id),\r\n    Status TEXT DEFAULT 'pending',\r\n    StartedAt TEXT,\r\n    CompletedAt TEXT,\r\n    TotalModels INTEGER DEFAULT 0,\r\n    TotalTests INTEGER DEFAULT 0,\r\n    CompletedTests INTEGER DEFAULT 0,\r\n    ErrorCount INTEGER DEFAULT 0,\r\n    BaseJudgeModelId INTEGER REFERENCES Models(Id),\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS RunLogs (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL REFERENCES BenchmarkRuns(Id) ON DELETE CASCADE,\r\n    Level TEXT NOT NULL,\r\n    Message TEXT NOT NULL,\r\n    ModelName TEXT,\r\n    Timestamp TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS BenchmarkRunModels (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL REFERENCES BenchmarkRuns(Id) ON DELETE CASCADE,\r\n    ModelId INTEGER NOT NULL REFERENCES Models(Id),\r\n    Status TEXT DEFAULT 'pending',\r\n    QualificationPassed INTEGER,\r\n    InstructionPassRate REAL,\r\n    InstructionStrictPassRate REAL,\r\n    ReasoningAvgScore REAL,\r\n    IsBaseJudge INTEGER DEFAULT 0,\r\n    IsAutoJudge INTEGER DEFAULT 0,\r\n    UNIQUE(RunId, ModelId)\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS BenchmarkAutoJudges (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL REFERENCES BenchmarkRuns(Id) ON DELETE CASCADE,\r\n    ModelId INTEGER NOT NULL REFERENCES Models(Id),\r\n    SelectionReason TEXT,\r\n    UNIQUE(RunId, ModelId)\r\n);";

	private const string CreateResultTables = "CREATE TABLE IF NOT EXISTS InstructionTestResults (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL,\r\n    ModelId INTEGER NOT NULL,\r\n    TestId INTEGER NOT NULL,\r\n    Passed INTEGER NOT NULL,\r\n    StrictPass INTEGER NOT NULL,\r\n    LenientPass INTEGER DEFAULT 0,\r\n    Response TEXT,\r\n    FailureReason TEXT,\r\n    FirstTokenMs REAL,\r\n    TotalMs REAL NOT NULL,\r\n    TokensPerSec REAL,\r\n    PromptTokens INTEGER,\r\n    CompletionTokens INTEGER,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ReasoningTestResults (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL,\r\n    ModelId INTEGER NOT NULL,\r\n    TestId INTEGER NOT NULL,\r\n    Response TEXT NOT NULL,\r\n    OverallScore REAL,\r\n    CorrectAnswerScore REAL,\r\n    LogicalStepsScore REAL,\r\n    ClarityScore REAL,\r\n    JudgeReasoning TEXT,\r\n    JudgeModelId INTEGER,\r\n    FirstTokenMs REAL,\r\n    TotalMs REAL NOT NULL,\r\n    TokensPerSec REAL,\r\n    PromptTokens INTEGER,\r\n    CompletionTokens INTEGER,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ConversationTestResults (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL,\r\n    ModelId INTEGER NOT NULL,\r\n    TestId INTEGER NOT NULL,\r\n    OverallScore REAL,\r\n    TopicCoherence REAL,\r\n    ConversationalTone REAL,\r\n    ContextRetention REAL,\r\n    Helpfulness REAL,\r\n    JudgeReasoning TEXT,\r\n    JudgeModelId INTEGER,\r\n    TotalMs REAL,\r\n    TokensPerSec REAL,\r\n    PromptTokens INTEGER,\r\n    CompletionTokens INTEGER,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ConversationExchanges (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    ResultId INTEGER NOT NULL,\r\n    TurnNumber INTEGER NOT NULL,\r\n    UserMessage TEXT NOT NULL,\r\n    ModelResponse TEXT NOT NULL,\r\n    FirstTokenMs REAL,\r\n    TotalMs REAL,\r\n    TokensPerSec REAL,\r\n    PromptTokens INTEGER,\r\n    CompletionTokens INTEGER,\r\n    UNIQUE(ResultId, TurnNumber)\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ContextWindowTestResults (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL,\r\n    ModelId INTEGER NOT NULL,\r\n    TestId INTEGER NOT NULL,\r\n    MaxReliableTokens INTEGER,\r\n    CheckpointAccuracy REAL,\r\n    DegradationPattern TEXT,\r\n    AutopsyText TEXT,\r\n    TotalMs REAL,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ContextWindowProbes (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    ResultId INTEGER NOT NULL,\r\n    CheckpointId INTEGER,\r\n    TokenPosition INTEGER NOT NULL,\r\n    Found INTEGER NOT NULL,\r\n    Hallucinated INTEGER DEFAULT 0,\r\n    Response TEXT,\r\n    TotalMs REAL\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS GenerationResults (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL,\r\n    ModelId INTEGER NOT NULL,\r\n    SeedId INTEGER NOT NULL,\r\n    Category TEXT NOT NULL,\r\n    Response TEXT NOT NULL,\r\n    Temperature REAL,\r\n    TopP REAL,\r\n    MaxTokens INTEGER,\r\n    FirstTokenMs REAL,\r\n    TotalMs REAL NOT NULL,\r\n    TokensPerSec REAL,\r\n    PromptTokens INTEGER,\r\n    CompletionTokens INTEGER,\r\n    AvgScore REAL,\r\n    IsHighQuality INTEGER DEFAULT 0,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS GenerationRatings (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    ResultId INTEGER NOT NULL,\r\n    JudgeModelId INTEGER NOT NULL,\r\n    Score REAL NOT NULL,\r\n    Reasoning TEXT,\r\n    IsBaseJudge INTEGER DEFAULT 0,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS McpToolTestResults (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL,\r\n    ModelId INTEGER NOT NULL,\r\n    TestId INTEGER NOT NULL,\r\n    JsonValid INTEGER NOT NULL,\r\n    CorrectTool INTEGER NOT NULL,\r\n    CorrectCommand INTEGER NOT NULL,\r\n    ParamsValid INTEGER NOT NULL,\r\n    ModelResponse TEXT,\r\n    ParsedToolCall TEXT,\r\n    JsonParseError TEXT,\r\n    ToolExecuted INTEGER,\r\n    ExecutionSuccess INTEGER,\r\n    ToolResponse TEXT,\r\n    ExecutionError TEXT,\r\n    ResponseValidated INTEGER,\r\n    ValidationReason TEXT,\r\n    Passed INTEGER NOT NULL,\r\n    TotalMs REAL NOT NULL,\r\n    ExecutionMs REAL,\r\n    TokensPerSec REAL,\r\n    PromptTokens INTEGER,\r\n    CompletionTokens INTEGER,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);";

	private const string CreateModelGroupTables = "CREATE TABLE IF NOT EXISTS ModelGroups (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Name TEXT NOT NULL,\r\n    Description TEXT,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,\r\n    UpdatedAt TEXT\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ModelGroupMembers (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    GroupId INTEGER NOT NULL REFERENCES ModelGroups(Id),\r\n    ModelId INTEGER NOT NULL REFERENCES Models(Id),\r\n    AddedAt TEXT DEFAULT CURRENT_TIMESTAMP,\r\n    UNIQUE(GroupId, ModelId)\r\n);\r\n\r\nCREATE INDEX IF NOT EXISTS idx_model_group_members_group ON ModelGroupMembers(GroupId);\r\nCREATE INDEX IF NOT EXISTS idx_model_group_members_model ON ModelGroupMembers(ModelId);";

	private const string CreateIndexes = "CREATE INDEX IF NOT EXISTS idx_models_provider ON Models(ProviderId);\r\nCREATE INDEX IF NOT EXISTS idx_category_settings_config ON CategorySettings(ConfigId);\r\nCREATE INDEX IF NOT EXISTS idx_test_type_limits_config ON TestTypeLimits(ConfigId);\r\nCREATE INDEX IF NOT EXISTS idx_conversation_turns_test ON ConversationTurns(TestId);\r\nCREATE INDEX IF NOT EXISTS idx_conversation_criteria_test ON ConversationJudgingCriteria(TestId);\r\nCREATE INDEX IF NOT EXISTS idx_context_checkpoints_test ON ContextWindowCheckpoints(TestId);\r\nCREATE INDEX IF NOT EXISTS idx_seed_tags_seed ON SeedTags(SeedId);\r\nCREATE INDEX IF NOT EXISTS idx_runs_status ON BenchmarkRuns(Status);\r\nCREATE INDEX IF NOT EXISTS idx_runs_config ON BenchmarkRuns(ConfigId);\r\nCREATE INDEX IF NOT EXISTS idx_run_logs_run ON RunLogs(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_run_models_run ON BenchmarkRunModels(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_instruction_results_run ON InstructionTestResults(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_instruction_results_model ON InstructionTestResults(ModelId);\r\nCREATE INDEX IF NOT EXISTS idx_reasoning_results_run ON ReasoningTestResults(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_reasoning_results_model ON ReasoningTestResults(ModelId);\r\nCREATE INDEX IF NOT EXISTS idx_conversation_results_run ON ConversationTestResults(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_conversation_exchanges_result ON ConversationExchanges(ResultId);\r\nCREATE INDEX IF NOT EXISTS idx_context_results_run ON ContextWindowTestResults(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_context_probes_result ON ContextWindowProbes(ResultId);\r\nCREATE INDEX IF NOT EXISTS idx_generation_results_run ON GenerationResults(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_generation_results_model ON GenerationResults(ModelId);\r\nCREATE INDEX IF NOT EXISTS idx_generation_results_seed ON GenerationResults(SeedId);\r\nCREATE INDEX IF NOT EXISTS idx_generation_ratings_result ON GenerationRatings(ResultId);\r\nCREATE INDEX IF NOT EXISTS idx_mcp_tool_results_run ON McpToolTestResults(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_mcp_tool_results_model ON McpToolTestResults(ModelId);\r\nCREATE UNIQUE INDEX IF NOT EXISTS idx_mcp_tool_results_unique ON McpToolTestResults(RunId, ModelId, TestId);";

	public DatabaseInitializer(string connectionString, string? seedsJsonPath = null)
	{
		_connectionString = connectionString;
		_seedsJsonPath = seedsJsonPath;
	}

	public async Task InitializeAsync()
	{
		await using SqliteConnection connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync();
		await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS Providers (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Name TEXT NOT NULL,\r\n    BaseUrl TEXT NOT NULL,\r\n    AuthToken TEXT,\r\n    UseAuth INTEGER DEFAULT 0,\r\n    TimeoutMinutes INTEGER DEFAULT 10,\r\n    IsActive INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,\r\n    UpdatedAt TEXT\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS Models (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    ProviderId INTEGER NOT NULL REFERENCES Providers(Id),\r\n    Identifier TEXT NOT NULL,\r\n    DisplayName TEXT,\r\n    IsDisabled INTEGER DEFAULT 0,\r\n    IsAvailable INTEGER DEFAULT 1,\r\n    IsDeleted INTEGER DEFAULT 0,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,\r\n    UNIQUE(ProviderId, Identifier)\r\n);");
		await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS TestSuiteConfigs (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Name TEXT NOT NULL,\r\n    Description TEXT,\r\n    RunPromptTests INTEGER DEFAULT 1,\r\n    RunContextWindowTests INTEGER DEFAULT 0,\r\n    RunConversationTests INTEGER DEFAULT 1,\r\n    RunQualificationTests INTEGER DEFAULT 1,\r\n    MaxInstructionTests INTEGER DEFAULT 10,\r\n    MaxReasoningTests INTEGER DEFAULT 10,\r\n    MaxConversationTests INTEGER DEFAULT 10,\r\n    RunMcpToolTests INTEGER DEFAULT 0,\r\n    MaxMcpToolTests INTEGER DEFAULT 10,\r\n    EchoMcpBaseUrl TEXT,\r\n    EchoMcpToken TEXT,\r\n    FetchSchemasFromEchoMcp INTEGER DEFAULT 1,\r\n    McpTransportType TEXT DEFAULT 'sse',\r\n    McpServerUrl TEXT,\r\n    McpServerCommand TEXT,\r\n    McpServerArgs TEXT,\r\n    HighQualityThreshold REAL DEFAULT 7.5,\r\n    InstructionPassThreshold REAL DEFAULT 0.8,\r\n    TopJudgeCount INTEGER DEFAULT 2,\r\n    ContextWindowLevel TEXT DEFAULT 'shallow',\r\n    ContextWindowTestType TEXT DEFAULT 'Multi-Needle Recall',\r\n    ContextWindowTargetTokens INTEGER DEFAULT 32000,\r\n    ContextWindowProbeCount INTEGER DEFAULT 10,\r\n    ContextWindowCheckpoints INTEGER DEFAULT 4,\r\n    ContextWindowMaxTests INTEGER DEFAULT 5,\r\n    ContextWindowTestIds TEXT,\r\n    DegradationGraceful INTEGER DEFAULT 100000,\r\n    DegradationModerate INTEGER DEFAULT 60000,\r\n    DegradationSudden INTEGER DEFAULT 30000,\r\n    TargetSeedCount INTEGER DEFAULT 50,\r\n    OverwriteSeeds INTEGER DEFAULT 1,\r\n    GlobalTemperature REAL DEFAULT 0.5,\r\n    GlobalTopP REAL DEFAULT 0.9,\r\n    GlobalMaxTokens INTEGER DEFAULT 512,\r\n    MaxParallelRequests INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,\r\n    UpdatedAt TEXT\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS CategorySettings (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    ConfigId INTEGER NOT NULL REFERENCES TestSuiteConfigs(Id) ON DELETE CASCADE,\r\n    Category TEXT NOT NULL,\r\n    Temperature REAL,\r\n    TopP REAL,\r\n    MaxTokens INTEGER,\r\n    SystemPrompt TEXT,\r\n    Weight REAL DEFAULT 0.25,\r\n    UNIQUE(ConfigId, Category)\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS TestTypeLimits (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    ConfigId INTEGER NOT NULL REFERENCES TestSuiteConfigs(Id) ON DELETE CASCADE,\r\n    TestType TEXT NOT NULL,\r\n    Category TEXT NOT NULL,\r\n    MaxTests INTEGER NOT NULL,\r\n    Temperature REAL,\r\n    TopP REAL,\r\n    MaxTokens INTEGER,\r\n    PassThreshold REAL,\r\n    MinScore REAL,\r\n    UNIQUE(ConfigId, TestType, Category)\r\n);");
		await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS InstructionTests (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Category TEXT NOT NULL,\r\n    Prompt TEXT NOT NULL,\r\n    ExpectedResult TEXT NOT NULL,\r\n    ValidationType TEXT NOT NULL DEFAULT 'exact',\r\n    StrictOrder INTEGER DEFAULT 0,\r\n    IsActive INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ReasoningTests (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Category TEXT NOT NULL,\r\n    Description TEXT,\r\n    Prompt TEXT NOT NULL,\r\n    CorrectAnswer TEXT NOT NULL,\r\n    IsActive INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ConversationTests (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Category TEXT NOT NULL,\r\n    Description TEXT,\r\n    SystemPrompt TEXT,\r\n    IsActive INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ConversationTurns (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    TestId INTEGER NOT NULL REFERENCES ConversationTests(Id) ON DELETE CASCADE,\r\n    TurnNumber INTEGER NOT NULL,\r\n    UserMessage TEXT NOT NULL,\r\n    ExpectedTheme TEXT,\r\n    UNIQUE(TestId, TurnNumber)\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ConversationJudgingCriteria (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    TestId INTEGER NOT NULL REFERENCES ConversationTests(Id) ON DELETE CASCADE,\r\n    Criterion TEXT NOT NULL,\r\n    SortOrder INTEGER DEFAULT 0\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ContextWindowTests (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Name TEXT NOT NULL,\r\n    Description TEXT,\r\n    FillerType TEXT DEFAULT 'mixed',\r\n    BaseTargetTokens INTEGER NOT NULL,\r\n    BaseCheckpointCount INTEGER NOT NULL,\r\n    BuriedInstruction TEXT,\r\n    IsActive INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ContextWindowCheckpoints (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    TestId INTEGER NOT NULL REFERENCES ContextWindowTests(Id) ON DELETE CASCADE,\r\n    TargetTokenPosition INTEGER NOT NULL,\r\n    SecretWord TEXT NOT NULL,\r\n    CarrierSentence TEXT,\r\n    SortOrder INTEGER DEFAULT 0\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS McpToolTests (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Category TEXT NOT NULL,\r\n    Description TEXT,\r\n    ToolName TEXT NOT NULL,\r\n    Command TEXT NOT NULL,\r\n    ToolSchema TEXT,\r\n    ScenarioPrompt TEXT NOT NULL,\r\n    ExpectedParams TEXT,\r\n    ResponseValidationType TEXT DEFAULT 'success',\r\n    ExpectedResponsePatterns TEXT,\r\n    ExecuteTool INTEGER DEFAULT 1,\r\n    IsActive INTEGER DEFAULT 1,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);");
		await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS Seeds (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Category TEXT NOT NULL,\r\n    Instruction TEXT NOT NULL,\r\n    Temperature REAL,\r\n    TopP REAL,\r\n    MaxTokens INTEGER,\r\n    IsAugmented INTEGER DEFAULT 0,\r\n    SourceSeedId INTEGER REFERENCES Seeds(Id),\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS SeedTags (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    SeedId INTEGER NOT NULL REFERENCES Seeds(Id) ON DELETE CASCADE,\r\n    Tag TEXT NOT NULL,\r\n    UNIQUE(SeedId, Tag)\r\n);");
		await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS BenchmarkRuns (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Name TEXT,\r\n    ConfigId INTEGER NOT NULL REFERENCES TestSuiteConfigs(Id),\r\n    ProviderId INTEGER NOT NULL REFERENCES Providers(Id),\r\n    Status TEXT DEFAULT 'pending',\r\n    StartedAt TEXT,\r\n    CompletedAt TEXT,\r\n    TotalModels INTEGER DEFAULT 0,\r\n    TotalTests INTEGER DEFAULT 0,\r\n    CompletedTests INTEGER DEFAULT 0,\r\n    ErrorCount INTEGER DEFAULT 0,\r\n    BaseJudgeModelId INTEGER REFERENCES Models(Id),\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS RunLogs (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL REFERENCES BenchmarkRuns(Id) ON DELETE CASCADE,\r\n    Level TEXT NOT NULL,\r\n    Message TEXT NOT NULL,\r\n    ModelName TEXT,\r\n    Timestamp TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS BenchmarkRunModels (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL REFERENCES BenchmarkRuns(Id) ON DELETE CASCADE,\r\n    ModelId INTEGER NOT NULL REFERENCES Models(Id),\r\n    Status TEXT DEFAULT 'pending',\r\n    QualificationPassed INTEGER,\r\n    InstructionPassRate REAL,\r\n    InstructionStrictPassRate REAL,\r\n    ReasoningAvgScore REAL,\r\n    IsBaseJudge INTEGER DEFAULT 0,\r\n    IsAutoJudge INTEGER DEFAULT 0,\r\n    UNIQUE(RunId, ModelId)\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS BenchmarkAutoJudges (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL REFERENCES BenchmarkRuns(Id) ON DELETE CASCADE,\r\n    ModelId INTEGER NOT NULL REFERENCES Models(Id),\r\n    SelectionReason TEXT,\r\n    UNIQUE(RunId, ModelId)\r\n);");
		await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS InstructionTestResults (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL,\r\n    ModelId INTEGER NOT NULL,\r\n    TestId INTEGER NOT NULL,\r\n    Passed INTEGER NOT NULL,\r\n    StrictPass INTEGER NOT NULL,\r\n    LenientPass INTEGER DEFAULT 0,\r\n    Response TEXT,\r\n    FailureReason TEXT,\r\n    FirstTokenMs REAL,\r\n    TotalMs REAL NOT NULL,\r\n    TokensPerSec REAL,\r\n    PromptTokens INTEGER,\r\n    CompletionTokens INTEGER,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ReasoningTestResults (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL,\r\n    ModelId INTEGER NOT NULL,\r\n    TestId INTEGER NOT NULL,\r\n    Response TEXT NOT NULL,\r\n    OverallScore REAL,\r\n    CorrectAnswerScore REAL,\r\n    LogicalStepsScore REAL,\r\n    ClarityScore REAL,\r\n    JudgeReasoning TEXT,\r\n    JudgeModelId INTEGER,\r\n    FirstTokenMs REAL,\r\n    TotalMs REAL NOT NULL,\r\n    TokensPerSec REAL,\r\n    PromptTokens INTEGER,\r\n    CompletionTokens INTEGER,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ConversationTestResults (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL,\r\n    ModelId INTEGER NOT NULL,\r\n    TestId INTEGER NOT NULL,\r\n    OverallScore REAL,\r\n    TopicCoherence REAL,\r\n    ConversationalTone REAL,\r\n    ContextRetention REAL,\r\n    Helpfulness REAL,\r\n    JudgeReasoning TEXT,\r\n    JudgeModelId INTEGER,\r\n    TotalMs REAL,\r\n    TokensPerSec REAL,\r\n    PromptTokens INTEGER,\r\n    CompletionTokens INTEGER,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ConversationExchanges (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    ResultId INTEGER NOT NULL,\r\n    TurnNumber INTEGER NOT NULL,\r\n    UserMessage TEXT NOT NULL,\r\n    ModelResponse TEXT NOT NULL,\r\n    FirstTokenMs REAL,\r\n    TotalMs REAL,\r\n    TokensPerSec REAL,\r\n    PromptTokens INTEGER,\r\n    CompletionTokens INTEGER,\r\n    UNIQUE(ResultId, TurnNumber)\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ContextWindowTestResults (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL,\r\n    ModelId INTEGER NOT NULL,\r\n    TestId INTEGER NOT NULL,\r\n    MaxReliableTokens INTEGER,\r\n    CheckpointAccuracy REAL,\r\n    DegradationPattern TEXT,\r\n    AutopsyText TEXT,\r\n    TotalMs REAL,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ContextWindowProbes (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    ResultId INTEGER NOT NULL,\r\n    CheckpointId INTEGER,\r\n    TokenPosition INTEGER NOT NULL,\r\n    Found INTEGER NOT NULL,\r\n    Hallucinated INTEGER DEFAULT 0,\r\n    Response TEXT,\r\n    TotalMs REAL\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS GenerationResults (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL,\r\n    ModelId INTEGER NOT NULL,\r\n    SeedId INTEGER NOT NULL,\r\n    Category TEXT NOT NULL,\r\n    Response TEXT NOT NULL,\r\n    Temperature REAL,\r\n    TopP REAL,\r\n    MaxTokens INTEGER,\r\n    FirstTokenMs REAL,\r\n    TotalMs REAL NOT NULL,\r\n    TokensPerSec REAL,\r\n    PromptTokens INTEGER,\r\n    CompletionTokens INTEGER,\r\n    AvgScore REAL,\r\n    IsHighQuality INTEGER DEFAULT 0,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS GenerationRatings (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    ResultId INTEGER NOT NULL,\r\n    JudgeModelId INTEGER NOT NULL,\r\n    Score REAL NOT NULL,\r\n    Reasoning TEXT,\r\n    IsBaseJudge INTEGER DEFAULT 0,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS McpToolTestResults (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    RunId INTEGER NOT NULL,\r\n    ModelId INTEGER NOT NULL,\r\n    TestId INTEGER NOT NULL,\r\n    JsonValid INTEGER NOT NULL,\r\n    CorrectTool INTEGER NOT NULL,\r\n    CorrectCommand INTEGER NOT NULL,\r\n    ParamsValid INTEGER NOT NULL,\r\n    ModelResponse TEXT,\r\n    ParsedToolCall TEXT,\r\n    JsonParseError TEXT,\r\n    ToolExecuted INTEGER,\r\n    ExecutionSuccess INTEGER,\r\n    ToolResponse TEXT,\r\n    ExecutionError TEXT,\r\n    ResponseValidated INTEGER,\r\n    ValidationReason TEXT,\r\n    Passed INTEGER NOT NULL,\r\n    TotalMs REAL NOT NULL,\r\n    ExecutionMs REAL,\r\n    TokensPerSec REAL,\r\n    PromptTokens INTEGER,\r\n    CompletionTokens INTEGER,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n);");
		await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS ModelGroups (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    Name TEXT NOT NULL,\r\n    Description TEXT,\r\n    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,\r\n    UpdatedAt TEXT\r\n);\r\n\r\nCREATE TABLE IF NOT EXISTS ModelGroupMembers (\r\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n    GroupId INTEGER NOT NULL REFERENCES ModelGroups(Id),\r\n    ModelId INTEGER NOT NULL REFERENCES Models(Id),\r\n    AddedAt TEXT DEFAULT CURRENT_TIMESTAMP,\r\n    UNIQUE(GroupId, ModelId)\r\n);\r\n\r\nCREATE INDEX IF NOT EXISTS idx_model_group_members_group ON ModelGroupMembers(GroupId);\r\nCREATE INDEX IF NOT EXISTS idx_model_group_members_model ON ModelGroupMembers(ModelId);");
		await connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_models_provider ON Models(ProviderId);\r\nCREATE INDEX IF NOT EXISTS idx_category_settings_config ON CategorySettings(ConfigId);\r\nCREATE INDEX IF NOT EXISTS idx_test_type_limits_config ON TestTypeLimits(ConfigId);\r\nCREATE INDEX IF NOT EXISTS idx_conversation_turns_test ON ConversationTurns(TestId);\r\nCREATE INDEX IF NOT EXISTS idx_conversation_criteria_test ON ConversationJudgingCriteria(TestId);\r\nCREATE INDEX IF NOT EXISTS idx_context_checkpoints_test ON ContextWindowCheckpoints(TestId);\r\nCREATE INDEX IF NOT EXISTS idx_seed_tags_seed ON SeedTags(SeedId);\r\nCREATE INDEX IF NOT EXISTS idx_runs_status ON BenchmarkRuns(Status);\r\nCREATE INDEX IF NOT EXISTS idx_runs_config ON BenchmarkRuns(ConfigId);\r\nCREATE INDEX IF NOT EXISTS idx_run_logs_run ON RunLogs(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_run_models_run ON BenchmarkRunModels(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_instruction_results_run ON InstructionTestResults(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_instruction_results_model ON InstructionTestResults(ModelId);\r\nCREATE INDEX IF NOT EXISTS idx_reasoning_results_run ON ReasoningTestResults(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_reasoning_results_model ON ReasoningTestResults(ModelId);\r\nCREATE INDEX IF NOT EXISTS idx_conversation_results_run ON ConversationTestResults(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_conversation_exchanges_result ON ConversationExchanges(ResultId);\r\nCREATE INDEX IF NOT EXISTS idx_context_results_run ON ContextWindowTestResults(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_context_probes_result ON ContextWindowProbes(ResultId);\r\nCREATE INDEX IF NOT EXISTS idx_generation_results_run ON GenerationResults(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_generation_results_model ON GenerationResults(ModelId);\r\nCREATE INDEX IF NOT EXISTS idx_generation_results_seed ON GenerationResults(SeedId);\r\nCREATE INDEX IF NOT EXISTS idx_generation_ratings_result ON GenerationRatings(ResultId);\r\nCREATE INDEX IF NOT EXISTS idx_mcp_tool_results_run ON McpToolTestResults(RunId);\r\nCREATE INDEX IF NOT EXISTS idx_mcp_tool_results_model ON McpToolTestResults(ModelId);\r\nCREATE UNIQUE INDEX IF NOT EXISTS idx_mcp_tool_results_unique ON McpToolTestResults(RunId, ModelId, TestId);");
		await RunMigrationsAsync(connection);
	}

	private async Task RunMigrationsAsync(SqliteConnection connection)
	{
		List<string> columnList = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('BenchmarkRuns')")).ToList();
		if (!columnList.Contains("ProviderId"))
		{
			await connection.ExecuteAsync("ALTER TABLE BenchmarkRuns ADD COLUMN ProviderId INTEGER NOT NULL DEFAULT 0");
		}
		if (!columnList.Contains("ModelGroupId"))
		{
			await connection.ExecuteAsync("ALTER TABLE BenchmarkRuns ADD COLUMN ModelGroupId INTEGER REFERENCES ModelGroups(Id)");
		}
		IEnumerable<string> configColumns = await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('TestSuiteConfigs')");
		if (!configColumns.Contains("InstructionPassThreshold"))
		{
			await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN InstructionPassThreshold REAL DEFAULT 0.8");
		}
		if (!configColumns.Contains("MaxInstructionTests"))
		{
			await connection.ExecuteAsync("\r\n                ALTER TABLE TestSuiteConfigs ADD COLUMN MaxInstructionTests INTEGER DEFAULT 10;\r\n                ALTER TABLE TestSuiteConfigs ADD COLUMN MaxReasoningTests INTEGER DEFAULT 10;\r\n                ALTER TABLE TestSuiteConfigs ADD COLUMN MaxConversationTests INTEGER DEFAULT 10;\r\n            ");
		}
		if (!configColumns.Contains("ContextWindowTargetTokens"))
		{
			await connection.ExecuteAsync("\r\n                ALTER TABLE TestSuiteConfigs ADD COLUMN ContextWindowTargetTokens INTEGER DEFAULT 32000;\r\n                ALTER TABLE TestSuiteConfigs ADD COLUMN ContextWindowProbeCount INTEGER DEFAULT 10;\r\n                ALTER TABLE TestSuiteConfigs ADD COLUMN ContextWindowCheckpoints INTEGER DEFAULT 4;\r\n            ");
		}
		await MigrateScoreColumnsToRealAsync(connection);
		await AddUniqueResultIndexesAsync(connection);
		if (!(await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('ContextWindowCheckpoints')")).Contains("RelativePosition"))
		{
			await connection.ExecuteAsync("ALTER TABLE ContextWindowCheckpoints ADD COLUMN RelativePosition REAL");
		}
		if (!(await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('BenchmarkRunModels')")).Contains("ContextWindowAvgReliability"))
		{
			await connection.ExecuteAsync("\r\n                ALTER TABLE BenchmarkRunModels ADD COLUMN ContextWindowAvgReliability REAL;\r\n                ALTER TABLE BenchmarkRunModels ADD COLUMN ContextWindowAvgAccuracy REAL;\r\n                ALTER TABLE BenchmarkRunModels ADD COLUMN ContextWindowTestCount INTEGER DEFAULT 0;\r\n            ");
		}
		if (!(await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('ContextWindowTests')")).Contains("NeedleComplexity"))
		{
			await connection.ExecuteAsync("ALTER TABLE ContextWindowTests ADD COLUMN NeedleComplexity TEXT DEFAULT 'single'");
		}
		if (!configColumns.Contains("ContextWindowTestType"))
		{
			await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN ContextWindowTestType TEXT DEFAULT 'Multi-Needle Recall'");
		}
		List<string> modelColumns = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('Models')")).ToList();
		if (modelColumns.Contains("IsActive") && !modelColumns.Contains("IsDisabled"))
		{
			await connection.ExecuteAsync("ALTER TABLE Models ADD COLUMN IsDisabled INTEGER DEFAULT 0");
			await connection.ExecuteAsync("ALTER TABLE Models ADD COLUMN IsAvailable INTEGER DEFAULT 1");
			await connection.ExecuteAsync("ALTER TABLE Models ADD COLUMN IsDeleted INTEGER DEFAULT 0");
			await connection.ExecuteAsync("UPDATE Models SET IsAvailable = IsActive");
		}
		else
		{
			if (!modelColumns.Contains("IsDisabled"))
			{
				await connection.ExecuteAsync("ALTER TABLE Models ADD COLUMN IsDisabled INTEGER DEFAULT 0");
			}
			if (!modelColumns.Contains("IsAvailable"))
			{
				await connection.ExecuteAsync("ALTER TABLE Models ADD COLUMN IsAvailable INTEGER DEFAULT 1");
			}
			if (!modelColumns.Contains("IsDeleted"))
			{
				await connection.ExecuteAsync("ALTER TABLE Models ADD COLUMN IsDeleted INTEGER DEFAULT 0");
			}
		}
		List<string> instrTestColumns = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('InstructionTests')")).ToList();
		if (!instrTestColumns.Contains("ExcludePatterns"))
		{
			await connection.ExecuteAsync("ALTER TABLE InstructionTests ADD COLUMN ExcludePatterns TEXT");
		}
		if (!instrTestColumns.Contains("AllowedValues"))
		{
			await connection.ExecuteAsync("ALTER TABLE InstructionTests ADD COLUMN AllowedValues TEXT");
		}
		if (!instrTestColumns.Contains("ExpectedCount"))
		{
			await connection.ExecuteAsync("ALTER TABLE InstructionTests ADD COLUMN ExpectedCount INTEGER");
		}
		List<string> configCols = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('TestSuiteConfigs')")).ToList();
		if (!configCols.Contains("RunMcpToolTests"))
		{
			await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN RunMcpToolTests INTEGER DEFAULT 0");
		}
		if (!configCols.Contains("MaxMcpToolTests"))
		{
			await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN MaxMcpToolTests INTEGER DEFAULT 10");
		}
		if (!configCols.Contains("EchoMcpBaseUrl"))
		{
			await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN EchoMcpBaseUrl TEXT");
		}
		if (!configCols.Contains("EchoMcpToken"))
		{
			await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN EchoMcpToken TEXT");
		}
		if (!configCols.Contains("FetchSchemasFromEchoMcp"))
		{
			await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN FetchSchemasFromEchoMcp INTEGER DEFAULT 1");
		}
		if (!configCols.Contains("McpTransportType"))
		{
			await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN McpTransportType TEXT DEFAULT 'sse'");
		}
		if (!configCols.Contains("McpServerUrl"))
		{
			await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN McpServerUrl TEXT");
		}
		if (!configCols.Contains("McpServerCommand"))
		{
			await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN McpServerCommand TEXT");
		}
		if (!configCols.Contains("McpServerArgs"))
		{
			await connection.ExecuteAsync("ALTER TABLE TestSuiteConfigs ADD COLUMN McpServerArgs TEXT");
		}
		List<string> convResultCols = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('ConversationTestResults')")).ToList();
		if (!convResultCols.Contains("TokensPerSec"))
		{
			await connection.ExecuteAsync("ALTER TABLE ConversationTestResults ADD COLUMN TokensPerSec REAL");
		}
		if (!convResultCols.Contains("PromptTokens"))
		{
			await connection.ExecuteAsync("ALTER TABLE ConversationTestResults ADD COLUMN PromptTokens INTEGER");
		}
		if (!convResultCols.Contains("CompletionTokens"))
		{
			await connection.ExecuteAsync("ALTER TABLE ConversationTestResults ADD COLUMN CompletionTokens INTEGER");
		}
		await ImportSeedsIfEmptyAsync(connection);
		await ImportContextWindowTestsIfEmptyAsync(connection);
		await ImportInstructionTestsIfEmptyAsync(connection);
		await ImportReasoningTestsIfEmptyAsync(connection);
		await ImportConversationTestsIfEmptyAsync(connection);
		await ImportMcpToolTestsIfEmptyAsync(connection);
	}

	private async Task ImportSeedsIfEmptyAsync(SqliteConnection connection)
	{
		if (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Seeds") > 0)
		{
			return;
		}
		string baseSeedsPath = _seedsJsonPath;
		if (string.IsNullOrEmpty(baseSeedsPath))
		{
			string[] obj = new string[4]
			{
				Path.Combine(AppContext.BaseDirectory, "base_seeds.jsonl"),
				null,
				null,
				null
			};
			_003C_003Ey__InlineArray9<string> buffer = default(_003C_003Ey__InlineArray9<string>);
			buffer[0] = AppContext.BaseDirectory;
			buffer[1] = "..";
			buffer[2] = "..";
			buffer[3] = "..";
			buffer[4] = "..";
			buffer[5] = "..";
			buffer[6] = "src";
			buffer[7] = "config";
			buffer[8] = "base_seeds.jsonl";
			obj[1] = Path.Combine(buffer);
			_003C_003Ey__InlineArray6<string> buffer2 = default(_003C_003Ey__InlineArray6<string>);
			buffer2[0] = AppContext.BaseDirectory;
			buffer2[1] = "..";
			buffer2[2] = "..";
			buffer2[3] = "..";
			buffer2[4] = "config";
			buffer2[5] = "base_seeds.jsonl";
			obj[2] = Path.Combine(buffer2);
			obj[3] = "src/config/base_seeds.jsonl";
			string[] searchPaths = obj;
			baseSeedsPath = searchPaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
		}
		if (string.IsNullOrEmpty(baseSeedsPath) || !File.Exists(baseSeedsPath))
		{
			Console.WriteLine("[DatabaseInitializer] No base_seeds.jsonl found, skipping seed import");
			return;
		}
		try
		{
			string[] lines = await File.ReadAllLinesAsync(baseSeedsPath);
			int importedCount = 0;
			using SqliteTransaction transaction = connection.BeginTransaction();
			string[] array = lines;
			foreach (string line in array)
			{
				if (string.IsNullOrWhiteSpace(line))
				{
					continue;
				}
				try
				{
					BaseSeedItem seed = JsonSerializer.Deserialize<BaseSeedItem>(line, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
					if (seed == null || string.IsNullOrEmpty(seed.Instruction))
					{
						continue;
					}
					string category = seed.Tags?.FirstOrDefault() ?? "instruction";
					await connection.ExecuteAsync("INSERT INTO Seeds (Category, Instruction, IsAugmented, CreatedAt)\r\nVALUES (@Category, @Instruction, 0, datetime('now'))", new
					{
						Category = category,
						Instruction = seed.Instruction
					}, transaction);
					int seedId = await connection.ExecuteScalarAsync<int>("SELECT last_insert_rowid()", null, transaction);
					if (seed.Tags != null)
					{
						foreach (string tag in seed.Tags)
						{
							await connection.ExecuteAsync("INSERT OR IGNORE INTO SeedTags (SeedId, Tag)\r\nVALUES (@SeedId, @Tag)", new
							{
								SeedId = seedId,
								Tag = tag
							}, transaction);
						}
					}
					importedCount++;
				}
				catch
				{
				}
			}
			transaction.Commit();
			Console.WriteLine($"[DatabaseInitializer] Imported {importedCount} base seeds from base_seeds.jsonl");
		}
		catch (Exception ex)
		{
			Console.WriteLine("[DatabaseInitializer] Failed to import seeds: " + ex.Message);
		}
	}

	private async Task ImportContextWindowTestsIfEmptyAsync(SqliteConnection connection)
	{
		if (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ContextWindowTests") > 0)
		{
			return;
		}
		string[] obj = new string[4]
		{
			Path.Combine(AppContext.BaseDirectory, "tests", "context_window_tests.json"),
			null,
			null,
			null
		};
		_003C_003Ey__InlineArray10<string> buffer = default(_003C_003Ey__InlineArray10<string>);
		buffer[0] = AppContext.BaseDirectory;
		buffer[1] = "..";
		buffer[2] = "..";
		buffer[3] = "..";
		buffer[4] = "..";
		buffer[5] = "..";
		buffer[6] = "src";
		buffer[7] = "config";
		buffer[8] = "tests";
		buffer[9] = "context_window_tests.json";
		obj[1] = Path.Combine(buffer);
		global::_003C_003Ey__InlineArray7<string> buffer2 = default(global::_003C_003Ey__InlineArray7<string>);
		buffer2[0] = AppContext.BaseDirectory;
		buffer2[1] = "..";
		buffer2[2] = "..";
		buffer2[3] = "..";
		buffer2[4] = "config";
		buffer2[5] = "tests";
		buffer2[6] = "context_window_tests.json";
		obj[2] = Path.Combine(buffer2);
		obj[3] = "src/config/tests/context_window_tests.json";
		string[] searchPaths = obj;
		string seedsPath = searchPaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
		if (string.IsNullOrEmpty(seedsPath))
		{
			Console.WriteLine("[DatabaseInitializer] No context_window_tests.json found, skipping context window test import");
			return;
		}
		try
		{
			ContextWindowTestSeedFile seedData = JsonSerializer.Deserialize<ContextWindowTestSeedFile>(await File.ReadAllTextAsync(seedsPath), new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});
			if (seedData?.Tests == null || seedData.Tests.Count == 0)
			{
				Console.WriteLine("[DatabaseInitializer] context_window_tests.json is empty or invalid");
				return;
			}
			int importedCount = 0;
			using SqliteTransaction transaction = connection.BeginTransaction();
			foreach (ContextWindowTestSeedItem test in seedData.Tests)
			{
				int testId = await connection.ExecuteScalarAsync<int>("INSERT INTO ContextWindowTests (Name, Description, FillerType, BaseTargetTokens, BaseCheckpointCount, BuriedInstruction, IsActive, CreatedAt)\r\nVALUES (@Name, @Description, @FillerType, @BaseTargetTokens, @BaseCheckpointCount, @BuriedInstruction, 1, datetime('now'));\r\nSELECT last_insert_rowid();", new
				{
					Name = test.Name,
					Description = test.Description,
					FillerType = test.FillerType,
					BaseTargetTokens = test.BaseTargetTokens,
					BaseCheckpointCount = (test.BaseCheckpointCount ?? test.Checkpoints?.Count ?? 0),
					BuriedInstruction = test.BuriedInstruction
				}, transaction);
				if (test.Checkpoints != null)
				{
					int sortOrder = 0;
					foreach (ContextWindowCheckpointSeedItem cp in test.Checkpoints)
					{
						await connection.ExecuteAsync("INSERT INTO ContextWindowCheckpoints (TestId, TargetTokenPosition, RelativePosition, SecretWord, CarrierSentence, SortOrder)\r\nVALUES (@TestId, @TargetTokenPosition, @RelativePosition, @SecretWord, @CarrierSentence, @SortOrder)", new
						{
							TestId = testId,
							TargetTokenPosition = (cp.RelativePosition.HasValue ? ((int)(cp.RelativePosition.Value * (double)test.BaseTargetTokens)) : 0),
							RelativePosition = cp.RelativePosition,
							SecretWord = cp.SecretWord,
							CarrierSentence = cp.CarrierSentence,
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
			Console.WriteLine("[DatabaseInitializer] Failed to import context window tests: " + ex.Message);
		}
	}

	private async Task ImportInstructionTestsIfEmptyAsync(SqliteConnection connection)
	{
		if (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM InstructionTests") > 0)
		{
			return;
		}
		string[] obj = new string[4]
		{
			Path.Combine(AppContext.BaseDirectory, "tests", "instruction_tests.json"),
			null,
			null,
			null
		};
		_003C_003Ey__InlineArray10<string> buffer = default(_003C_003Ey__InlineArray10<string>);
		buffer[0] = AppContext.BaseDirectory;
		buffer[1] = "..";
		buffer[2] = "..";
		buffer[3] = "..";
		buffer[4] = "..";
		buffer[5] = "..";
		buffer[6] = "src";
		buffer[7] = "config";
		buffer[8] = "tests";
		buffer[9] = "instruction_tests.json";
		obj[1] = Path.Combine(buffer);
		global::_003C_003Ey__InlineArray7<string> buffer2 = default(global::_003C_003Ey__InlineArray7<string>);
		buffer2[0] = AppContext.BaseDirectory;
		buffer2[1] = "..";
		buffer2[2] = "..";
		buffer2[3] = "..";
		buffer2[4] = "config";
		buffer2[5] = "tests";
		buffer2[6] = "instruction_tests.json";
		obj[2] = Path.Combine(buffer2);
		obj[3] = "src/config/tests/instruction_tests.json";
		string[] searchPaths = obj;
		string seedsPath = searchPaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
		if (string.IsNullOrEmpty(seedsPath))
		{
			Console.WriteLine("[DatabaseInitializer] No instruction_tests.json found, skipping import");
			return;
		}
		try
		{
			InstructionTestSeedFile seedData = JsonSerializer.Deserialize<InstructionTestSeedFile>(await File.ReadAllTextAsync(seedsPath), new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});
			if (seedData?.Tests == null || seedData.Tests.Count == 0)
			{
				Console.WriteLine("[DatabaseInitializer] instruction_tests.json is empty or invalid");
				return;
			}
			int importedCount = 0;
			using SqliteTransaction transaction = connection.BeginTransaction();
			foreach (InstructionTestSeedItem test in seedData.Tests)
			{
				string excludeJson = ((test.ExcludePatterns != null) ? JsonSerializer.Serialize(test.ExcludePatterns) : null);
				string allowedJson = ((test.AllowedValues != null) ? JsonSerializer.Serialize(test.AllowedValues) : null);
				await connection.ExecuteAsync("INSERT INTO InstructionTests (Category, Prompt, ExpectedResult, ValidationType, StrictOrder, ExcludePatterns, AllowedValues, ExpectedCount, IsActive, CreatedAt)\r\nVALUES (@Category, @Prompt, @ExpectedResult, @ValidationType, @StrictOrder, @ExcludePatterns, @AllowedValues, @ExpectedCount, 1, datetime('now'))", new
				{
					Category = test.Category,
					Prompt = test.Prompt,
					ExpectedResult = (test.ExpectedResult ?? ""),
					ValidationType = test.ValidationType,
					StrictOrder = (test.StrictOrder ? 1 : 0),
					ExcludePatterns = excludeJson,
					AllowedValues = allowedJson,
					ExpectedCount = test.ExpectedCount
				}, transaction);
				importedCount++;
			}
			transaction.Commit();
			Console.WriteLine($"[DatabaseInitializer] Imported {importedCount} instruction tests from instruction_tests.json");
		}
		catch (Exception ex)
		{
			Console.WriteLine("[DatabaseInitializer] Failed to import instruction tests: " + ex.Message);
		}
	}

	private async Task ImportReasoningTestsIfEmptyAsync(SqliteConnection connection)
	{
		if (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ReasoningTests") > 0)
		{
			return;
		}
		string[] obj = new string[4]
		{
			Path.Combine(AppContext.BaseDirectory, "tests", "reasoning_tests.json"),
			null,
			null,
			null
		};
		_003C_003Ey__InlineArray10<string> buffer = default(_003C_003Ey__InlineArray10<string>);
		buffer[0] = AppContext.BaseDirectory;
		buffer[1] = "..";
		buffer[2] = "..";
		buffer[3] = "..";
		buffer[4] = "..";
		buffer[5] = "..";
		buffer[6] = "src";
		buffer[7] = "config";
		buffer[8] = "tests";
		buffer[9] = "reasoning_tests.json";
		obj[1] = Path.Combine(buffer);
		global::_003C_003Ey__InlineArray7<string> buffer2 = default(global::_003C_003Ey__InlineArray7<string>);
		buffer2[0] = AppContext.BaseDirectory;
		buffer2[1] = "..";
		buffer2[2] = "..";
		buffer2[3] = "..";
		buffer2[4] = "config";
		buffer2[5] = "tests";
		buffer2[6] = "reasoning_tests.json";
		obj[2] = Path.Combine(buffer2);
		obj[3] = "src/config/tests/reasoning_tests.json";
		string[] searchPaths = obj;
		string seedsPath = searchPaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
		if (string.IsNullOrEmpty(seedsPath))
		{
			Console.WriteLine("[DatabaseInitializer] No reasoning_tests.json found, skipping import");
			return;
		}
		try
		{
			ReasoningTestSeedFile seedData = JsonSerializer.Deserialize<ReasoningTestSeedFile>(await File.ReadAllTextAsync(seedsPath), new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});
			if (seedData?.Tests == null || seedData.Tests.Count == 0)
			{
				Console.WriteLine("[DatabaseInitializer] reasoning_tests.json is empty or invalid");
				return;
			}
			int importedCount = 0;
			using SqliteTransaction transaction = connection.BeginTransaction();
			foreach (ReasoningTestSeedItem test in seedData.Tests)
			{
				await connection.ExecuteAsync("INSERT INTO ReasoningTests (Category, Description, Prompt, CorrectAnswer, IsActive, CreatedAt)\r\nVALUES (@Category, @Description, @Prompt, @CorrectAnswer, 1, datetime('now'))", new { test.Category, test.Description, test.Prompt, test.CorrectAnswer }, transaction);
				importedCount++;
			}
			transaction.Commit();
			Console.WriteLine($"[DatabaseInitializer] Imported {importedCount} reasoning tests from reasoning_tests.json");
		}
		catch (Exception ex)
		{
			Console.WriteLine("[DatabaseInitializer] Failed to import reasoning tests: " + ex.Message);
		}
	}

	private async Task ImportConversationTestsIfEmptyAsync(SqliteConnection connection)
	{
		if (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ConversationTests") > 0)
		{
			return;
		}
		string[] obj = new string[4]
		{
			Path.Combine(AppContext.BaseDirectory, "tests", "conversation_tests.json"),
			null,
			null,
			null
		};
		_003C_003Ey__InlineArray10<string> buffer = default(_003C_003Ey__InlineArray10<string>);
		buffer[0] = AppContext.BaseDirectory;
		buffer[1] = "..";
		buffer[2] = "..";
		buffer[3] = "..";
		buffer[4] = "..";
		buffer[5] = "..";
		buffer[6] = "src";
		buffer[7] = "config";
		buffer[8] = "tests";
		buffer[9] = "conversation_tests.json";
		obj[1] = Path.Combine(buffer);
		global::_003C_003Ey__InlineArray7<string> buffer2 = default(global::_003C_003Ey__InlineArray7<string>);
		buffer2[0] = AppContext.BaseDirectory;
		buffer2[1] = "..";
		buffer2[2] = "..";
		buffer2[3] = "..";
		buffer2[4] = "config";
		buffer2[5] = "tests";
		buffer2[6] = "conversation_tests.json";
		obj[2] = Path.Combine(buffer2);
		obj[3] = "src/config/tests/conversation_tests.json";
		string[] searchPaths = obj;
		string seedsPath = searchPaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
		if (string.IsNullOrEmpty(seedsPath))
		{
			Console.WriteLine("[DatabaseInitializer] No conversation_tests.json found, skipping import");
			return;
		}
		try
		{
			ConversationTestSeedFile seedData = JsonSerializer.Deserialize<ConversationTestSeedFile>(await File.ReadAllTextAsync(seedsPath), new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});
			if (seedData?.Tests == null || seedData.Tests.Count == 0)
			{
				Console.WriteLine("[DatabaseInitializer] conversation_tests.json is empty or invalid");
				return;
			}
			int importedCount = 0;
			using SqliteTransaction transaction = connection.BeginTransaction();
			foreach (ConversationTestSeedItem test in seedData.Tests)
			{
				int testId = await connection.ExecuteScalarAsync<int>("INSERT INTO ConversationTests (Category, Description, SystemPrompt, IsActive, CreatedAt)\r\nVALUES (@Category, @Description, @SystemPrompt, 1, datetime('now'));\r\nSELECT last_insert_rowid();", new { test.Category, test.Description, test.SystemPrompt }, transaction);
				int turnNumber = 1;
				foreach (ConversationTurnSeedItem turn in test.Turns)
				{
					await connection.ExecuteAsync("INSERT INTO ConversationTurns (TestId, TurnNumber, UserMessage, ExpectedTheme)\r\nVALUES (@TestId, @TurnNumber, @UserMessage, @ExpectedTheme)", new
					{
						TestId = testId,
						TurnNumber = turnNumber++,
						UserMessage = turn.UserMessage,
						ExpectedTheme = turn.ExpectedTheme
					}, transaction);
				}
				int sortOrder = 0;
				foreach (string criterion in test.JudgingCriteria)
				{
					await connection.ExecuteAsync("INSERT INTO ConversationJudgingCriteria (TestId, Criterion, SortOrder)\r\nVALUES (@TestId, @Criterion, @SortOrder)", new
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
			Console.WriteLine("[DatabaseInitializer] Failed to import conversation tests: " + ex.Message);
		}
	}

	private async Task ImportMcpToolTestsIfEmptyAsync(SqliteConnection connection)
	{
		if (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM McpToolTests") > 0)
		{
			return;
		}
		string[] obj = new string[4]
		{
			Path.Combine(AppContext.BaseDirectory, "tests", "mcp_tool_tests.json"),
			null,
			null,
			null
		};
		_003C_003Ey__InlineArray10<string> buffer = default(_003C_003Ey__InlineArray10<string>);
		buffer[0] = AppContext.BaseDirectory;
		buffer[1] = "..";
		buffer[2] = "..";
		buffer[3] = "..";
		buffer[4] = "..";
		buffer[5] = "..";
		buffer[6] = "src";
		buffer[7] = "config";
		buffer[8] = "tests";
		buffer[9] = "mcp_tool_tests.json";
		obj[1] = Path.Combine(buffer);
		global::_003C_003Ey__InlineArray7<string> buffer2 = default(global::_003C_003Ey__InlineArray7<string>);
		buffer2[0] = AppContext.BaseDirectory;
		buffer2[1] = "..";
		buffer2[2] = "..";
		buffer2[3] = "..";
		buffer2[4] = "config";
		buffer2[5] = "tests";
		buffer2[6] = "mcp_tool_tests.json";
		obj[2] = Path.Combine(buffer2);
		obj[3] = "src/config/tests/mcp_tool_tests.json";
		string[] searchPaths = obj;
		string seedsPath = searchPaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
		if (string.IsNullOrEmpty(seedsPath))
		{
			Console.WriteLine("[DatabaseInitializer] No mcp_tool_tests.json found, skipping import");
			return;
		}
		try
		{
			McpToolTestSeedFile seedData = JsonSerializer.Deserialize<McpToolTestSeedFile>(await File.ReadAllTextAsync(seedsPath), new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});
			if (seedData?.Tests == null || seedData.Tests.Count == 0)
			{
				Console.WriteLine("[DatabaseInitializer] mcp_tool_tests.json is empty or invalid");
				return;
			}
			int importedCount = 0;
			using SqliteTransaction transaction = connection.BeginTransaction();
			foreach (McpToolTestSeedItem test in seedData.Tests)
			{
				string expectedParamsJson = ((test.ExpectedParams != null) ? JsonSerializer.Serialize(test.ExpectedParams) : null);
				string expectedPatternsJson = ((test.ExpectedResponsePatterns != null) ? JsonSerializer.Serialize(test.ExpectedResponsePatterns) : null);
				await connection.ExecuteAsync("INSERT INTO McpToolTests (Category, Description, ToolName, Command, ToolSchema, ScenarioPrompt,\r\n    ExpectedParams, ResponseValidationType, ExpectedResponsePatterns, ExecuteTool, IsActive, CreatedAt)\r\nVALUES (@Category, @Description, @ToolName, @Command, @ToolSchema, @ScenarioPrompt,\r\n    @ExpectedParams, @ResponseValidationType, @ExpectedResponsePatterns, @ExecuteTool, 1, datetime('now'))", new
				{
					Category = test.Category,
					Description = test.Description,
					ToolName = test.ToolName,
					Command = test.Command,
					ToolSchema = test.ToolSchema,
					ScenarioPrompt = test.ScenarioPrompt,
					ExpectedParams = expectedParamsJson,
					ResponseValidationType = test.ResponseValidationType,
					ExpectedResponsePatterns = expectedPatternsJson,
					ExecuteTool = (test.ExecuteTool ? 1 : 0)
				}, transaction);
				importedCount++;
			}
			transaction.Commit();
			Console.WriteLine($"[DatabaseInitializer] Imported {importedCount} MCP tool tests from mcp_tool_tests.json");
		}
		catch (Exception ex)
		{
			Console.WriteLine("[DatabaseInitializer] Failed to import MCP tool tests: " + ex.Message);
		}
	}

	private async Task MigrateScoreColumnsToRealAsync(SqliteConnection connection)
	{
		dynamic overallScoreCol = (await connection.QueryAsync<object>("SELECT name, type FROM pragma_table_info('ReasoningTestResults')")).FirstOrDefault((dynamic c) => c.name == "OverallScore");
		if (overallScoreCol != null && overallScoreCol.type?.ToString()?.ToUpper() == "INTEGER")
		{
			await connection.ExecuteAsync("PRAGMA foreign_keys = OFF");
			await connection.ExecuteAsync("\r\n                -- Migrate ReasoningTestResults (no FK constraints to avoid migration issues)\r\n                CREATE TABLE ReasoningTestResults_new (\r\n                    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n                    RunId INTEGER NOT NULL,\r\n                    ModelId INTEGER NOT NULL,\r\n                    TestId INTEGER NOT NULL,\r\n                    Response TEXT NOT NULL,\r\n                    OverallScore REAL,\r\n                    CorrectAnswerScore REAL,\r\n                    LogicalStepsScore REAL,\r\n                    ClarityScore REAL,\r\n                    JudgeReasoning TEXT,\r\n                    JudgeModelId INTEGER,\r\n                    FirstTokenMs REAL,\r\n                    TotalMs REAL NOT NULL,\r\n                    TokensPerSec REAL,\r\n                    PromptTokens INTEGER,\r\n                    CompletionTokens INTEGER,\r\n                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n                );\r\n                INSERT INTO ReasoningTestResults_new SELECT * FROM ReasoningTestResults;\r\n                DROP TABLE ReasoningTestResults;\r\n                ALTER TABLE ReasoningTestResults_new RENAME TO ReasoningTestResults;\r\n                CREATE INDEX IF NOT EXISTS idx_reasoning_results_run ON ReasoningTestResults(RunId);\r\n                CREATE INDEX IF NOT EXISTS idx_reasoning_results_model ON ReasoningTestResults(ModelId);\r\n            ");
			await connection.ExecuteAsync("PRAGMA foreign_keys = ON");
		}
		dynamic convOverallCol = (await connection.QueryAsync<object>("SELECT name, type FROM pragma_table_info('ConversationTestResults')")).FirstOrDefault((dynamic c) => c.name == "OverallScore");
		if (convOverallCol != null && convOverallCol.type?.ToString()?.ToUpper() == "INTEGER")
		{
			await connection.ExecuteAsync("PRAGMA foreign_keys = OFF");
			await connection.ExecuteAsync("\r\n                -- Migrate ConversationTestResults (no FK constraints)\r\n                CREATE TABLE ConversationTestResults_new (\r\n                    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n                    RunId INTEGER NOT NULL,\r\n                    ModelId INTEGER NOT NULL,\r\n                    TestId INTEGER NOT NULL,\r\n                    OverallScore REAL,\r\n                    TopicCoherence REAL,\r\n                    ConversationalTone REAL,\r\n                    ContextRetention REAL,\r\n                    Helpfulness REAL,\r\n                    JudgeReasoning TEXT,\r\n                    JudgeModelId INTEGER,\r\n                    TotalMs REAL,\r\n                    TokensPerSec REAL,\r\n                    PromptTokens INTEGER,\r\n                    CompletionTokens INTEGER,\r\n                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n                );\r\n                INSERT INTO ConversationTestResults_new\r\n                    (Id, RunId, ModelId, TestId, OverallScore, TopicCoherence, ConversationalTone, ContextRetention, Helpfulness, JudgeReasoning, JudgeModelId, TotalMs, CreatedAt)\r\n                    SELECT Id, RunId, ModelId, TestId, OverallScore, TopicCoherence, ConversationalTone, ContextRetention, Helpfulness, JudgeReasoning, JudgeModelId, TotalMs, CreatedAt FROM ConversationTestResults;\r\n                DROP TABLE ConversationTestResults;\r\n                ALTER TABLE ConversationTestResults_new RENAME TO ConversationTestResults;\r\n                CREATE INDEX IF NOT EXISTS idx_conversation_results_run ON ConversationTestResults(RunId);\r\n            ");
			await connection.ExecuteAsync("PRAGMA foreign_keys = ON");
		}
		dynamic scoreCol = (await connection.QueryAsync<object>("SELECT name, type FROM pragma_table_info('GenerationRatings')")).FirstOrDefault((dynamic c) => c.name == "Score");
		if (scoreCol != null && scoreCol.type?.ToString()?.ToUpper() == "INTEGER")
		{
			await connection.ExecuteAsync("PRAGMA foreign_keys = OFF");
			await connection.ExecuteAsync("\r\n                -- Migrate GenerationRatings (no FK constraints)\r\n                CREATE TABLE GenerationRatings_new (\r\n                    Id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n                    ResultId INTEGER NOT NULL,\r\n                    JudgeModelId INTEGER NOT NULL,\r\n                    Score REAL NOT NULL,\r\n                    Reasoning TEXT,\r\n                    IsBaseJudge INTEGER DEFAULT 0,\r\n                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP\r\n                );\r\n                INSERT INTO GenerationRatings_new SELECT * FROM GenerationRatings;\r\n                DROP TABLE GenerationRatings;\r\n                ALTER TABLE GenerationRatings_new RENAME TO GenerationRatings;\r\n                CREATE INDEX IF NOT EXISTS idx_generation_ratings_result ON GenerationRatings(ResultId);\r\n            ");
			await connection.ExecuteAsync("PRAGMA foreign_keys = ON");
		}
	}

	private async Task AddUniqueResultIndexesAsync(SqliteConnection connection)
	{
		if (!(await connection.QueryAsync<string>("SELECT name FROM sqlite_master WHERE type='index' AND name='idx_instruction_results_unique'")).Any())
		{
			await connection.ExecuteAsync("\r\n            DELETE FROM InstructionTestResults\r\n            WHERE Id NOT IN (\r\n                SELECT MAX(Id)\r\n                FROM InstructionTestResults\r\n                GROUP BY RunId, ModelId, TestId\r\n            );\r\n            CREATE UNIQUE INDEX IF NOT EXISTS idx_instruction_results_unique\r\n            ON InstructionTestResults(RunId, ModelId, TestId);\r\n        ");
			await connection.ExecuteAsync("\r\n            DELETE FROM ReasoningTestResults\r\n            WHERE Id NOT IN (\r\n                SELECT MAX(Id)\r\n                FROM ReasoningTestResults\r\n                GROUP BY RunId, ModelId, TestId\r\n            );\r\n            CREATE UNIQUE INDEX IF NOT EXISTS idx_reasoning_results_unique\r\n            ON ReasoningTestResults(RunId, ModelId, TestId);\r\n        ");
			await connection.ExecuteAsync("\r\n            DELETE FROM ConversationTestResults\r\n            WHERE Id NOT IN (\r\n                SELECT MAX(Id)\r\n                FROM ConversationTestResults\r\n                GROUP BY RunId, ModelId, TestId\r\n            );\r\n            CREATE UNIQUE INDEX IF NOT EXISTS idx_conversation_results_unique\r\n            ON ConversationTestResults(RunId, ModelId, TestId);\r\n        ");
			await connection.ExecuteAsync("\r\n            DELETE FROM GenerationResults\r\n            WHERE Id NOT IN (\r\n                SELECT MAX(Id)\r\n                FROM GenerationResults\r\n                GROUP BY RunId, ModelId, SeedId\r\n            );\r\n            CREATE UNIQUE INDEX IF NOT EXISTS idx_generation_results_unique\r\n            ON GenerationResults(RunId, ModelId, SeedId);\r\n        ");
			await connection.ExecuteAsync("\r\n            DELETE FROM ContextWindowTestResults\r\n            WHERE Id NOT IN (\r\n                SELECT MAX(Id)\r\n                FROM ContextWindowTestResults\r\n                GROUP BY RunId, ModelId, TestId\r\n            );\r\n            CREATE UNIQUE INDEX IF NOT EXISTS idx_context_results_unique\r\n            ON ContextWindowTestResults(RunId, ModelId, TestId);\r\n        ");
		}
	}
}
