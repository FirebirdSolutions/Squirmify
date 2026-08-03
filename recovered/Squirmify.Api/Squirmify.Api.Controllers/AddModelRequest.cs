using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Squirmify.Api.Controllers;

public record AddModelRequest
{
	public required int ModelId { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	protected AddModelRequest(AddModelRequest original)
	{
		ModelId = original.ModelId;
	}
}
