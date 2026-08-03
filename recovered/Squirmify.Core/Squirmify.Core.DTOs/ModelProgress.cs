namespace Squirmify.Core.DTOs;

public class ModelProgress
{
	public int ModelId { get; set; }

	public string ModelName { get; set; } = string.Empty;

	public string Status { get; set; } = "pending";

	public double? InstructionPassRate { get; set; }

	public double? ReasoningScore { get; set; }

	public bool? QualificationPassed { get; set; }
}
