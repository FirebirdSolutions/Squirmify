using System;

namespace Squirmify.Core.Entities;

public class ConversationTest
{
	public int Id { get; set; }

	public string Category { get; set; } = string.Empty;

	public string? Description { get; set; }

	public string? SystemPrompt { get; set; }

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
