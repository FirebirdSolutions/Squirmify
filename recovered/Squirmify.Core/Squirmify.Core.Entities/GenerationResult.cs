using System;

namespace Squirmify.Core.Entities;

public class GenerationResult
{
	public int Id { get; set; }

	public int RunId { get; set; }

	public int ModelId { get; set; }

	public int SeedId { get; set; }

	public string Category { get; set; } = string.Empty;

	public string Response { get; set; } = string.Empty;

	public double? Temperature { get; set; }

	public double? TopP { get; set; }

	public int? MaxTokens { get; set; }

	public double? FirstTokenMs { get; set; }

	public double TotalMs { get; set; }

	public double? TokensPerSec { get; set; }

	public int? PromptTokens { get; set; }

	public int? CompletionTokens { get; set; }

	public double? AvgScore { get; set; }

	public bool IsHighQuality { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
