using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModelEvaluator;

#region Config Models

public class SettingsConfig
{
    [JsonPropertyName("server")]
    public ServerSettings Server { get; set; } = new();

    [JsonPropertyName("testSuites")]
    public TestSuiteSettings TestSuites { get; set; } = new();

    [JsonPropertyName("seedGeneration")]
    public SeedGenerationSettings SeedGeneration { get; set; } = new();

    [JsonPropertyName("instructionTests")]
    public InstructionTestSettings InstructionTests { get; set; } = new();

    [JsonPropertyName("reasoningTests")]
    public ReasoningTestSettings ReasoningTests { get; set; } = new();

    [JsonPropertyName("conversationTests")]
    public ConversationTestSettings ConversationTests { get; set; } = new();

    [JsonPropertyName("generation")]
    public GenerationSettings Generation { get; set; } = new();

    [JsonPropertyName("categorySettings")]
    public Dictionary<string, CategorySettingsEntry> CategorySettings { get; set; } = new();

    [JsonPropertyName("scoring")]
    public ScoringSettings Scoring { get; set; } = new();

    [JsonPropertyName("judging")]
    public JudgingSettings Judging { get; set; } = new();

    [JsonPropertyName("contextWindowTests")]
    public ContextWindowTestSettings ContextWindowTests { get; set; } = new();

    [JsonPropertyName("performance")]
    public PerformanceSettings Performance { get; set; } = new();

    [JsonPropertyName("output")]
    public OutputSettings Output { get; set; } = new();
}

public class ServerSettings
{
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "http://localhost:1234/v1";

    [JsonPropertyName("authToken")]
    public string AuthToken { get; set; } = "";

    [JsonPropertyName("useAuth")]
    public bool UseAuth { get; set; } = false;

    [JsonPropertyName("requestTimeoutMinutes")]
    public int RequestTimeoutMinutes { get; set; } = 10;
}

public class TestSuiteSettings
{
    [JsonPropertyName("runPromptTests")]
    public bool RunPromptTests { get; set; } = true;

    [JsonPropertyName("runContextWindowTests")]
    public bool RunContextWindowTests { get; set; } = false;

    [JsonPropertyName("runConversationTests")]
    public bool RunConversationTests { get; set; } = true;

    [JsonPropertyName("runQualificationTests")]
    public bool RunQualificationTests { get; set; } = true;
}

public class SeedGenerationSettings
{
    [JsonPropertyName("targetSeedCount")]
    public int TargetSeedCount { get; set; } = 5;

    [JsonPropertyName("baseSeedsFile")]
    public string BaseSeedsFile { get; set; } = "base_seeds.jsonl";

    [JsonPropertyName("generatedSeedsFile")]
    public string GeneratedSeedsFile { get; set; } = "seeds.json";

    [JsonPropertyName("overwriteSeeds")]
    public bool OverwriteSeeds { get; set; } = true;

    [JsonPropertyName("categoryWeights")]
    public Dictionary<string, double> CategoryWeights { get; set; } = new()
    {
        ["code"] = 0.25,
        ["instruction"] = 0.25,
        ["chat"] = 0.25,
        ["support"] = 0.25
    };
}

public class InstructionTestSettings
{
    [JsonPropertyName("maxModelErrors")]
    public int MaxModelErrors { get; set; } = 3;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.0;

    [JsonPropertyName("topP")]
    public double TopP { get; set; } = 0.8;

    [JsonPropertyName("passThreshold")]
    public double PassThreshold { get; set; } = 0.8;

    [JsonPropertyName("maxTestsPerCategory")]
    public int MaxTestsPerCategory { get; set; } = 5;

    [JsonPropertyName("categoryLimits")]
    public Dictionary<string, int> CategoryLimits { get; set; } = new();
}

public class ReasoningTestSettings
{
    [JsonPropertyName("minScore")]
    public double MinScore { get; set; } = 7.0;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.3;

    [JsonPropertyName("topP")]
    public double TopP { get; set; } = 0.9;

    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; set; } = 600;

    [JsonPropertyName("maxTestsPerCategory")]
    public int MaxTestsPerCategory { get; set; } = 3;

    [JsonPropertyName("categoryLimits")]
    public Dictionary<string, int> CategoryLimits { get; set; } = new();
}

public class ConversationTestSettings
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;

    [JsonPropertyName("topP")]
    public double TopP { get; set; } = 0.9;

    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; set; } = 600;

    [JsonPropertyName("maxTestsPerCategory")]
    public int MaxTestsPerCategory { get; set; } = 3;

    [JsonPropertyName("categoryLimits")]
    public Dictionary<string, int> CategoryLimits { get; set; } = new();
}

