using System;

namespace Squirmify.Core.Entities;

public class InstructionTest
{
	public int Id { get; set; }

	public string Category { get; set; } = string.Empty;

	public string Prompt { get; set; } = string.Empty;

	public string ExpectedResult { get; set; } = string.Empty;

	public string ValidationType { get; set; } = "exact";

	public bool StrictOrder { get; set; }

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public string? ExcludePatterns { get; set; }

	public string? AllowedValues { get; set; }

	public int? ExpectedCount { get; set; }
}
