namespace Squirmify.Core.DTOs;

public class ValidationTypeBreakdown
{
	public string ValidationType { get; set; } = string.Empty;

	public int Total { get; set; }

	public int Passed { get; set; }

	public double PassRate { get; set; }
}
