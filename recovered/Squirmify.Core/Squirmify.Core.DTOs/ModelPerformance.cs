namespace Squirmify.Core.DTOs;

public class ModelPerformance
{
	public int ModelId { get; set; }

	public string ModelName { get; set; } = string.Empty;

	public double AvgTokensPerSec { get; set; }

	public double InstructionAvgTokensPerSec { get; set; }

	public double GenerationAvgTokensPerSec { get; set; }

	public double AvgLatencyMs { get; set; }

	public int TotalRequests { get; set; }
}
