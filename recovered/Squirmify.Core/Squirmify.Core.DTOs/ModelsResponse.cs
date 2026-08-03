using System.Text.Json.Serialization;

namespace Squirmify.Core.DTOs;

public class ModelsResponse
{
	[JsonPropertyName("data")]
	public ModelInfo[]? Data { get; set; }
}
