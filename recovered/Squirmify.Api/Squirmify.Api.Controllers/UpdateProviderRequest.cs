namespace Squirmify.Api.Controllers;

public record UpdateProviderRequest
{
	public string? Name { get; init; }

	public string? BaseUrl { get; init; }

	public string? AuthToken { get; init; }

	public bool? UseAuth { get; init; }

	public int? TimeoutMinutes { get; init; }

	public bool? IsActive { get; init; }
}
