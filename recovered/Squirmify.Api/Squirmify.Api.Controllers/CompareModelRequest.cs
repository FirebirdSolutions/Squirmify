using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Squirmify.Api.Controllers;

public record CompareModelRequest
{
	public required int ModelId { get; init; }

	public required List<int> RunIds { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	protected CompareModelRequest(CompareModelRequest original)
	{
		ModelId = original.ModelId;
		RunIds = original.RunIds;
	}
}
