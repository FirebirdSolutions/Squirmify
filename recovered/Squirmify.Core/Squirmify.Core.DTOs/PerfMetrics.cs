namespace Squirmify.Core.DTOs;

public class PerfMetrics
{
	public double? FirstTokenMs { get; set; }

	public double TotalMs { get; set; }

	public double? TokensPerSec { get; set; }

	public int? PromptTokens { get; set; }

	public int? CompletionTokens { get; set; }
}
