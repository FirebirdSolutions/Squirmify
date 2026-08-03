namespace Squirmify.Core.Entities;

public class BenchmarkRunModel
{
	public int Id { get; set; }

	public int RunId { get; set; }

	public int ModelId { get; set; }

	public string Status { get; set; } = "pending";

	public bool? QualificationPassed { get; set; }

	public double? InstructionPassRate { get; set; }

	public double? InstructionStrictPassRate { get; set; }

	public double? ReasoningAvgScore { get; set; }

	public double? ContextWindowAvgReliability { get; set; }

	public double? ContextWindowAvgAccuracy { get; set; }

	public int ContextWindowTestCount { get; set; }

	public bool IsBaseJudge { get; set; }

	public bool IsAutoJudge { get; set; }
}
