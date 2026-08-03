using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Squirmify.Core.Interfaces;
using Squirmify.Data.Database;
using Squirmify.Data.Repositories;
using Squirmify.Services.Analysis;
using Squirmify.Services.Evaluation;
using Squirmify.Services.Orchestration;
using Squirmify.Services.Seeds;

namespace Squirmify.Services;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddSquirmifyServices(this IServiceCollection services, string connectionString)
	{
		services.AddSingleton((IDbConnectionFactory)new SqliteConnectionFactory(connectionString));
		services.AddSingleton((IServiceProvider sp) => new DatabaseInitializer(connectionString));
		services.AddScoped<IProviderRepository, ProviderRepository>();
		services.AddScoped<IModelRepository, ModelRepository>();
		services.AddScoped<IConfigRepository, ConfigRepository>();
		services.AddScoped<ITestDefinitionRepository, TestDefinitionRepository>();
		services.AddScoped<ISeedRepository, SeedRepository>();
		services.AddScoped<IBenchmarkRepository, BenchmarkRepository>();
		services.AddScoped<IResultsRepository, ResultsRepository>();
		services.AddScoped<IModelGroupRepository, ModelGroupRepository>();
		services.AddHttpClient();
		services.AddScoped<ILlmClient, LlmClient>();
		services.AddScoped<IEchoMcpClient, McpSdkClient>();
		services.AddSingleton<IBenchmarkOrchestrator, BenchmarkOrchestrator>();
		services.AddScoped<SeedGenerationService>();
		services.AddScoped<IAnalysisService, AnalysisService>();
		return services;
	}

	public static async Task InitializeDatabaseAsync(this IServiceProvider services)
	{
		DatabaseInitializer initializer = services.GetRequiredService<DatabaseInitializer>();
		await initializer.InitializeAsync();
	}
}
