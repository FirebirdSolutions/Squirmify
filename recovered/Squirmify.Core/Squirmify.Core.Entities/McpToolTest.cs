using System;

namespace Squirmify.Core.Entities;

public class McpToolTest
{
	public int Id { get; set; }

	public string Category { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public string ToolName { get; set; } = string.Empty;

	public string Command { get; set; } = string.Empty;

	public string? ToolSchema { get; set; }

	public string ScenarioPrompt { get; set; } = string.Empty;

	public string? ExpectedParams { get; set; }

	public string ResponseValidationType { get; set; } = "success";

	public string? ExpectedResponsePatterns { get; set; }

	public bool ExecuteTool { get; set; } = true;

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
