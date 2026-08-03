using System.Text.Json;

namespace Squirmify.Services.Evaluation;

public class ToolCallParseResult
{
	public bool Success { get; set; }

	public JsonElement ToolCall { get; set; }

	public string? RawJson { get; set; }

	public string? Error { get; set; }
}
