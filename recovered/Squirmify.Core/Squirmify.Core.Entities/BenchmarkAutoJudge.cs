namespace Squirmify.Core.Entities;

public class BenchmarkAutoJudge
{
	public int Id { get; set; }

	public int RunId { get; set; }

	public int ModelId { get; set; }

	public string? SelectionReason { get; set; }
}
