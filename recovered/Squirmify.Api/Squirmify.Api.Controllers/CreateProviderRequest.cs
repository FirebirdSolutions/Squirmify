using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Squirmify.Api.Controllers;

public record CreateProviderRequest
{
	public required string Name { get; init; }

	public required string BaseUrl { get; init; }

	public string? AuthToken { get; init; }

	public bool UseAuth { get; init; }

	public int TimeoutMinutes { get; init; } = 10;

	[CompilerGenerated]
	[SetsRequiredMembers]
	protected CreateProviderRequest(CreateProviderRequest original)
	{
		Name = original.Name;
		BaseUrl = original.BaseUrl;
		AuthToken = original.AuthToken;
		UseAuth = original.UseAuth;
		TimeoutMinutes = original.TimeoutMinutes;
	}
}
