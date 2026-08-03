using System.Collections.Generic;
using System.Threading.Tasks;
using Squirmify.Core.Entities;

namespace Squirmify.Core.Interfaces;

public interface ITestDefinitionRepository
{
	Task<IEnumerable<InstructionTest>> GetInstructionTestsAsync(bool activeOnly = true);

	Task<IEnumerable<InstructionTest>> GetInstructionTestsByCategoryAsync(string category);

	Task<InstructionTest?> GetInstructionTestByIdAsync(int id);

	Task<int> CreateInstructionTestAsync(InstructionTest test);

	Task UpdateInstructionTestAsync(InstructionTest test);

	Task DeleteInstructionTestAsync(int id);

	Task<IEnumerable<ReasoningTest>> GetReasoningTestsAsync(bool activeOnly = true);

	Task<IEnumerable<ReasoningTest>> GetReasoningTestsByCategoryAsync(string category);

	Task<ReasoningTest?> GetReasoningTestByIdAsync(int id);

	Task<int> CreateReasoningTestAsync(ReasoningTest test);

	Task UpdateReasoningTestAsync(ReasoningTest test);

	Task DeleteReasoningTestAsync(int id);

	Task<IEnumerable<ConversationTest>> GetConversationTestsAsync(bool activeOnly = true);

	Task<ConversationTest?> GetConversationTestByIdAsync(int id);

	Task<IEnumerable<ConversationTurn>> GetConversationTurnsAsync(int testId);

	Task<IEnumerable<ConversationJudgingCriterion>> GetConversationCriteriaAsync(int testId);

	Task<int> CreateConversationTestAsync(ConversationTest test, IEnumerable<ConversationTurn> turns, IEnumerable<ConversationJudgingCriterion> criteria);

	Task UpdateConversationTestAsync(ConversationTest test, IEnumerable<ConversationTurn> turns, IEnumerable<ConversationJudgingCriterion> criteria);

	Task DeleteConversationTestAsync(int id);

	Task<IEnumerable<ContextWindowTest>> GetContextWindowTestsAsync(bool activeOnly = true);

	Task<ContextWindowTest?> GetContextWindowTestByIdAsync(int id);

	Task<IEnumerable<ContextWindowCheckpoint>> GetContextWindowCheckpointsAsync(int testId);

	Task<int> CreateContextWindowTestAsync(ContextWindowTest test, IEnumerable<ContextWindowCheckpoint> checkpoints);

	Task UpdateContextWindowTestAsync(ContextWindowTest test, IEnumerable<ContextWindowCheckpoint> checkpoints);

	Task<IEnumerable<McpToolTest>> GetMcpToolTestsAsync(bool activeOnly = true);

	Task<IEnumerable<McpToolTest>> GetMcpToolTestsByCategoryAsync(string category);

	Task<McpToolTest?> GetMcpToolTestByIdAsync(int id);

	Task<int> CreateMcpToolTestAsync(McpToolTest test);

	Task UpdateMcpToolTestAsync(McpToolTest test);

	Task DeleteMcpToolTestAsync(int id);
}
