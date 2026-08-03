using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Squirmify.Core.DTOs;

public class ChatRequest
{
	[JsonPropertyName("model")]
	public string Model { get; set; } = string.Empty;

	[JsonPropertyName("messages")]
	public List<Message> Messages { get; set; } = new List<Message>();

	[JsonPropertyName("temperature")]
	public double Temperature { get; set; }

	[JsonPropertyName("top_p")]
	public double TopP { get; set; }

	[JsonPropertyName("max_tokens")]
	public int MaxTokens { get; set; }

	[JsonPropertyName("stream")]
	public bool Stream { get; set; }
}
