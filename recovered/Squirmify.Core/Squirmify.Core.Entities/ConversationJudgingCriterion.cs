namespace Squirmify.Core.Entities;

public class ConversationJudgingCriterion
{
	public int Id { get; set; }

	public int TestId { get; set; }

	public string Criterion { get; set; } = string.Empty;

	public int SortOrder { get; set; }
}
