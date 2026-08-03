namespace Squirmify.Core.DTOs;

public class McpExecutionResult
{
	public bool Success { get; set; }

	public string? Response { get; set; }

	public string? Error { get; set; }

	public double ExecutionMs { get; set; }
}
