using System;

namespace Squirmify.Core.Entities;

public class Seed
{
	public int Id { get; set; }

	public string Category { get; set; } = string.Empty;

	public string Instruction { get; set; } = string.Empty;

	public double? Temperature { get; set; }

	public double? TopP { get; set; }

	public int? MaxTokens { get; set; }

	public bool IsAugmented { get; set; }

	public int? SourceSeedId { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
