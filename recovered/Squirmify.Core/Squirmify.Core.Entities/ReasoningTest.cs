using System;

namespace Squirmify.Core.Entities;

public class ReasoningTest
{
	public int Id { get; set; }

	public string Category { get; set; } = string.Empty;

	public string? Description { get; set; }

	public string Prompt { get; set; } = string.Empty;

	public string CorrectAnswer { get; set; } = string.Empty;

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
