using System;

namespace Squirmify.Core.Entities;

public class ContextWindowTestResult
{
	public int Id { get; set; }

	public int RunId { get; set; }

	public int ModelId { get; set; }

	public int TestId { get; set; }

	public int? MaxReliableTokens { get; set; }

	public double? CheckpointAccuracy { get; set; }

	public string? DegradationPattern { get; set; }

	public string? AutopsyText { get; set; }

	public double? TotalMs { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
