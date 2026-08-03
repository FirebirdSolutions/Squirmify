namespace Squirmify.Core.Entities;

public class ContextWindowProbe
{
	public int Id { get; set; }

	public int ResultId { get; set; }

	public int? CheckpointId { get; set; }

	public int TokenPosition { get; set; }

	public bool Found { get; set; }

	public bool Hallucinated { get; set; }

	public string? Response { get; set; }

	public double? TotalMs { get; set; }
}
