using System.Collections.Generic;

namespace Squirmify.Core.DTOs;

public class ConversationAnalysis
{
	public int TotalTests { get; set; }

	public double AvgOverallScore { get; set; }

	public double AvgTopicCoherence { get; set; }

	public double AvgConversationalTone { get; set; }

	public double AvgContextRetention { get; set; }

	public double AvgHelpfulness { get; set; }

	public List<CategoryBreakdown> ByCategory { get; set; } = new List<CategoryBreakdown>();
}
