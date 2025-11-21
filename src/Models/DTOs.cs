using System.Text.Json.Serialization;

namespace ModelEvaluator.Models;

// ========== API Communication ==========

public class Message
{
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    
    public Message() { }
    public Message(string role, string content) => (Role, Content) = (role, content);
}

public class ChatRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "";
    [JsonPropertyName("messages")] public List<Message> Messages { get; set; } = new();
    [JsonPropertyName("temperature")] public double? Temperature { get; set; }
    [JsonPropertyName("top_p")] public double? TopP { get; set; }
    [JsonPropertyName("max_tokens")] public int? MaxTokens { get; set; }
    [JsonPropertyName("stream")] public bool Stream { get; set; } = false;
}

public class Choice 
{ 
    [JsonPropertyName("message")] public Message Message { get; set; } = new();
}

public class Usage
{
    [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }
    [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
    [JsonPropertyName("total_tokens")] public int TotalTokens { get; set; }
}

public class ChatResponse
{
    [JsonPropertyName("choices")] public Choice[] Choices { get; set; } = Array.Empty<Choice>();
    [JsonPropertyName("usage")] public Usage? Usage { get; set; }
}

public class ModelInfo 
{ 
    [JsonPropertyName("id")] public string Id { get; set; } = "";
}

public class ModelsResponse 
{ 
    [JsonPropertyName("data")] public List<ModelInfo> Data { get; set; } = new();
}

// ========== Seeds & Prompts ==========

public class SeedItem
{
    public string instruction { get; set; } = "";
    public List<string> tags { get; set; } = new();
}

public class SeedEntry
{
    public string prompt { get; set; } = "";
    public string category { get; set; } = "general";
    public double? temperature { get; set; }
    public double? top_p { get; set; }
    public int? max_tokens { get; set; }
}

public class SeedsConfig
{
    public double temperature { get; set; } = Config.GlobalTemperature;
    public double top_p { get; set; } = Config.GlobalTopP;
    public int max_tokens { get; set; } = Config.GlobalMaxTokens;
    public string system_prompt { get; set; } = "You are a synthetic data teacher.";
    public Dictionary<string, string> system_prompts { get; set; } = new();
    public List<SeedEntry> seeds { get; set; } = new();
}

// ========== Instruction Tests ==========

public class InstructionTest
{
    public string Prompt { get; set; } = "";
    public string ExpectedResult { get; set; } = "";
    public string ValidationType { get; set; } = "exact";
}

public class InstructionTestResult
{
    public string ModelName { get; set; } = "";
    public int PassedTests { get; set; }
    public int TotalTests { get; set; }
    public double PassRate => TotalTests > 0 ? (double)PassedTests / TotalTests : 0;
    public double AvgTokensPerSec { get; set; }
    public double AvgLatencyMs { get; set; }
    public List<string> FailureDetails { get; set; } = new();
}

// ========== Results & Scoring ==========

public class PerfMetrics
{
    public double? first_token_ms { get; set; }
    public double total_ms { get; set; }
    public double? tokens_per_sec { get; set; }
    public int? prompt_tokens { get; set; }
    public int? completion_tokens { get; set; }
}

public class Rating
{
    public int score { get; set; }
    public string reasoning { get; set; } = "";
    public string rater { get; set; } = "";
}

public class GenerationResult
{
    public int id { get; set; }
    public string seed { get; set; } = "";
    public string category { get; set; } = "";
    public string generator { get; set; } = "";
    public string response { get; set; } = "";
    public double temperature { get; set; }
    public double top_p { get; set; }
    public int max_tokens { get; set; }
    public PerfMetrics perf { get; set; } = new();
    public List<Rating> ratings { get; set; } = new();
    public double avg_score => ratings.Any() ? ratings.Average(r => r.score) : 0.0;
    public bool high_quality => avg_score >= Config.HighQualityThreshold;
}

// ========== Summary Reports ==========

public class ModelSummary
{
    public string ModelName { get; set; } = "";
    public double AvgScore { get; set; }
    public double AvgTokensPerSec { get; set; }
    public double AvgLatencyMs { get; set; }
    public int HighQualityCount { get; set; }
    public string BestCategory { get; set; } = "";
}

/// <summary>
/// Represents a single turn in a conversation
/// </summary>
public class ConversationTurn
{
    public string UserMessage { get; set; } = "";
    public string? ExpectedTheme { get; set; } // Optional: what we expect the model to address
}

/// <summary>
/// Defines a multi-turn conversation test scenario
/// </summary>
public class ConversationTest
{
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public List<ConversationTurn> Turns { get; set; } = new();

    /// <summary>
    /// Criteria the judge should evaluate this conversation on
    /// </summary>
    public List<string> JudgingCriteria { get; set; } = new();
}

/// <summary>
/// Result of a complete conversation test
/// </summary>
public class ConversationTestResult
{
    public string ModelName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ConversationExchange> Exchanges { get; set; } = new();
    public PerfMetrics AggregatePerf { get; set; } = new();
    public ConversationRating? Rating { get; set; }
}

/// <summary>
/// A single user message + model response pair
/// </summary>
public class ConversationExchange
{
    public int TurnNumber { get; set; }
    public string UserMessage { get; set; } = "";
    public string ModelResponse { get; set; } = "";
    public PerfMetrics Perf { get; set; } = new();
}

/// <summary>
/// Judge's rating of an entire conversation
/// </summary>
public class ConversationRating
{
    public int OverallScore { get; set; } // 1-10
    public string Reasoning { get; set; } = "";
    public string Rater { get; set; } = "";

    // Detailed scoring
    public int TopicCoherence { get; set; } // 1-10
    public int ConversationalTone { get; set; } // 1-10
    public int ContextRetention { get; set; } // 1-10
    public int Helpfulness { get; set; } // 1-10
}

/// <summary>
/// Summary of all conversation tests for a model
/// </summary>
public class ConversationTestSummary
{
    public string ModelName { get; set; } = "";
    public double AvgOverallScore { get; set; }
    public double AvgTokensPerSec { get; set; }
    public double AvgLatencyMs { get; set; }
    public Dictionary<string, double> CategoryScores { get; set; } = new();
    public int TotalConversations { get; set; }
}

