namespace ModelEvaluator;

/// <summary>
/// All configuration flags in one place - no command line args
/// </summary>
public static class Config
{
    // Server Settings
    public const string BaseUrl = "http://10.0.0.50:1234/v1";
    public const string BaseAuthToken = "sk-aimate-T0byGrace!";
    public const bool UseAuth = false;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(10);

    // Seed Generation
    public const int TargetSeedCount = 5;
    
    public const bool RunPromptTests = true;
    public const bool RunContextWindowTests = false;
    public const bool RunConversationTests = true;

    // File Paths
    public static readonly string ProjectPath = GetProjectPath();
    public static readonly string BaseSeedsFile = Path.Combine(ProjectPath, "base_seeds.jsonl");
    public static readonly string GeneratedSeedsFile = Path.Combine(ProjectPath, "seeds.json");
    public static readonly string OutputDir = Path.Combine(ProjectPath, "output");
    
    // Instruction Test Settings
    public const int MaxModelErrors = 3;
    public const double InstructionTestTemperature = 0.0;
    public const double InstructionTestTopP = 0.8;
    public const double InstructionTestPassThreshold = 0.8; // 80% pass rate required

    // Reasoning Test Settings
    public const double ReasoningTestMinScore = 7.0; // Minimum score out of 10

    // Conversation Test Settings
    public const double ConversationTestTemperature = 0.7;
    public const double ConversationTestTopP = 0.9;
    
    // Generation Settings
    public const double GlobalTemperature = 0.5;
    public const double GlobalTopP = 0.9;
    public const int GlobalMaxTokens = 512;
    
    // Category-specific defaults
    public static readonly Dictionary<string, CategoryDefaults> CategorySettings = new()
    {
        ["code"] = new(0.3, 2500, "You are a senior .NET engineer. Produce clear, correct, and self-contained C# code. Return only the code (no explanations, no headers, no backticks). Prefer minimal dependencies and idiomatic patterns. If the task implies I/O or setup, include that code too."),
        ["instruction"] = new(0.6, 800, "You are a patient teacher who explains concepts clearly."),
        ["chat"] = new(0.95, 800, "You are a friendly, casual kiwi conversational partner."),
        ["support"] = new(0.95, 1200, "You are a friendly, capable Kiwi support specialist. Be empathetic but efficient—avoid repeating yourself. Use everyday NZ English and finish with a reassuring closing line.")
    };
    
    // Scoring Settings
    public const double HighQualityThreshold = 7.5;
    public const int TopJudgeCount = 2;

    // Context Window Test Settings
    public const int ContextWindowDegradationThreshold_Graceful = 100_000;
    public const int ContextWindowDegradationThreshold_Moderate = 60_000;
    public const int ContextWindowDegradationThreshold_Sudden = 30_000;
    
    // Performance Settings
    public const int MaxParallelRequests = 1;
    
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
