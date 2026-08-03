using System.Text.Json.Serialization;

namespace Squirmify.Core.DTOs;

public class ModelInfo
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;
}