public class GenerationSettings
{
    [JsonPropertyName("globalTemperature")]
    public double GlobalTemperature { get; set; } = 0.5;

    [JsonPropertyName("globalTopP")]
    public double GlobalTopP { get; set; } = 0.9;

    [JsonPropertyName("globalMaxTokens")]
    public int GlobalMaxTokens { get; set; } = 512;
}

public class CategorySettingsEntry
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("systemPrompt")]
    public string SystemPrompt { get; set; } = "";
}

public class ScoringSettings
{
    [JsonPropertyName("highQualityThreshold")]
    public double HighQualityThreshold { get; set; } = 7.5;

    [JsonPropertyName("topJudgeCount")]
    public int TopJudgeCount { get; set; } = 2;
}

public class JudgingSettings
{
    [JsonPropertyName("scoreOnly")]
    public bool ScoreOnly { get; set; } = false;

    [JsonPropertyName("scoreOnlyInputFile")]
    public string ScoreOnlyInputFile { get; set; } = "";

    [JsonPropertyName("overrideBaseJudge")]
    public string OverrideBaseJudge { get; set; } = "";

    [JsonPropertyName("overrideAutoJudges")]
    public List<string> OverrideAutoJudges { get; set; } = new();
}

public class ContextWindowTestSettings
{
    [JsonPropertyName("level")]
    public string Level { get; set; } = "shallow";

    [JsonPropertyName("maxTests")]
    public int MaxTests { get; set; } = 5;

    [JsonPropertyName("degradationThresholds")]
    public DegradationThresholds DegradationThresholds { get; set; } = new();
}

public class DegradationThresholds
{
    [JsonPropertyName("graceful")]
    public int Graceful { get; set; } = 100000;

    [JsonPropertyName("moderate")]
    public int Moderate { get; set; } = 60000;

    [JsonPropertyName("sudden")]
    public int Sudden { get; set; } = 30000;
}

public class PerformanceSettings
{
    [JsonPropertyName("maxParallelRequests")]
    public int MaxParallelRequests { get; set; } = 1;
}

public class OutputSettings
{
    [JsonPropertyName("directory")]
    public string Directory { get; set; } = "output";
}

#endregion

