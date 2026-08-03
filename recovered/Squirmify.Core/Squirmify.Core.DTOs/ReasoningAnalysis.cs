using System.Collections.Generic;

namespace Squirmify.Core.DTOs;

public class ReasoningAnalysis
{
	public int TotalTests { get; set; }

	public double AvgOverallScore { get; set; }

	public double AvgCorrectAnswerScore { get; set; }

	public double AvgLogicalStepsScore { get; set; }

	public double AvgClarityScore { get; set; }

	public List<CategoryBreakdown> ByCategory { get; set; } = new List<CategoryBreakdown>();

	public ScoreDistribution ScoreDistribution { get; set; } = new ScoreDistribution();
}
