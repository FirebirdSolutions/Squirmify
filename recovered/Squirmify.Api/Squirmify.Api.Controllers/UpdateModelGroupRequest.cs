using System.Collections.Generic;

namespace Squirmify.Api.Controllers;

public record UpdateModelGroupRequest
{
	public string? Name { get; init; }

	public string? Description { get; init; }

	public List<int>? ModelIds { get; init; }
}
