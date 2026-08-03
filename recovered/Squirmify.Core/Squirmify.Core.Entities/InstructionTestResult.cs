using System;

namespace Squirmify.Core.Entities;

public class InstructionTestResult
{
	public int Id { get; set; }

	public int RunId { get; set; }

	public int ModelId { get; set; }

	public int TestId { get; set; }

	public bool Passed { get; set; }

	public bool StrictPass { get; set; }

	public bool LenientPass { get; set; }

	public string? Response { get; set; }

	public string? FailureReason { get; set; }

	public double? FirstTokenMs { get; set; }

	public double TotalMs { get; set; }

	public double? TokensPerSec { get; set; }

	public int? PromptTokens { get; set; }

	public int? CompletionTokens { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
