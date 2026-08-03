using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;
using Squirmify.Data.Database;

namespace Squirmify.Data.Repositories;

public class ResultsRepository : IResultsRepository
{
	private readonly IDbConnectionFactory _connectionFactory;

	public ResultsRepository(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public async Task<IEnumerable<InstructionTestResult>> GetInstructionResultsAsync(int runId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<InstructionTestResult>("SELECT * FROM InstructionTestResults WHERE RunId = @RunId ORDER BY ModelId, TestId", new
		{
			RunId = runId
		});
	}

	public async Task<IEnumerable<InstructionTestResult>> GetInstructionResultsByModelAsync(int runId, int modelId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<InstructionTestResult>("SELECT * FROM InstructionTestResults WHERE RunId = @RunId AND ModelId = @ModelId ORDER BY TestId", new
		{
			RunId = runId,
			ModelId = modelId
		});
	}

	public async Task<int> SaveInstructionResultAsync(InstructionTestResult result)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT OR REPLACE INTO InstructionTestResults (RunId, ModelId, TestId, Passed, StrictPass, LenientPass, Response, FailureReason, FirstTokenMs, TotalMs, TokensPerSec, PromptTokens, CompletionTokens, CreatedAt)\r\nVALUES (@RunId, @ModelId, @TestId, @Passed, @StrictPass, @LenientPass, @Response, @FailureReason, @FirstTokenMs, @TotalMs, @TokensPerSec, @PromptTokens, @CompletionTokens, @CreatedAt);\r\nSELECT last_insert_rowid();", result);
	}

