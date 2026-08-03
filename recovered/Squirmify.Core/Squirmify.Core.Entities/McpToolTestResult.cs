using System;

namespace Squirmify.Core.Entities;

public class McpToolTestResult
{
	public int Id { get; set; }

	public int RunId { get; set; }

	public int ModelId { get; set; }

	public int TestId { get; set; }

	public bool JsonValid { get; set; }

	public bool CorrectTool { get; set; }

	public bool CorrectCommand { get; set; }

	public bool ParamsValid { get; set; }

	public string? ModelResponse { get; set; }

	public string? ParsedToolCall { get; set; }

	public string? JsonParseError { get; set; }

	public bool? ToolExecuted { get; set; }

	public bool? ExecutionSuccess { get; set; }

	public string? ToolResponse { get; set; }

	public string? ExecutionError { get; set; }

	public bool? ResponseValidated { get; set; }

	public string? ValidationReason { get; set; }

	public bool Passed { get; set; }

	public double TotalMs { get; set; }

	public double? ExecutionMs { get; set; }

	public double? TokensPerSec { get; set; }

	public int? PromptTokens { get; set; }

	public int? CompletionTokens { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
