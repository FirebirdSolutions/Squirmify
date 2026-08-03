using System.Text.Json.Serialization;

namespace Squirmify.Core.DTOs;

public class McpToolCallRequest
{
	[JsonPropertyName("cmd")]
	public string Command { get; set; } = string.Empty;

	[JsonPropertyName("params")]
	public object? Parameters { get; set; }

	[JsonPropertyName("detail")]
	public string? Detail { get; set; }
}
