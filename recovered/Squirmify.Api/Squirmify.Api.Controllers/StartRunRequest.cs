using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Squirmify.Api.Controllers;

public record StartRunRequest
{
	public required int ConfigId { get; init; }

	public required int ProviderId { get; init; }

	public int? ModelGroupId { get; init; }

	public int? JudgeModelId { get; init; }

	public string? Name { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	protected StartRunRequest(StartRunRequest original)
	{
		ConfigId = original.ConfigId;
		ProviderId = original.ProviderId;
		ModelGroupId = original.ModelGroupId;
		JudgeModelId = original.JudgeModelId;
		Name = original.Name;
	}
}
