using System;

namespace Squirmify.Core.Entities;

public class ModelGroupMember
{
	public int Id { get; set; }

	public int GroupId { get; set; }

	public int ModelId { get; set; }

	public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
