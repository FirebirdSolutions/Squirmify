using System.Collections.Generic;

namespace Squirmify.Api.Controllers;

public record UpdateSeedRequest
{
	public string? Category { get; init; }

	public string? Instruction { get; init; }

	public double? Temperature { get; init; }

	public double? TopP { get; init; }

	public int? MaxTokens { get; init; }

	public List<string>? Tags { get; init; }
}
