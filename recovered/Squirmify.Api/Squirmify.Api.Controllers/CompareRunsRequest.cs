using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Squirmify.Api.Controllers;

public record CompareRunsRequest
{
	public required List<int> RunIds { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	protected CompareRunsRequest(CompareRunsRequest original)
	{
		RunIds = original.RunIds;
	}
}
