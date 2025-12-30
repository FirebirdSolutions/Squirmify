using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

namespace ModelEvaluator.Services;

#region Config Models

/// <summary>
/// Configuration for instruction tests loaded from JSON
/// </summary>
public class InstructionTestsConfig
{
    [JsonPropertyName("tests")]
    public List<InstructionTestDefinition> Tests { get; set; } = new();

    [JsonPropertyName("systemPrompt")]
    public string SystemPrompt { get; set; } = "";
}

public class InstructionTestDefinition
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("expectedResult")]
    public string ExpectedResult { get; set; } = "";

    [JsonPropertyName("validationType")]
    public string ValidationType { get; set; } = "exact";

    [JsonPropertyName("strictOrder")]
    public bool StrictOrder { get; set; } = true;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";
}

/// <summary>
/// Configuration for reasoning tests loaded from JSON
/// </summary>
public class ReasoningTestsConfig
{
    [JsonPropertyName("tests")]
    public List<ReasoningTestDefinition> Tests { get; set; } = new();

    [JsonPropertyName("systemPrompt")]
    public string SystemPrompt { get; set; } = "";

    [JsonPropertyName("judgeSystemPrompt")]
    public string JudgeSystemPrompt { get; set; } = "";

    [JsonPropertyName("judgePromptTemplate")]
    public string JudgePromptTemplate { get; set; } = "";
}

public class ReasoningTestDefinition
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("correctAnswer")]
    public string CorrectAnswer { get; set; } = "";
}

/// <summary>
/// Configuration for conversation tests loaded from JSON
/// </summary>
public class ConversationTestsConfig
{
    [JsonPropertyName("tests")]
    public List<ConversationTestDefinition> Tests { get; set; } = new();

    [JsonPropertyName("judgeSystemPrompt")]
    public string JudgeSystemPrompt { get; set; } = "";

    [JsonPropertyName("judgePromptTemplate")]
    public string JudgePromptTemplate { get; set; } = "";
}

public class ConversationTestDefinition
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("systemPrompt")]
    public string SystemPrompt { get; set; } = "";

    [JsonPropertyName("turns")]
    public List<ConversationTurnDefinition> Turns { get; set; } = new();

    [JsonPropertyName("judgingCriteria")]
    public List<string> JudgingCriteria { get; set; } = new();
}

public class ConversationTurnDefinition
{
    [JsonPropertyName("userMessage")]
    public string UserMessage { get; set; } = "";

    [JsonPropertyName("expectedTheme")]
    public string? ExpectedTheme { get; set; }
}

/// <summary>
/// Configuration for context window tests loaded from JSON
/// </summary>
public class ContextWindowTestsConfig
{
    [JsonPropertyName("level")]
    public string Level { get; set; } = "shallow";

    [JsonPropertyName("levels")]
    public Dictionary<string, ContextWindowLevelConfig> Levels { get; set; } = new();

    [JsonPropertyName("tests")]
    public List<ContextWindowTestDefinition> Tests { get; set; } = new();

    [JsonPropertyName("checkpointTemplates")]
    public List<string> CheckpointTemplates { get; set; } = new();

    [JsonPropertyName("fillerContent")]
    public FillerContentConfig FillerContent { get; set; } = new();

    /// <summary>
    /// Get the token multiplier for the configured level
    /// </summary>
    public double GetTokenMultiplier()
    {
        var level = Config.ContextWindowTestLevel; // Use settings.json level if set
        if (Levels.TryGetValue(level, out var config))
            return config.TokenMultiplier;
        return 1.0;
    }
}

public class ContextWindowLevelConfig
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("tokenMultiplier")]
    public double TokenMultiplier { get; set; } = 1.0;
}

public class ContextWindowTestDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("fillerType")]
    public string FillerType { get; set; } = "mixed";

    [JsonPropertyName("targetTokens")]
    public int TargetTokens { get; set; }

    [JsonPropertyName("baseTargetTokens")]
    public int BaseTargetTokens { get; set; }

    [JsonPropertyName("baseCheckpointCount")]
    public int? BaseCheckpointCount { get; set; }

    [JsonPropertyName("checkpointCount")]
    public int? CheckpointCount { get; set; }

    [JsonPropertyName("checkpoints")]
    public List<CheckpointDefinition>? Checkpoints { get; set; }

    [JsonPropertyName("buriedInstruction")]
    public string? BuriedInstruction { get; set; }

    /// <summary>
    /// Get effective target tokens (uses base if targetTokens not set)
    /// </summary>
    public int GetEffectiveTargetTokens() => TargetTokens > 0 ? TargetTokens : BaseTargetTokens;

    /// <summary>
    /// Get effective checkpoint count
    /// </summary>
    public int? GetEffectiveCheckpointCount() => CheckpointCount ?? BaseCheckpointCount;
}

