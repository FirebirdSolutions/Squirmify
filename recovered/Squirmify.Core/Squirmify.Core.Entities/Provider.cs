using System;

namespace Squirmify.Core.Entities;

public class Provider
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public string BaseUrl { get; set; } = string.Empty;

	public string? AuthToken { get; set; }

	public bool UseAuth { get; set; }

	public int TimeoutMinutes { get; set; } = 10;

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? UpdatedAt { get; set; }
}
