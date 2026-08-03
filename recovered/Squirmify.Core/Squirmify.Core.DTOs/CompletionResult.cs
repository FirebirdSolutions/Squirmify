namespace Squirmify.Core.DTOs;

public class CompletionResult
{
	public string Response { get; set; } = string.Empty;

	public PerfMetrics Perf { get; set; } = new PerfMetrics();
}
