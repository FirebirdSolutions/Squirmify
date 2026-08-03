namespace Squirmify.Core.Entities;

public class ConversationExchange
{
	public int Id { get; set; }

	public int ResultId { get; set; }

	public int TurnNumber { get; set; }

	public string UserMessage { get; set; } = string.Empty;

	public string ModelResponse { get; set; } = string.Empty;

	public double? FirstTokenMs { get; set; }

	public double? TotalMs { get; set; }

	public double? TokensPerSec { get; set; }

	public int? PromptTokens { get; set; }

	public int? CompletionTokens { get; set; }
}
