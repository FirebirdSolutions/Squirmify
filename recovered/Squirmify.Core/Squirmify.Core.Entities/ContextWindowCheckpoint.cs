namespace Squirmify.Core.Entities;

public class ContextWindowCheckpoint
{
	public int Id { get; set; }

	public int TestId { get; set; }

	public int TargetTokenPosition { get; set; }

	public double? RelativePosition { get; set; }

	public string SecretWord { get; set; } = string.Empty;

	public string? CarrierSentence { get; set; }

	public int SortOrder { get; set; }
}
