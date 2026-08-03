using System.Collections.Generic;

namespace Squirmify.Core.DTOs;

public class InstructionAnalysis
{
	public int TotalTests { get; set; }

	public int PassedTests { get; set; }

	public int StrictPassedTests { get; set; }

	public double PassRate { get; set; }

	public double StrictPassRate { get; set; }

	public List<CategoryBreakdown> ByCategory { get; set; } = new List<CategoryBreakdown>();

	public List<FailureReason> TopFailures { get; set; } = new List<FailureReason>();

	public List<ValidationTypeBreakdown> ByValidationType { get; set; } = new List<ValidationTypeBreakdown>();
}
