using System.Collections.Generic;

namespace Squirmify.Core.DTOs;

public class RunProgress
{
	public int RunId { get; set; }

	public string Stage { get; set; } = string.Empty;

	public int CurrentModelIndex { get; set; }

	public int TotalModels { get; set; }

	public int CurrentTestIndex { get; set; }

	public int TotalTests { get; set; }

	public string? CurrentModel { get; set; }

	public string? CurrentTest { get; set; }

	public double PercentComplete { get; set; }

	public List<string> RecentEvents { get; set; } = new List<string>();
}
