using System.Collections.Generic;

namespace Squirmify.Core.DTOs;

public class ModelComparisonAcrossRuns
{
	public int ModelId { get; set; }

	public string ModelName { get; set; } = string.Empty;

	public List<ModelRunScore> RunScores { get; set; } = new List<ModelRunScore>();
}
