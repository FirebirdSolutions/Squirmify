using System.Text.Json.Serialization;

namespace Squirmify.Core.DTOs;

public class Message
{
	[JsonPropertyName("role")]
	public string Role { get; set; } = string.Empty;

	[JsonPropertyName("content")]
	public string Content { get; set; } = string.Empty;

	[JsonPropertyName("reasoning_content")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? ReasoningContent { get; set; }

	public Message()
	{
	}

	public Message(string role, string content)
	{
		Role = role;
		Content = content;
	}
}