public class CheckpointDefinition
{
    [JsonPropertyName("targetTokenPosition")]
    public int TargetTokenPosition { get; set; }

    [JsonPropertyName("relativePosition")]
    public double? RelativePosition { get; set; }

    [JsonPropertyName("secretWord")]
    public string SecretWord { get; set; } = "";

    [JsonPropertyName("carrierSentence")]
    public string CarrierSentence { get; set; } = "";
}

public class FillerContentConfig
{
    [JsonPropertyName("code")]
    public List<string> Code { get; set; } = new();

    [JsonPropertyName("prose")]
    public List<string> Prose { get; set; } = new();

    [JsonPropertyName("technical")]
    public List<string> Technical { get; set; } = new();
}

/// <summary>
/// Configuration for system prompts loaded from JSON
/// </summary>
public class SystemPromptsConfig
{
    [JsonPropertyName("categoryPrompts")]
    public Dictionary<string, string> CategoryPrompts { get; set; } = new();

    [JsonPropertyName("testPrompts")]
    public Dictionary<string, string> TestPrompts { get; set; } = new();

    [JsonPropertyName("judgePrompts")]
    public Dictionary<string, string> JudgePrompts { get; set; } = new();

    [JsonPropertyName("judgePromptTemplates")]
    public Dictionary<string, string> JudgePromptTemplates { get; set; } = new();

    // Convenience getters with fallbacks
    public string GetTestPrompt(string key, string fallback = "") =>
        TestPrompts.TryGetValue(key, out var prompt) ? prompt : fallback;

    public string GetJudgePrompt(string key, string fallback = "") =>
        JudgePrompts.TryGetValue(key, out var prompt) ? prompt : fallback;

    public string GetJudgeTemplate(string key, string fallback = "") =>
        JudgePromptTemplates.TryGetValue(key, out var template) ? template : fallback;
}

/// <summary>
/// Helper class for filtering tests by category limits
/// </summary>
public static class TestFilterHelper
{
    /// <summary>
    /// Filter tests by category limits from config
    /// </summary>
    public static List<T> FilterByCategory<T>(
        List<T> tests,
        Func<T, string> categorySelector,
        int defaultMax,
        Dictionary<string, int> categoryLimits)
    {
        var result = new List<T>();
        var categoryCounts = new Dictionary<string, int>();

        foreach (var test in tests)
        {
            var category = categorySelector(test);
            var limit = categoryLimits.TryGetValue(category, out var specificLimit)
                ? specificLimit
                : defaultMax;

            if (!categoryCounts.TryGetValue(category, out var count))
                count = 0;

            if (count < limit)
            {
                result.Add(test);
                categoryCounts[category] = count + 1;
            }
        }

        return result;
    }
}

/// <summary>
/// Configuration for seed augmentation loaded from JSON
/// </summary>
public class SeedAugmentationConfig
{
    [JsonPropertyName("kiwiPhrases")]
    public Dictionary<string, List<string>> KiwiPhrases { get; set; } = new();

    [JsonPropertyName("contextSuffixes")]
    public List<string> ContextSuffixes { get; set; } = new();

    [JsonPropertyName("supportSuffixes")]
    public List<string> SupportSuffixes { get; set; } = new();

    [JsonPropertyName("verbParaphrases")]
    public Dictionary<string, List<string>> VerbParaphrases { get; set; } = new();

    [JsonPropertyName("kiwiCasualStarters")]
    public List<string> KiwiCasualStarters { get; set; } = new();

    [JsonPropertyName("kiwiCasualEndings")]
    public List<string> KiwiCasualEndings { get; set; } = new();

    [JsonPropertyName("complexityModifiers")]
    public List<string> ComplexityModifiers { get; set; } = new();
}

#endregion

