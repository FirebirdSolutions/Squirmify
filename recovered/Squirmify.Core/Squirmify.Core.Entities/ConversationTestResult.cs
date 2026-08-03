using System;

namespace Squirmify.Core.Entities;

public class ConversationTestResult
{
	public int Id { get; set; }

	public int RunId { get; set; }

	public int ModelId { get; set; }

	public int TestId { get; set; }

	public double? OverallScore { get; set; }

	public double? TopicCoherence { get; set; }

	public double? ConversationalTone { get; set; }

	public double? ContextRetention { get; set; }

	public double? Helpfulness { get; set; }

	public string? JudgeReasoning { get; set; }

	public int? JudgeModelId { get; set; }

	public double? TotalMs { get; set; }

	public double? TokensPerSec { get; set; }

	public int? PromptTokens { get; set; }

	public int? CompletionTokens { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
