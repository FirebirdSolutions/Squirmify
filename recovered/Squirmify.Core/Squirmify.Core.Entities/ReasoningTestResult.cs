using System;

namespace Squirmify.Core.Entities;

public class ReasoningTestResult
{
	public int Id { get; set; }

	public int RunId { get; set; }

	public int ModelId { get; set; }

	public int TestId { get; set; }

	public string Response { get; set; } = string.Empty;

	public double? OverallScore { get; set; }

	public double? CorrectAnswerScore { get; set; }

	public double? LogicalStepsScore { get; set; }

	public double? ClarityScore { get; set; }

	public string? JudgeReasoning { get; set; }

	public int? JudgeModelId { get; set; }

	public double? FirstTokenMs { get; set; }

	public double TotalMs { get; set; }

	public double? TokensPerSec { get; set; }

	public int? PromptTokens { get; set; }

	public int? CompletionTokens { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
