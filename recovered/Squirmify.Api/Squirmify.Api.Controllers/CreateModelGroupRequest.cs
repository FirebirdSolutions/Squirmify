using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Squirmify.Api.Controllers;

public record CreateModelGroupRequest
{
	public required string Name { get; init; }

	public string? Description { get; init; }

	public List<int>? ModelIds { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	protected CreateModelGroupRequest(CreateModelGroupRequest original)
	{
		Name = original.Name;
		Description = original.Description;
		ModelIds = original.ModelIds;
	}
}
