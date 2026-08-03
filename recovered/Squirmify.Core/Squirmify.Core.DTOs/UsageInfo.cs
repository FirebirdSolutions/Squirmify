using System.Text.Json.Serialization;

namespace Squirmify.Core.DTOs;

public class UsageInfo
{
	[JsonPropertyName("prompt_tokens")]
	public int PromptTokens { get; set; }

	[JsonPropertyName("completion_tokens")]
	public int CompletionTokens { get; set; }
}
