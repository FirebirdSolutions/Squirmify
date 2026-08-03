using System.Text.Json.Serialization;

namespace Squirmify.Core.DTOs;

public class ChatResponse
{
	[JsonPropertyName("choices")]
	public ChatChoice[]? Choices { get; set; }

	[JsonPropertyName("usage")]
	public UsageInfo? Usage { get; set; }
}
