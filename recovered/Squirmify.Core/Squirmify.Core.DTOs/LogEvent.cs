using System;

namespace Squirmify.Core.DTOs;

public class LogEvent
{
	public int RunId { get; set; }

	public DateTime Timestamp { get; set; } = DateTime.UtcNow;

	public string Level { get; set; } = "info";

	public string Message { get; set; } = string.Empty;

	public string? ModelName { get; set; }
}
