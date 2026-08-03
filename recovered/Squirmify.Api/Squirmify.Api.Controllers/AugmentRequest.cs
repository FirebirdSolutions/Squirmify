using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Squirmify.Api.Controllers;

public record AugmentRequest
{
	public required int ConfigId { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	protected AugmentRequest(AugmentRequest original)
	{
		ConfigId = original.ConfigId;
	}
}
