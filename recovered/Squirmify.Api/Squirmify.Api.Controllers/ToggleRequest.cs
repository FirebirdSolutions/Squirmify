namespace Squirmify.Api.Controllers;

public record ToggleRequest
{
	public bool Value { get; init; }
}
