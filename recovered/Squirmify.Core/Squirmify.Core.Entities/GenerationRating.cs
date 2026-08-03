using System;

namespace Squirmify.Core.Entities;

public class GenerationRating
{
	public int Id { get; set; }

	public int ResultId { get; set; }

	public int JudgeModelId { get; set; }

	public double Score { get; set; }

	public string? Reasoning { get; set; }

	public bool IsBaseJudge { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
