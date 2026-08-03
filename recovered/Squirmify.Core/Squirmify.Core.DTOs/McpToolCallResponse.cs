using System.Text.Json.Serialization;

namespace Squirmify.Core.DTOs;

public class McpToolCallResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; set; }

	[JsonPropertyName("error")]
	public string? Error { get; set; }

	[JsonPropertyName("data")]
	public object? Data { get; set; }
}
