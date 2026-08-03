using System;

namespace Squirmify.Core.Entities;

public class Model
{
	public int Id { get; set; }

	public int ProviderId { get; set; }

	public string Identifier { get; set; } = string.Empty;

	public string? DisplayName { get; set; }

	public bool IsDisabled { get; set; } = false;

	public bool IsAvailable { get; set; } = true;

	public bool IsDeleted { get; set; } = false;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public bool CanTest => !IsDisabled && IsAvailable && !IsDeleted;
}
