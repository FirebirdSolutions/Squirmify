using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;
using Squirmify.Data.Database;

namespace Squirmify.Data.Repositories;

public class ConfigRepository : IConfigRepository
{
	private readonly IDbConnectionFactory _connectionFactory;

	public ConfigRepository(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public async Task<IEnumerable<TestSuiteConfig>> GetAllConfigsAsync()
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<TestSuiteConfig>("SELECT * FROM TestSuiteConfigs ORDER BY Name");
	}

	public async Task<TestSuiteConfig?> GetConfigByIdAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<TestSuiteConfig>("SELECT * FROM TestSuiteConfigs WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<int> CreateConfigAsync(TestSuiteConfig config)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT INTO TestSuiteConfigs (\r\n    Name, Description, RunPromptTests, RunContextWindowTests, RunConversationTests,\r\n    RunQualificationTests, MaxInstructionTests, MaxReasoningTests, MaxConversationTests,\r\n    HighQualityThreshold, InstructionPassThreshold, TopJudgeCount, ContextWindowLevel,\r\n    ContextWindowTestType, ContextWindowTargetTokens, ContextWindowProbeCount, ContextWindowCheckpoints,\r\n    ContextWindowMaxTests, ContextWindowTestIds, TargetSeedCount, OverwriteSeeds,\r\n    GlobalTemperature, GlobalTopP, GlobalMaxTokens, MaxParallelRequests,\r\n    RunMcpToolTests, MaxMcpToolTests, EchoMcpBaseUrl, EchoMcpToken, FetchSchemasFromEchoMcp,\r\n    McpTransportType, McpServerUrl, McpServerCommand, McpServerArgs,\r\n    CreatedAt\r\n) VALUES (\r\n    @Name, @Description, @RunPromptTests, @RunContextWindowTests, @RunConversationTests,\r\n    @RunQualificationTests, @MaxInstructionTests, @MaxReasoningTests, @MaxConversationTests,\r\n    @HighQualityThreshold, @InstructionPassThreshold, @TopJudgeCount, @ContextWindowLevel,\r\n    @ContextWindowTestType, @ContextWindowTargetTokens, @ContextWindowProbeCount, @ContextWindowCheckpoints,\r\n    @ContextWindowMaxTests, @ContextWindowTestIds, @TargetSeedCount, @OverwriteSeeds,\r\n    @GlobalTemperature, @GlobalTopP, @GlobalMaxTokens, @MaxParallelRequests,\r\n    @RunMcpToolTests, @MaxMcpToolTests, @EchoMcpBaseUrl, @EchoMcpToken, @FetchSchemasFromEchoMcp,\r\n    @McpTransportType, @McpServerUrl, @McpServerCommand, @McpServerArgs,\r\n    @CreatedAt\r\n);\r\nSELECT last_insert_rowid();", config);
	}

	public async Task UpdateConfigAsync(TestSuiteConfig config)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		config.UpdatedAt = DateTime.UtcNow;
		await connection.ExecuteAsync("UPDATE TestSuiteConfigs SET\r\n    Name = @Name, Description = @Description,\r\n    RunPromptTests = @RunPromptTests, RunContextWindowTests = @RunContextWindowTests,\r\n    RunConversationTests = @RunConversationTests, RunQualificationTests = @RunQualificationTests,\r\n    MaxInstructionTests = @MaxInstructionTests, MaxReasoningTests = @MaxReasoningTests,\r\n    MaxConversationTests = @MaxConversationTests,\r\n    HighQualityThreshold = @HighQualityThreshold, InstructionPassThreshold = @InstructionPassThreshold,\r\n    TopJudgeCount = @TopJudgeCount, ContextWindowLevel = @ContextWindowLevel,\r\n    ContextWindowTestType = @ContextWindowTestType,\r\n    ContextWindowTargetTokens = @ContextWindowTargetTokens,\r\n    ContextWindowProbeCount = @ContextWindowProbeCount,\r\n    ContextWindowCheckpoints = @ContextWindowCheckpoints,\r\n    ContextWindowMaxTests = @ContextWindowMaxTests,\r\n    ContextWindowTestIds = @ContextWindowTestIds,\r\n    TargetSeedCount = @TargetSeedCount, OverwriteSeeds = @OverwriteSeeds,\r\n    GlobalTemperature = @GlobalTemperature, GlobalTopP = @GlobalTopP,\r\n    GlobalMaxTokens = @GlobalMaxTokens, MaxParallelRequests = @MaxParallelRequests,\r\n    RunMcpToolTests = @RunMcpToolTests, MaxMcpToolTests = @MaxMcpToolTests,\r\n    EchoMcpBaseUrl = @EchoMcpBaseUrl, EchoMcpToken = @EchoMcpToken,\r\n    FetchSchemasFromEchoMcp = @FetchSchemasFromEchoMcp,\r\n    McpTransportType = @McpTransportType, McpServerUrl = @McpServerUrl,\r\n    McpServerCommand = @McpServerCommand, McpServerArgs = @McpServerArgs,\r\n    UpdatedAt = @UpdatedAt\r\nWHERE Id = @Id", config);
	}

	public async Task DeleteConfigAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM TestSuiteConfigs WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<IEnumerable<CategorySetting>> GetCategorySettingsAsync(int configId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<CategorySetting>("SELECT * FROM CategorySettings WHERE ConfigId = @ConfigId ORDER BY Category", new
		{
			ConfigId = configId
		});
	}

	public async Task SaveCategorySettingsAsync(int configId, IEnumerable<CategorySetting> settings)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		await connection.ExecuteAsync("DELETE FROM CategorySettings WHERE ConfigId = @ConfigId", new
		{
			ConfigId = configId
		}, transaction);
		foreach (CategorySetting setting in settings)
		{
			setting.ConfigId = configId;
			await connection.ExecuteAsync("INSERT INTO CategorySettings (ConfigId, Category, Temperature, TopP, MaxTokens, SystemPrompt, Weight)\r\nVALUES (@ConfigId, @Category, @Temperature, @TopP, @MaxTokens, @SystemPrompt, @Weight)", setting, transaction);
		}
		transaction.Commit();
	}

	public async Task<IEnumerable<TestTypeLimit>> GetTestTypeLimitsAsync(int configId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<TestTypeLimit>("SELECT * FROM TestTypeLimits WHERE ConfigId = @ConfigId ORDER BY TestType, Category", new
		{
			ConfigId = configId
		});
	}

	public async Task SaveTestTypeLimitsAsync(int configId, IEnumerable<TestTypeLimit> limits)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		await connection.ExecuteAsync("DELETE FROM TestTypeLimits WHERE ConfigId = @ConfigId", new
		{
			ConfigId = configId
		}, transaction);
		foreach (TestTypeLimit limit in limits)
		{
			limit.ConfigId = configId;
			await connection.ExecuteAsync("INSERT INTO TestTypeLimits (ConfigId, TestType, Category, MaxTests, Temperature, TopP, MaxTokens, PassThreshold, MinScore)\r\nVALUES (@ConfigId, @TestType, @Category, @MaxTests, @Temperature, @TopP, @MaxTokens, @PassThreshold, @MinScore)", limit, transaction);
		}
		transaction.Commit();
	}
}
