namespace Squirmify.Core.DTOs;

public class ModelRunScore
{
	public int RunId { get; set; }

	public double InstructionPassRate { get; set; }

	public double ReasoningAvgScore { get; set; }

	public double ConversationAvgScore { get; set; }

	public double CompositeScore { get; set; }

	public double? Delta { get; set; }
}
