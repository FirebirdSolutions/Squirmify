using System;

namespace Squirmify.Core.Entities;

public class RunLog
{
	public int Id { get; set; }

	public int RunId { get; set; }

	public string Level { get; set; } = "info";

	public string Message { get; set; } = "";

	public string? ModelName { get; set; }

	public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
