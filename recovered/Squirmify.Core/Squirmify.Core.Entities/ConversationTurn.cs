namespace Squirmify.Core.Entities;

public class ConversationTurn
{
	public int Id { get; set; }

	public int TestId { get; set; }

	public int TurnNumber { get; set; }

	public string UserMessage { get; set; } = string.Empty;

	public string? ExpectedTheme { get; set; }
}
