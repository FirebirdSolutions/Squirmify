using System;

namespace Squirmify.Core.Entities;

public class BenchmarkRun
{
	public int Id { get; set; }

	public string? Name { get; set; }

	public int ConfigId { get; set; }

	public int ProviderId { get; set; }

	public int? ModelGroupId { get; set; }

	public string Status { get; set; } = "pending";

	public DateTime? StartedAt { get; set; }

	public DateTime? CompletedAt { get; set; }

	public int TotalModels { get; set; }

	public int TotalTests { get; set; }

	public int CompletedTests { get; set; }

	public int ErrorCount { get; set; }

	public int? BaseJudgeModelId { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
