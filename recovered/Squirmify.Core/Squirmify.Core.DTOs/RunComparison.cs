using System.Collections.Generic;

namespace Squirmify.Core.DTOs;

public class RunComparison
{
	public List<RunComparisonEntry> Runs { get; set; } = new List<RunComparisonEntry>();

	public List<ModelComparisonAcrossRuns> ModelComparisons { get; set; } = new List<ModelComparisonAcrossRuns>();
}