/// <summary>
/// All configuration flags in one place - loaded from config/settings.json
/// </summary>
public static class Config
{
    private static SettingsConfig? _settings;
    private static bool _initialized = false;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Initialize configuration from settings.json
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        var configPath = Path.Combine(ProjectPath, "config", "settings.json");

        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                _settings = JsonSerializer.Deserialize<SettingsConfig>(json, JsonOptions);
                Console.WriteLine($"✓ Loaded settings from {configPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Failed to load settings: {ex.Message}. Using defaults.");
                _settings = new SettingsConfig();
            }
        }
        else
        {
            Console.WriteLine($"⚠ Settings file not found: {configPath}. Using defaults.");
            _settings = new SettingsConfig();
        }

        _initialized = true;
        InitializeDerivedSettings();
    }

    private static void InitializeDerivedSettings()
    {
        // Build CategorySettings dictionary from loaded config
        _categorySettings = new Dictionary<string, CategoryDefaults>();

        foreach (var kvp in Settings.CategorySettings)
        {
            _categorySettings[kvp.Key] = new CategoryDefaults(
                kvp.Value.Temperature,
                kvp.Value.MaxTokens,
                kvp.Value.SystemPrompt
            );
        }

        // Add defaults if not present in config
        if (!_categorySettings.ContainsKey("code"))
            _categorySettings["code"] = new(0.3, 2500, "You are a senior .NET engineer. Produce clear, correct, and self-contained C# code.");
        if (!_categorySettings.ContainsKey("instruction"))
            _categorySettings["instruction"] = new(0.6, 800, "You are a patient teacher who explains concepts clearly.");
        if (!_categorySettings.ContainsKey("chat"))
            _categorySettings["chat"] = new(0.95, 800, "You are a friendly, casual kiwi conversational partner.");
        if (!_categorySettings.ContainsKey("support"))
            _categorySettings["support"] = new(0.95, 1200, "You are a friendly, capable Kiwi support specialist.");
    }

    private static SettingsConfig Settings
    {
        get
        {
            if (!_initialized) Initialize();
            return _settings!;
        }
    }

    // Server Settings
    public static string BaseUrl => Settings.Server.BaseUrl;
    public static string BaseAuthToken => Settings.Server.AuthToken;
    public static bool UseAuth => Settings.Server.UseAuth;
    public static TimeSpan RequestTimeout => TimeSpan.FromMinutes(Settings.Server.RequestTimeoutMinutes);

    // Test Suite Toggles
    public static bool RunPromptTests => Settings.TestSuites.RunPromptTests;
    public static bool RunContextWindowTests => Settings.TestSuites.RunContextWindowTests;
    public static bool RunConversationTests => Settings.TestSuites.RunConversationTests;
    public static bool RunQualificationTests => Settings.TestSuites.RunQualificationTests;

    // Seed Generation
    public static int TargetSeedCount => Settings.SeedGeneration.TargetSeedCount;
    public static bool OverwriteSeeds => Settings.SeedGeneration.OverwriteSeeds;
    public static Dictionary<string, double> CategoryWeights => Settings.SeedGeneration.CategoryWeights;

    // File Paths
    public static readonly string ProjectPath = GetProjectPath();
    public static string BaseSeedsFile => Path.Combine(ProjectPath, Settings.SeedGeneration.BaseSeedsFile);
    public static string GeneratedSeedsFile => Path.Combine(ProjectPath, Settings.SeedGeneration.GeneratedSeedsFile);
    public static string OutputDir => Path.Combine(ProjectPath, Settings.Output.Directory);

    // Instruction Test Settings
    public static int MaxModelErrors => Settings.InstructionTests.MaxModelErrors;
    public static double InstructionTestTemperature => Settings.InstructionTests.Temperature;
    public static double InstructionTestTopP => Settings.InstructionTests.TopP;
    public static double InstructionTestPassThreshold => Settings.InstructionTests.PassThreshold;
    public static int InstructionTestMaxPerCategory => Settings.InstructionTests.MaxTestsPerCategory;
    public static Dictionary<string, int> InstructionTestCategoryLimits => Settings.InstructionTests.CategoryLimits;

    // Reasoning Test Settings
    public static double ReasoningTestMinScore => Settings.ReasoningTests.MinScore;
    public static int ReasoningTestMaxPerCategory => Settings.ReasoningTests.MaxTestsPerCategory;
    public static Dictionary<string, int> ReasoningTestCategoryLimits => Settings.ReasoningTests.CategoryLimits;

    // Conversation Test Settings
    public static double ConversationTestTemperature => Settings.ConversationTests.Temperature;
    public static double ConversationTestTopP => Settings.ConversationTests.TopP;
    public static int ConversationTestMaxPerCategory => Settings.ConversationTests.MaxTestsPerCategory;
    public static Dictionary<string, int> ConversationTestCategoryLimits => Settings.ConversationTests.CategoryLimits;

    // Generation Settings
    public static double GlobalTemperature => Settings.Generation.GlobalTemperature;
    public static double GlobalTopP => Settings.Generation.GlobalTopP;
    public static int GlobalMaxTokens => Settings.Generation.GlobalMaxTokens;

    // Category Settings
    private static Dictionary<string, CategoryDefaults> _categorySettings = new();
    public static Dictionary<string, CategoryDefaults> CategorySettings
    {
        get
        {
            if (!_initialized) Initialize();
            return _categorySettings;
        }
    }

    // Scoring Settings
    public static double HighQualityThreshold => Settings.Scoring.HighQualityThreshold;
    public static int TopJudgeCount => Settings.Scoring.TopJudgeCount;

    // Judging Settings
    public static bool ScoreOnly => Settings.Judging.ScoreOnly;
    public static string ScoreOnlyInputFile => Settings.Judging.ScoreOnlyInputFile;
    public static string OverrideBaseJudge => Settings.Judging.OverrideBaseJudge;
    public static List<string> OverrideAutoJudges => Settings.Judging.OverrideAutoJudges;

    // Context Window Test Settings
    public static string ContextWindowTestLevel => Settings.ContextWindowTests.Level;
    public static int ContextWindowTestMaxTests => Settings.ContextWindowTests.MaxTests;
    public static int ContextWindowDegradationThreshold_Graceful => Settings.ContextWindowTests.DegradationThresholds.Graceful;
    public static int ContextWindowDegradationThreshold_Moderate => Settings.ContextWindowTests.DegradationThresholds.Moderate;
    public static int ContextWindowDegradationThreshold_Sudden => Settings.ContextWindowTests.DegradationThresholds.Sudden;

    // Performance Settings
    public static int MaxParallelRequests => Settings.Performance.MaxParallelRequests;

    private static string GetProjectPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir != null; i++)
        {
            if (dir.GetFiles("*.csproj").Any())
                return dir.FullName;
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }
}

public record CategoryDefaults(double Temperature, int MaxTokens, string SystemPrompt);
