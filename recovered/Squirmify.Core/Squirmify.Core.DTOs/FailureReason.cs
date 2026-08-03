namespace Squirmify.Core.DTOs;

public class FailureReason
{
	public string Reason { get; set; } = string.Empty;

	public int Count { get; set; }

	public double Percentage { get; set; }
}
