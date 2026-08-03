using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;
using Squirmify.Data.Database;

namespace Squirmify.Data.Repositories;

public class BenchmarkRepository : IBenchmarkRepository
{
	private readonly IDbConnectionFactory _connectionFactory;

	public BenchmarkRepository(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public async Task<IEnumerable<BenchmarkRun>> GetAllRunsAsync()
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<BenchmarkRun>("SELECT * FROM BenchmarkRuns ORDER BY CreatedAt DESC");
	}

	public async Task<IEnumerable<BenchmarkRun>> GetRecentRunsAsync(int count = 10)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<BenchmarkRun>("SELECT * FROM BenchmarkRuns ORDER BY CreatedAt DESC LIMIT @Count", new
		{
			Count = count
		});
	}

	public async Task<BenchmarkRun?> GetRunByIdAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<BenchmarkRun>("SELECT * FROM BenchmarkRuns WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<int> CreateRunAsync(BenchmarkRun run)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT INTO BenchmarkRuns (Name, ConfigId, ProviderId, ModelGroupId, Status, StartedAt, TotalModels, TotalTests, BaseJudgeModelId, CreatedAt)\r\nVALUES (@Name, @ConfigId, @ProviderId, @ModelGroupId, @Status, @StartedAt, @TotalModels, @TotalTests, @BaseJudgeModelId, @CreatedAt);\r\nSELECT last_insert_rowid();", run);
	}

	public async Task UpdateRunAsync(BenchmarkRun run)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE BenchmarkRuns SET\r\n    Name = @Name,\r\n    Status = @Status,\r\n    StartedAt = @StartedAt,\r\n    CompletedAt = @CompletedAt,\r\n    TotalModels = @TotalModels,\r\n    TotalTests = @TotalTests,\r\n    CompletedTests = @CompletedTests,\r\n    ErrorCount = @ErrorCount,\r\n    BaseJudgeModelId = @BaseJudgeModelId\r\nWHERE Id = @Id", run);
	}

	public async Task UpdateRunStatusAsync(int runId, string status)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		bool flag;
		switch (status)
		{
		case "completed":
		case "failed":
		case "cancelled":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		DateTime? completedAt = (flag ? new DateTime?(DateTime.Now) : ((DateTime?)null));
		await connection.ExecuteAsync("UPDATE BenchmarkRuns SET Status = @Status, CompletedAt = @CompletedAt WHERE Id = @Id", new
		{
			Id = runId,
			Status = status,
			CompletedAt = completedAt
		});
	}

	public async Task UpdateRunProgressAsync(int runId, int completedTests, int errorCount)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE BenchmarkRuns SET CompletedTests = @CompletedTests, ErrorCount = @ErrorCount WHERE Id = @Id", new
		{
			Id = runId,
			CompletedTests = completedTests,
			ErrorCount = errorCount
		});
	}

	public async Task DeleteRunAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM BenchmarkRuns WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<IEnumerable<BenchmarkRunModel>> GetRunModelsAsync(int runId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<BenchmarkRunModel>("SELECT * FROM BenchmarkRunModels WHERE RunId = @RunId", new
		{
			RunId = runId
		});
	}

	public async Task<BenchmarkRunModel?> GetRunModelAsync(int runId, int modelId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<BenchmarkRunModel>("SELECT * FROM BenchmarkRunModels WHERE RunId = @RunId AND ModelId = @ModelId", new
		{
			RunId = runId,
			ModelId = modelId
		});
	}

	public async Task AddRunModelAsync(BenchmarkRunModel runModel)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("INSERT INTO BenchmarkRunModels (RunId, ModelId, Status)\r\nVALUES (@RunId, @ModelId, @Status)", runModel);
	}

	public async Task UpdateRunModelAsync(BenchmarkRunModel runModel)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE BenchmarkRunModels SET\r\n    Status = @Status,\r\n    QualificationPassed = @QualificationPassed,\r\n    InstructionPassRate = @InstructionPassRate,\r\n    InstructionStrictPassRate = @InstructionStrictPassRate,\r\n    ReasoningAvgScore = @ReasoningAvgScore,\r\n    ContextWindowAvgReliability = @ContextWindowAvgReliability,\r\n    ContextWindowAvgAccuracy = @ContextWindowAvgAccuracy,\r\n    ContextWindowTestCount = @ContextWindowTestCount,\r\n    IsBaseJudge = @IsBaseJudge,\r\n    IsAutoJudge = @IsAutoJudge\r\nWHERE RunId = @RunId AND ModelId = @ModelId", runModel);
	}

	public async Task<IEnumerable<BenchmarkAutoJudge>> GetAutoJudgesAsync(int runId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<BenchmarkAutoJudge>("SELECT * FROM BenchmarkAutoJudges WHERE RunId = @RunId", new
		{
			RunId = runId
		});
	}

	public async Task AddAutoJudgeAsync(BenchmarkAutoJudge autoJudge)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("INSERT INTO BenchmarkAutoJudges (RunId, ModelId, SelectionReason)\r\nVALUES (@RunId, @ModelId, @SelectionReason)", autoJudge);
	}

	public async Task<IEnumerable<RunLog>> GetRunLogsAsync(int runId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<RunLog>("SELECT * FROM RunLogs WHERE RunId = @RunId ORDER BY Timestamp ASC", new
		{
			RunId = runId
		});
	}

	public async Task AddRunLogAsync(RunLog log)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("INSERT INTO RunLogs (RunId, Level, Message, ModelName, Timestamp)\r\nVALUES (@RunId, @Level, @Message, @ModelName, @Timestamp)", log);
	}
}
