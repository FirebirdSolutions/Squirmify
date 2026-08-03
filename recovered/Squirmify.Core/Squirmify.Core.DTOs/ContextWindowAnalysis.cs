using System.Collections.Generic;

namespace Squirmify.Core.DTOs;

public class ContextWindowAnalysis
{
	public int TotalTests { get; set; }

	public int MaxReliableTokensAvg { get; set; }

	public int MaxReliableTokensMax { get; set; }

	public int MaxReliableTokensMin { get; set; }

	public double AvgCheckpointAccuracy { get; set; }

	public Dictionary<string, int> DegradationPatterns { get; set; } = new Dictionary<string, int>();

	public List<ModelContextSummary> ByModel { get; set; } = new List<ModelContextSummary>();
}
