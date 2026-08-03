using System.Collections.Generic;

namespace Squirmify.Core.DTOs;

public class PerformanceAnalysis
{
	public double AvgTokensPerSec { get; set; }

	public double MaxTokensPerSec { get; set; }

	public double MinTokensPerSec { get; set; }

	public double InstructionAvgTokensPerSec { get; set; }

	public double GenerationAvgTokensPerSec { get; set; }

	public double AvgLatencyMs { get; set; }

	public double P50LatencyMs { get; set; }

	public double P95LatencyMs { get; set; }

	public double P99LatencyMs { get; set; }

	public long TotalPromptTokens { get; set; }

	public long TotalCompletionTokens { get; set; }

	public List<ModelPerformance> ByModel { get; set; } = new List<ModelPerformance>();
}
