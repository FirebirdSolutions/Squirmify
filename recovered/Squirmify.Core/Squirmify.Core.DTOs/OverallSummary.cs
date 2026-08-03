namespace Squirmify.Core.DTOs;

public class OverallSummary
{
	public int TotalTests { get; set; }

	public int CompletedTests { get; set; }

	public int ModelCount { get; set; }

	public double InstructionPassRate { get; set; }

	public double ReasoningAvgScore { get; set; }

	public double ConversationAvgScore { get; set; }

	public double GenerationAvgScore { get; set; }

	public int HighQualityCount { get; set; }

	public double CompositeScore { get; set; }
}