	public async Task SaveInstructionResultsAsync(IEnumerable<InstructionTestResult> results)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		foreach (InstructionTestResult result in results)
		{
			await connection.ExecuteAsync("INSERT OR REPLACE INTO InstructionTestResults (RunId, ModelId, TestId, Passed, StrictPass, LenientPass, Response, FailureReason, FirstTokenMs, TotalMs, TokensPerSec, PromptTokens, CompletionTokens, CreatedAt)\r\nVALUES (@RunId, @ModelId, @TestId, @Passed, @StrictPass, @LenientPass, @Response, @FailureReason, @FirstTokenMs, @TotalMs, @TokensPerSec, @PromptTokens, @CompletionTokens, @CreatedAt)", result, transaction);
		}
		transaction.Commit();
	}

	public async Task<IEnumerable<ReasoningTestResult>> GetReasoningResultsAsync(int runId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<ReasoningTestResult>("SELECT * FROM ReasoningTestResults WHERE RunId = @RunId ORDER BY ModelId, TestId", new
		{
			RunId = runId
		});
	}

	public async Task<IEnumerable<ReasoningTestResult>> GetReasoningResultsByModelAsync(int runId, int modelId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<ReasoningTestResult>("SELECT * FROM ReasoningTestResults WHERE RunId = @RunId AND ModelId = @ModelId ORDER BY TestId", new
		{
			RunId = runId,
			ModelId = modelId
		});
	}

	public async Task<int> SaveReasoningResultAsync(ReasoningTestResult result)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT OR REPLACE INTO ReasoningTestResults (RunId, ModelId, TestId, Response, OverallScore, CorrectAnswerScore, LogicalStepsScore, ClarityScore, JudgeReasoning, JudgeModelId, FirstTokenMs, TotalMs, TokensPerSec, PromptTokens, CompletionTokens, CreatedAt)\r\nVALUES (@RunId, @ModelId, @TestId, @Response, @OverallScore, @CorrectAnswerScore, @LogicalStepsScore, @ClarityScore, @JudgeReasoning, @JudgeModelId, @FirstTokenMs, @TotalMs, @TokensPerSec, @PromptTokens, @CompletionTokens, @CreatedAt);\r\nSELECT last_insert_rowid();", result);
	}

	public async Task SaveReasoningResultsAsync(IEnumerable<ReasoningTestResult> results)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		foreach (ReasoningTestResult result in results)
		{
			await connection.ExecuteAsync("INSERT OR REPLACE INTO ReasoningTestResults (RunId, ModelId, TestId, Response, OverallScore, CorrectAnswerScore, LogicalStepsScore, ClarityScore, JudgeReasoning, JudgeModelId, FirstTokenMs, TotalMs, TokensPerSec, PromptTokens, CompletionTokens, CreatedAt)\r\nVALUES (@RunId, @ModelId, @TestId, @Response, @OverallScore, @CorrectAnswerScore, @LogicalStepsScore, @ClarityScore, @JudgeReasoning, @JudgeModelId, @FirstTokenMs, @TotalMs, @TokensPerSec, @PromptTokens, @CompletionTokens, @CreatedAt)", result, transaction);
		}
		transaction.Commit();
	}

	public async Task<IEnumerable<ConversationTestResult>> GetConversationResultsAsync(int runId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<ConversationTestResult>("SELECT * FROM ConversationTestResults WHERE RunId = @RunId ORDER BY ModelId, TestId", new
		{
			RunId = runId
		});
	}

	public async Task<IEnumerable<ConversationExchange>> GetConversationExchangesAsync(int resultId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<ConversationExchange>("SELECT * FROM ConversationExchanges WHERE ResultId = @ResultId ORDER BY TurnNumber", new
		{
			ResultId = resultId
		});
	}

	public async Task<int> SaveConversationResultAsync(ConversationTestResult result, IEnumerable<ConversationExchange> exchanges)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		int id = await connection.ExecuteScalarAsync<int>("INSERT INTO ConversationTestResults (RunId, ModelId, TestId, OverallScore, TopicCoherence, ConversationalTone, ContextRetention, Helpfulness, JudgeReasoning, JudgeModelId, TotalMs, TokensPerSec, PromptTokens, CompletionTokens, CreatedAt)\r\nVALUES (@RunId, @ModelId, @TestId, @OverallScore, @TopicCoherence, @ConversationalTone, @ContextRetention, @Helpfulness, @JudgeReasoning, @JudgeModelId, @TotalMs, @TokensPerSec, @PromptTokens, @CompletionTokens, @CreatedAt);\r\nSELECT last_insert_rowid();", result, transaction);
		foreach (ConversationExchange exchange in exchanges)
		{
			exchange.ResultId = id;
			await connection.ExecuteAsync("INSERT INTO ConversationExchanges (ResultId, TurnNumber, UserMessage, ModelResponse, FirstTokenMs, TotalMs, TokensPerSec, PromptTokens, CompletionTokens)\r\nVALUES (@ResultId, @TurnNumber, @UserMessage, @ModelResponse, @FirstTokenMs, @TotalMs, @TokensPerSec, @PromptTokens, @CompletionTokens)", exchange, transaction);
		}
		transaction.Commit();
		return id;
	}

	public async Task<IEnumerable<ContextWindowTestResult>> GetContextWindowResultsAsync(int runId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<ContextWindowTestResult>("SELECT * FROM ContextWindowTestResults WHERE RunId = @RunId ORDER BY ModelId, TestId", new
		{
			RunId = runId
		});
	}

	public async Task<IEnumerable<ContextWindowProbe>> GetContextWindowProbesAsync(int resultId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<ContextWindowProbe>("SELECT * FROM ContextWindowProbes WHERE ResultId = @ResultId ORDER BY TokenPosition", new
		{
			ResultId = resultId
		});
	}

	public async Task<int> SaveContextWindowResultAsync(ContextWindowTestResult result, IEnumerable<ContextWindowProbe> probes)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		int? existingId = await connection.ExecuteScalarAsync<int?>("SELECT Id FROM ContextWindowTestResults\r\nWHERE RunId = @RunId AND ModelId = @ModelId AND TestId = @TestId", result, transaction);
		if (existingId.HasValue)
		{
			await connection.ExecuteAsync("DELETE FROM ContextWindowProbes WHERE ResultId = @ResultId", new
			{
				ResultId = existingId.Value
			}, transaction);
			await connection.ExecuteAsync("DELETE FROM ContextWindowTestResults WHERE Id = @Id", new
			{
				Id = existingId.Value
			}, transaction);
		}
		await connection.ExecuteAsync("INSERT INTO ContextWindowTestResults (RunId, ModelId, TestId, MaxReliableTokens, CheckpointAccuracy, DegradationPattern, AutopsyText, TotalMs, CreatedAt)\r\nVALUES (@RunId, @ModelId, @TestId, @MaxReliableTokens, @CheckpointAccuracy, @DegradationPattern, @AutopsyText, @TotalMs, @CreatedAt)", result, transaction);
		int id = await connection.ExecuteScalarAsync<int>("SELECT last_insert_rowid()", null, transaction);
		foreach (ContextWindowProbe probe in probes)
		{
			probe.ResultId = id;
			await connection.ExecuteAsync("INSERT INTO ContextWindowProbes (ResultId, CheckpointId, TokenPosition, Found, Hallucinated, Response, TotalMs)\r\nVALUES (@ResultId, @CheckpointId, @TokenPosition, @Found, @Hallucinated, @Response, @TotalMs)", probe, transaction);
		}
		transaction.Commit();
		return id;
	}

	public async Task<IEnumerable<GenerationResult>> GetGenerationResultsAsync(int runId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<GenerationResult>("SELECT * FROM GenerationResults WHERE RunId = @RunId ORDER BY ModelId, SeedId", new
		{
			RunId = runId
		});
	}

	public async Task<IEnumerable<GenerationResult>> GetGenerationResultsByModelAsync(int runId, int modelId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<GenerationResult>("SELECT * FROM GenerationResults WHERE RunId = @RunId AND ModelId = @ModelId ORDER BY SeedId", new
		{
			RunId = runId,
			ModelId = modelId
		});
	}

	public async Task<IEnumerable<GenerationResult>> GetHighQualityResultsAsync(int runId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<GenerationResult>("SELECT * FROM GenerationResults WHERE RunId = @RunId AND IsHighQuality = 1 ORDER BY AvgScore DESC", new
		{
			RunId = runId
		});
	}

	public async Task<int> SaveGenerationResultAsync(GenerationResult result)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT OR REPLACE INTO GenerationResults (RunId, ModelId, SeedId, Category, Response, Temperature, TopP, MaxTokens, FirstTokenMs, TotalMs, TokensPerSec, PromptTokens, CompletionTokens, AvgScore, IsHighQuality, CreatedAt)\r\nVALUES (@RunId, @ModelId, @SeedId, @Category, @Response, @Temperature, @TopP, @MaxTokens, @FirstTokenMs, @TotalMs, @TokensPerSec, @PromptTokens, @CompletionTokens, @AvgScore, @IsHighQuality, @CreatedAt);\r\nSELECT last_insert_rowid();", result);
	}

	public async Task SaveGenerationResultsAsync(IEnumerable<GenerationResult> results)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		foreach (GenerationResult result in results)
		{
			await connection.ExecuteAsync("INSERT OR REPLACE INTO GenerationResults (RunId, ModelId, SeedId, Category, Response, Temperature, TopP, MaxTokens, FirstTokenMs, TotalMs, TokensPerSec, PromptTokens, CompletionTokens, AvgScore, IsHighQuality, CreatedAt)\r\nVALUES (@RunId, @ModelId, @SeedId, @Category, @Response, @Temperature, @TopP, @MaxTokens, @FirstTokenMs, @TotalMs, @TokensPerSec, @PromptTokens, @CompletionTokens, @AvgScore, @IsHighQuality, @CreatedAt)", result, transaction);
		}
		transaction.Commit();
	}

	public async Task UpdateGenerationResultAsync(GenerationResult result)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE GenerationResults SET AvgScore = @AvgScore, IsHighQuality = @IsHighQuality WHERE Id = @Id", result);
	}

	public async Task<IEnumerable<GenerationRating>> GetRatingsAsync(int resultId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<GenerationRating>("SELECT * FROM GenerationRatings WHERE ResultId = @ResultId ORDER BY CreatedAt", new
		{
			ResultId = resultId
		});
	}

	public async Task<int> SaveRatingAsync(GenerationRating rating)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT INTO GenerationRatings (ResultId, JudgeModelId, Score, Reasoning, IsBaseJudge, CreatedAt)\r\nVALUES (@ResultId, @JudgeModelId, @Score, @Reasoning, @IsBaseJudge, @CreatedAt);\r\nSELECT last_insert_rowid();", rating);
	}

	public async Task SaveRatingsAsync(IEnumerable<GenerationRating> ratings)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		foreach (GenerationRating rating in ratings)
		{
			await connection.ExecuteAsync("INSERT INTO GenerationRatings (ResultId, JudgeModelId, Score, Reasoning, IsBaseJudge, CreatedAt)\r\nVALUES (@ResultId, @JudgeModelId, @Score, @Reasoning, @IsBaseJudge, @CreatedAt)", rating, transaction);
		}
		transaction.Commit();
	}

	public async Task UpdateGenerationScoresAsync(int runId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE GenerationResults\r\nSET AvgScore = (\r\n    SELECT AVG(CAST(Score AS REAL))\r\n    FROM GenerationRatings\r\n    WHERE GenerationRatings.ResultId = GenerationResults.Id\r\n),\r\nIsHighQuality = CASE\r\n    WHEN (SELECT AVG(CAST(Score AS REAL)) FROM GenerationRatings WHERE GenerationRatings.ResultId = GenerationResults.Id) >= 7.5 THEN 1\r\n    ELSE 0\r\nEND\r\nWHERE RunId = @RunId", new
		{
			RunId = runId
		});
	}

	public async Task<IEnumerable<McpToolTestResult>> GetMcpToolResultsAsync(int runId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<McpToolTestResult>("SELECT * FROM McpToolTestResults WHERE RunId = @RunId ORDER BY ModelId, TestId", new
		{
			RunId = runId
		});
	}

	public async Task<IEnumerable<McpToolTestResult>> GetMcpToolResultsByModelAsync(int runId, int modelId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<McpToolTestResult>("SELECT * FROM McpToolTestResults WHERE RunId = @RunId AND ModelId = @ModelId ORDER BY TestId", new
		{
			RunId = runId,
			ModelId = modelId
		});
	}

	public async Task<int> SaveMcpToolResultAsync(McpToolTestResult result)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT INTO McpToolTestResults (\r\n    RunId, ModelId, TestId, JsonValid, CorrectTool, CorrectCommand, ParamsValid,\r\n    ModelResponse, ParsedToolCall, JsonParseError, ToolExecuted, ExecutionSuccess,\r\n    ToolResponse, ExecutionError, ResponseValidated, ValidationReason,\r\n    Passed, TotalMs, ExecutionMs, TokensPerSec, PromptTokens, CompletionTokens, CreatedAt)\r\nVALUES (\r\n    @RunId, @ModelId, @TestId, @JsonValid, @CorrectTool, @CorrectCommand, @ParamsValid,\r\n    @ModelResponse, @ParsedToolCall, @JsonParseError, @ToolExecuted, @ExecutionSuccess,\r\n    @ToolResponse, @ExecutionError, @ResponseValidated, @ValidationReason,\r\n    @Passed, @TotalMs, @ExecutionMs, @TokensPerSec, @PromptTokens, @CompletionTokens, @CreatedAt);\r\nSELECT last_insert_rowid();", result);
	}
}
