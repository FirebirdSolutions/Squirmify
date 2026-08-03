namespace Squirmify.Core.DTOs;

public class ModelSummary
{
	public int ModelId { get; set; }

	public string ModelName { get; set; } = string.Empty;

	public double InstructionPassRate { get; set; }

	public double ReasoningAvgScore { get; set; }

	public double ConversationAvgScore { get; set; }

	public double GenerationAvgScore { get; set; }

	public double CompositeScore { get; set; }

	public double AvgTokensPerSec { get; set; }

	public double InstructionAvgTokensPerSec { get; set; }

	public double GenerationAvgTokensPerSec { get; set; }

	public double AvgLatencyMs { get; set; }

	public int TotalTests { get; set; }

	public int PassedTests { get; set; }
}
