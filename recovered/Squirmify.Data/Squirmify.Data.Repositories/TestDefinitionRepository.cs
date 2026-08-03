using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;
using Squirmify.Data.Database;

namespace Squirmify.Data.Repositories;

public class TestDefinitionRepository : ITestDefinitionRepository
{
	private readonly IDbConnectionFactory _connectionFactory;

	public TestDefinitionRepository(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public async Task<IEnumerable<InstructionTest>> GetInstructionTestsAsync(bool activeOnly = true)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		string sql = (activeOnly ? "SELECT * FROM InstructionTests WHERE IsActive = 1 ORDER BY Category, Id" : "SELECT * FROM InstructionTests ORDER BY Category, Id");
		return await connection.QueryAsync<InstructionTest>(sql);
	}

	public async Task<IEnumerable<InstructionTest>> GetInstructionTestsByCategoryAsync(string category)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<InstructionTest>("SELECT * FROM InstructionTests WHERE Category = @Category AND IsActive = 1 ORDER BY Id", new
		{
			Category = category
		});
	}

	public async Task<InstructionTest?> GetInstructionTestByIdAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<InstructionTest>("SELECT * FROM InstructionTests WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<int> CreateInstructionTestAsync(InstructionTest test)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT INTO InstructionTests (Category, Prompt, ExpectedResult, ValidationType, StrictOrder, IsActive, CreatedAt)\r\nVALUES (@Category, @Prompt, @ExpectedResult, @ValidationType, @StrictOrder, @IsActive, @CreatedAt);\r\nSELECT last_insert_rowid();", test);
	}

	public async Task UpdateInstructionTestAsync(InstructionTest test)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE InstructionTests SET\r\n    Category = @Category, Prompt = @Prompt, ExpectedResult = @ExpectedResult,\r\n    ValidationType = @ValidationType, StrictOrder = @StrictOrder, IsActive = @IsActive\r\nWHERE Id = @Id", test);
	}

	public async Task DeleteInstructionTestAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM InstructionTests WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<IEnumerable<ReasoningTest>> GetReasoningTestsAsync(bool activeOnly = true)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		string sql = (activeOnly ? "SELECT * FROM ReasoningTests WHERE IsActive = 1 ORDER BY Category, Id" : "SELECT * FROM ReasoningTests ORDER BY Category, Id");
		return await connection.QueryAsync<ReasoningTest>(sql);
	}

	public async Task<IEnumerable<ReasoningTest>> GetReasoningTestsByCategoryAsync(string category)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<ReasoningTest>("SELECT * FROM ReasoningTests WHERE Category = @Category AND IsActive = 1 ORDER BY Id", new
		{
			Category = category
		});
	}

	public async Task<ReasoningTest?> GetReasoningTestByIdAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<ReasoningTest>("SELECT * FROM ReasoningTests WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<int> CreateReasoningTestAsync(ReasoningTest test)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT INTO ReasoningTests (Category, Description, Prompt, CorrectAnswer, IsActive, CreatedAt)\r\nVALUES (@Category, @Description, @Prompt, @CorrectAnswer, @IsActive, @CreatedAt);\r\nSELECT last_insert_rowid();", test);
	}

	public async Task UpdateReasoningTestAsync(ReasoningTest test)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE ReasoningTests SET\r\n    Category = @Category, Description = @Description, Prompt = @Prompt,\r\n    CorrectAnswer = @CorrectAnswer, IsActive = @IsActive\r\nWHERE Id = @Id", test);
	}

	public async Task DeleteReasoningTestAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM ReasoningTests WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<IEnumerable<ConversationTest>> GetConversationTestsAsync(bool activeOnly = true)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		string sql = (activeOnly ? "SELECT * FROM ConversationTests WHERE IsActive = 1 ORDER BY Category, Id" : "SELECT * FROM ConversationTests ORDER BY Category, Id");
		return await connection.QueryAsync<ConversationTest>(sql);
	}

	public async Task<ConversationTest?> GetConversationTestByIdAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<ConversationTest>("SELECT * FROM ConversationTests WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<IEnumerable<ConversationTurn>> GetConversationTurnsAsync(int testId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<ConversationTurn>("SELECT * FROM ConversationTurns WHERE TestId = @TestId ORDER BY TurnNumber", new
		{
			TestId = testId
		});
	}

	public async Task<IEnumerable<ConversationJudgingCriterion>> GetConversationCriteriaAsync(int testId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<ConversationJudgingCriterion>("SELECT * FROM ConversationJudgingCriteria WHERE TestId = @TestId ORDER BY SortOrder", new
		{
			TestId = testId
		});
	}

	public async Task<int> CreateConversationTestAsync(ConversationTest test, IEnumerable<ConversationTurn> turns, IEnumerable<ConversationJudgingCriterion> criteria)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		int id = await connection.ExecuteScalarAsync<int>("INSERT INTO ConversationTests (Category, Description, SystemPrompt, IsActive, CreatedAt)\r\nVALUES (@Category, @Description, @SystemPrompt, @IsActive, @CreatedAt);\r\nSELECT last_insert_rowid();", test, transaction);
		foreach (ConversationTurn turn in turns)
		{
			turn.TestId = id;
			await connection.ExecuteAsync("INSERT INTO ConversationTurns (TestId, TurnNumber, UserMessage, ExpectedTheme)\r\nVALUES (@TestId, @TurnNumber, @UserMessage, @ExpectedTheme)", turn, transaction);
		}
		foreach (ConversationJudgingCriterion criterion in criteria)
		{
			criterion.TestId = id;
			await connection.ExecuteAsync("INSERT INTO ConversationJudgingCriteria (TestId, Criterion, SortOrder)\r\nVALUES (@TestId, @Criterion, @SortOrder)", criterion, transaction);
		}
		transaction.Commit();
		return id;
	}

	public async Task UpdateConversationTestAsync(ConversationTest test, IEnumerable<ConversationTurn> turns, IEnumerable<ConversationJudgingCriterion> criteria)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		await connection.ExecuteAsync("UPDATE ConversationTests SET\r\n    Category = @Category, Description = @Description, SystemPrompt = @SystemPrompt, IsActive = @IsActive\r\nWHERE Id = @Id", test, transaction);
		await connection.ExecuteAsync("DELETE FROM ConversationTurns WHERE TestId = @Id", new { test.Id }, transaction);
		await connection.ExecuteAsync("DELETE FROM ConversationJudgingCriteria WHERE TestId = @Id", new { test.Id }, transaction);
		foreach (ConversationTurn turn in turns)
		{
			turn.TestId = test.Id;
			await connection.ExecuteAsync("INSERT INTO ConversationTurns (TestId, TurnNumber, UserMessage, ExpectedTheme)\r\nVALUES (@TestId, @TurnNumber, @UserMessage, @ExpectedTheme)", turn, transaction);
		}
		foreach (ConversationJudgingCriterion criterion in criteria)
		{
			criterion.TestId = test.Id;
			await connection.ExecuteAsync("INSERT INTO ConversationJudgingCriteria (TestId, Criterion, SortOrder)\r\nVALUES (@TestId, @Criterion, @SortOrder)", criterion, transaction);
		}
		transaction.Commit();
	}

	public async Task DeleteConversationTestAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		await connection.ExecuteAsync("DELETE FROM ConversationTurns WHERE TestId = @Id", new
		{
			Id = id
		}, transaction);
		await connection.ExecuteAsync("DELETE FROM ConversationJudgingCriteria WHERE TestId = @Id", new
		{
			Id = id
		}, transaction);
		await connection.ExecuteAsync("DELETE FROM ConversationTests WHERE Id = @Id", new
		{
			Id = id
		}, transaction);
		transaction.Commit();
	}

	public async Task<IEnumerable<ContextWindowTest>> GetContextWindowTestsAsync(bool activeOnly = true)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		string sql = (activeOnly ? "SELECT * FROM ContextWindowTests WHERE IsActive = 1 ORDER BY Name" : "SELECT * FROM ContextWindowTests ORDER BY Name");
		return await connection.QueryAsync<ContextWindowTest>(sql);
	}

	public async Task<ContextWindowTest?> GetContextWindowTestByIdAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<ContextWindowTest>("SELECT * FROM ContextWindowTests WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<IEnumerable<ContextWindowCheckpoint>> GetContextWindowCheckpointsAsync(int testId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<ContextWindowCheckpoint>("SELECT * FROM ContextWindowCheckpoints WHERE TestId = @TestId ORDER BY SortOrder", new
		{
			TestId = testId
		});
	}

	public async Task<int> CreateContextWindowTestAsync(ContextWindowTest test, IEnumerable<ContextWindowCheckpoint> checkpoints)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		int id = await connection.ExecuteScalarAsync<int>("INSERT INTO ContextWindowTests (Name, Description, FillerType, BaseTargetTokens, BaseCheckpointCount, BuriedInstruction, IsActive, CreatedAt)\r\nVALUES (@Name, @Description, @FillerType, @BaseTargetTokens, @BaseCheckpointCount, @BuriedInstruction, @IsActive, @CreatedAt);\r\nSELECT last_insert_rowid();", test, transaction);
		foreach (ContextWindowCheckpoint checkpoint in checkpoints)
		{
			checkpoint.TestId = id;
			await connection.ExecuteAsync("INSERT INTO ContextWindowCheckpoints (TestId, TargetTokenPosition, SecretWord, CarrierSentence, SortOrder)\r\nVALUES (@TestId, @TargetTokenPosition, @SecretWord, @CarrierSentence, @SortOrder)", checkpoint, transaction);
		}
		transaction.Commit();
		return id;
	}

	public async Task UpdateContextWindowTestAsync(ContextWindowTest test, IEnumerable<ContextWindowCheckpoint> checkpoints)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		await connection.ExecuteAsync("UPDATE ContextWindowTests SET\r\n    Name = @Name, Description = @Description, FillerType = @FillerType,\r\n    BaseTargetTokens = @BaseTargetTokens, BaseCheckpointCount = @BaseCheckpointCount,\r\n    BuriedInstruction = @BuriedInstruction, IsActive = @IsActive\r\nWHERE Id = @Id", test, transaction);
		await connection.ExecuteAsync("DELETE FROM ContextWindowCheckpoints WHERE TestId = @Id", new { test.Id }, transaction);
		foreach (ContextWindowCheckpoint checkpoint in checkpoints)
		{
			checkpoint.TestId = test.Id;
			await connection.ExecuteAsync("INSERT INTO ContextWindowCheckpoints (TestId, TargetTokenPosition, SecretWord, CarrierSentence, SortOrder)\r\nVALUES (@TestId, @TargetTokenPosition, @SecretWord, @CarrierSentence, @SortOrder)", checkpoint, transaction);
		}
		transaction.Commit();
	}

	public async Task<IEnumerable<McpToolTest>> GetMcpToolTestsAsync(bool activeOnly = true)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		string sql = (activeOnly ? "SELECT * FROM McpToolTests WHERE IsActive = 1 ORDER BY Category, Id" : "SELECT * FROM McpToolTests ORDER BY Category, Id");
		return await connection.QueryAsync<McpToolTest>(sql);
	}

	public async Task<IEnumerable<McpToolTest>> GetMcpToolTestsByCategoryAsync(string category)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<McpToolTest>("SELECT * FROM McpToolTests WHERE Category = @Category AND IsActive = 1 ORDER BY Id", new
		{
			Category = category
		});
	}

	public async Task<McpToolTest?> GetMcpToolTestByIdAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<McpToolTest>("SELECT * FROM McpToolTests WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<int> CreateMcpToolTestAsync(McpToolTest test)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT INTO McpToolTests (Category, Description, ToolName, Command, ToolSchema, ScenarioPrompt,\r\n    ExpectedParams, ResponseValidationType, ExpectedResponsePatterns, ExecuteTool, IsActive, CreatedAt)\r\nVALUES (@Category, @Description, @ToolName, @Command, @ToolSchema, @ScenarioPrompt,\r\n    @ExpectedParams, @ResponseValidationType, @ExpectedResponsePatterns, @ExecuteTool, @IsActive, @CreatedAt);\r\nSELECT last_insert_rowid();", test);
	}

	public async Task UpdateMcpToolTestAsync(McpToolTest test)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE McpToolTests SET\r\n    Category = @Category, Description = @Description, ToolName = @ToolName,\r\n    Command = @Command, ToolSchema = @ToolSchema, ScenarioPrompt = @ScenarioPrompt,\r\n    ExpectedParams = @ExpectedParams, ResponseValidationType = @ResponseValidationType,\r\n    ExpectedResponsePatterns = @ExpectedResponsePatterns, ExecuteTool = @ExecuteTool, IsActive = @IsActive\r\nWHERE Id = @Id", test);
	}

	public async Task DeleteMcpToolTestAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM McpToolTests WHERE Id = @Id", new
		{
			Id = id
		});
	}
}
