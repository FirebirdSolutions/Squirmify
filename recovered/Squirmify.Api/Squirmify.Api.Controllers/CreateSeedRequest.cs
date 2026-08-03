using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Squirmify.Api.Controllers;

public record CreateSeedRequest
{
	public required string Category { get; init; }

	public required string Instruction { get; init; }

	public double? Temperature { get; init; }

	public double? TopP { get; init; }

	public int? MaxTokens { get; init; }

	public List<string>? Tags { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	protected CreateSeedRequest(CreateSeedRequest original)
	{
		Category = original.Category;
		Instruction = original.Instruction;
		Temperature = original.Temperature;
		TopP = original.TopP;
		MaxTokens = original.MaxTokens;
		Tags = original.Tags;
	}
}
