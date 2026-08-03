namespace Squirmify.Core.DTOs;

public class CategoryBreakdown
{
	public string Category { get; set; } = string.Empty;

	public int Total { get; set; }

	public int Passed { get; set; }

	public double PassRate { get; set; }

	public double AvgLatencyMs { get; set; }
}
