using System.Collections.Generic;
using System.Threading.Tasks;
using Squirmify.Core.Entities;

namespace Squirmify.Core.Interfaces;

public interface IResultsRepository
{
	Task<IEnumerable<InstructionTestResult>> GetInstructionResultsAsync(int runId);

	Task<IEnumerable<InstructionTestResult>> GetInstructionResultsByModelAsync(int runId, int modelId);

	Task<int> SaveInstructionResultAsync(InstructionTestResult result);

	Task SaveInstructionResultsAsync(IEnumerable<InstructionTestResult> results);

	Task<IEnumerable<ReasoningTestResult>> GetReasoningResultsAsync(int runId);

	Task<IEnumerable<ReasoningTestResult>> GetReasoningResultsByModelAsync(int runId, int modelId);

	Task<int> SaveReasoningResultAsync(ReasoningTestResult result);

	Task SaveReasoningResultsAsync(IEnumerable<ReasoningTestResult> results);

	Task<IEnumerable<ConversationTestResult>> GetConversationResultsAsync(int runId);

	Task<IEnumerable<ConversationExchange>> GetConversationExchangesAsync(int resultId);

	Task<int> SaveConversationResultAsync(ConversationTestResult result, IEnumerable<ConversationExchange> exchanges);

	Task<IEnumerable<ContextWindowTestResult>> GetContextWindowResultsAsync(int runId);

	Task<IEnumerable<ContextWindowProbe>> GetContextWindowProbesAsync(int resultId);

	Task<int> SaveContextWindowResultAsync(ContextWindowTestResult result, IEnumerable<ContextWindowProbe> probes);

	Task<IEnumerable<GenerationResult>> GetGenerationResultsAsync(int runId);

	Task<IEnumerable<GenerationResult>> GetGenerationResultsByModelAsync(int runId, int modelId);

	Task<IEnumerable<GenerationResult>> GetHighQualityResultsAsync(int runId);

	Task<int> SaveGenerationResultAsync(GenerationResult result);

	Task SaveGenerationResultsAsync(IEnumerable<GenerationResult> results);

	Task UpdateGenerationResultAsync(GenerationResult result);

	Task<IEnumerable<GenerationRating>> GetRatingsAsync(int resultId);

	Task<int> SaveRatingAsync(GenerationRating rating);

	Task SaveRatingsAsync(IEnumerable<GenerationRating> ratings);

	Task UpdateGenerationScoresAsync(int runId);

	Task<IEnumerable<McpToolTestResult>> GetMcpToolResultsAsync(int runId);

	Task<IEnumerable<McpToolTestResult>> GetMcpToolResultsByModelAsync(int runId, int modelId);

	Task<int> SaveMcpToolResultAsync(McpToolTestResult result);
}
