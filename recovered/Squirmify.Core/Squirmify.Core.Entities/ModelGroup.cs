using System;

namespace Squirmify.Core.Entities;

public class ModelGroup
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? UpdatedAt { get; set; }
}