/// <summary>
/// Service for loading configuration from external JSON files
/// </summary>
public class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly string ConfigPath = Path.Combine(Config.ProjectPath, "config");

    // Cached configurations
    private static InstructionTestsConfig? _instructionTestsConfig;
    private static ReasoningTestsConfig? _reasoningTestsConfig;
    private static ConversationTestsConfig? _conversationTestsConfig;
    private static ContextWindowTestsConfig? _contextWindowTestsConfig;
    private static SystemPromptsConfig? _systemPromptsConfig;
    private static SeedAugmentationConfig? _seedAugmentationConfig;

    /// <summary>
    /// Load instruction tests configuration
    /// </summary>
    public static async Task<InstructionTestsConfig> LoadInstructionTestsAsync()
    {
        if (_instructionTestsConfig != null) return _instructionTestsConfig;

        var filePath = Path.Combine(ConfigPath, "tests", "instruction_tests.json");
        _instructionTestsConfig = await LoadJsonAsync<InstructionTestsConfig>(filePath, "instruction tests");
        return _instructionTestsConfig ?? new InstructionTestsConfig();
    }

    /// <summary>
    /// Load reasoning tests configuration
    /// </summary>
    public static async Task<ReasoningTestsConfig> LoadReasoningTestsAsync()
    {
        if (_reasoningTestsConfig != null) return _reasoningTestsConfig;

        var filePath = Path.Combine(ConfigPath, "tests", "reasoning_tests.json");
        _reasoningTestsConfig = await LoadJsonAsync<ReasoningTestsConfig>(filePath, "reasoning tests");
        return _reasoningTestsConfig ?? new ReasoningTestsConfig();
    }

    /// <summary>
    /// Load conversation tests configuration
    /// </summary>
    public static async Task<ConversationTestsConfig> LoadConversationTestsAsync()
    {
        if (_conversationTestsConfig != null) return _conversationTestsConfig;

        var filePath = Path.Combine(ConfigPath, "tests", "conversation_tests.json");
        _conversationTestsConfig = await LoadJsonAsync<ConversationTestsConfig>(filePath, "conversation tests");
        return _conversationTestsConfig ?? new ConversationTestsConfig();
    }

    /// <summary>
    /// Load context window tests configuration
    /// </summary>
    public static async Task<ContextWindowTestsConfig> LoadContextWindowTestsAsync()
    {
        if (_contextWindowTestsConfig != null) return _contextWindowTestsConfig;

        var filePath = Path.Combine(ConfigPath, "tests", "context_window_tests.json");
        _contextWindowTestsConfig = await LoadJsonAsync<ContextWindowTestsConfig>(filePath, "context window tests");
        return _contextWindowTestsConfig ?? new ContextWindowTestsConfig();
    }

    /// <summary>
    /// Load system prompts configuration
    /// </summary>
    public static async Task<SystemPromptsConfig> LoadSystemPromptsAsync()
    {
        if (_systemPromptsConfig != null) return _systemPromptsConfig;

        var filePath = Path.Combine(ConfigPath, "prompts", "system_prompts.json");
        _systemPromptsConfig = await LoadJsonAsync<SystemPromptsConfig>(filePath, "system prompts");
        return _systemPromptsConfig ?? new SystemPromptsConfig();
    }

    /// <summary>
    /// Load seed augmentation configuration
    /// </summary>
    public static async Task<SeedAugmentationConfig> LoadSeedAugmentationAsync()
    {
        if (_seedAugmentationConfig != null) return _seedAugmentationConfig;

        var filePath = Path.Combine(ConfigPath, "augmentation", "seed_augmentation.json");
        _seedAugmentationConfig = await LoadJsonAsync<SeedAugmentationConfig>(filePath, "seed augmentation");
        return _seedAugmentationConfig ?? new SeedAugmentationConfig();
    }

    /// <summary>
    /// Clear all cached configurations (useful for reloading)
    /// </summary>
    public static void ClearCache()
    {
        _instructionTestsConfig = null;
        _reasoningTestsConfig = null;
        _conversationTestsConfig = null;
        _contextWindowTestsConfig = null;
        _systemPromptsConfig = null;
        _seedAugmentationConfig = null;
    }

    /// <summary>
    /// Generic JSON file loader with error handling
    /// </summary>
    private static async Task<T?> LoadJsonAsync<T>(string filePath, string configName) where T : class
    {
        try
        {
            if (!File.Exists(filePath))
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ {configName} config not found: {filePath}[/]");
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<T>(json, JsonOptions);

            if (config != null)
            {
                AnsiConsole.MarkupLine($"[green]✓ Loaded {configName} config[/]");
            }

            return config;
        }
        catch (JsonException ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Failed to parse {configName} config: {ex.Message}[/]");
            return null;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error loading {configName} config: {ex.Message}[/]");
            return null;
        }
    }
}
