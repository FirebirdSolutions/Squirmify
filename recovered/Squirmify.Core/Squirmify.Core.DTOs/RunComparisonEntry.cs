using System;

namespace Squirmify.Core.DTOs;

public class RunComparisonEntry
{
	public int RunId { get; set; }

	public string? RunName { get; set; }

	public DateTime? StartedAt { get; set; }

	public double InstructionPassRate { get; set; }

	public double ReasoningAvgScore { get; set; }

	public double ConversationAvgScore { get; set; }

	public double GenerationAvgScore { get; set; }

	public double CompositeScore { get; set; }

	public double AvgTokensPerSec { get; set; }

	public int ModelCount { get; set; }
}
