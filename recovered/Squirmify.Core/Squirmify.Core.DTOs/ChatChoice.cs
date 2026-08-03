using System.Text.Json.Serialization;

namespace Squirmify.Core.DTOs;

public class ChatChoice
{
	[JsonPropertyName("message")]
	public Message Message { get; set; } = new Message();
}
