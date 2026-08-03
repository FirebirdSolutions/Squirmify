using System;

namespace Squirmify.Core.Entities;

public class ContextWindowTest
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	public string FillerType { get; set; } = "mixed";

	public int BaseTargetTokens { get; set; }

	public int BaseCheckpointCount { get; set; }

	public string? BuriedInstruction { get; set; }

	public string NeedleComplexity { get; set; } = "single";

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
