using System;
using System.Collections.Generic;

namespace Squirmify.Core.DTOs;

public class RunAnalysis
{
	public int RunId { get; set; }

	public string? RunName { get; set; }

	public DateTime? StartedAt { get; set; }

	public DateTime? CompletedAt { get; set; }

	public TimeSpan Duration { get; set; }

	public OverallSummary Overall { get; set; } = new OverallSummary();

	public List<ModelSummary> ModelSummaries { get; set; } = new List<ModelSummary>();

	public InstructionAnalysis InstructionAnalysis { get; set; } = new InstructionAnalysis();

	public ReasoningAnalysis ReasoningAnalysis { get; set; } = new ReasoningAnalysis();

	public ConversationAnalysis ConversationAnalysis { get; set; } = new ConversationAnalysis();

	public ContextWindowAnalysis ContextWindowAnalysis { get; set; } = new ContextWindowAnalysis();

	public PerformanceAnalysis Performance { get; set; } = new PerformanceAnalysis();
}
