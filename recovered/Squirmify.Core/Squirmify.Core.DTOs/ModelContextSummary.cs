namespace Squirmify.Core.DTOs;

public class ModelContextSummary
{
	public int ModelId { get; set; }

	public string ModelName { get; set; } = string.Empty;

	public int MaxReliableTokens { get; set; }

	public double CheckpointAccuracy { get; set; }

	public string DegradationPattern { get; set; } = string.Empty;
}
