using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Squirmify.Api.Hubs;
using Squirmify.Core;
using Squirmify.Core.DTOs;
using Squirmify.Core.Interfaces;
using Squirmify.Services;

public class Program
{
	private static async Task _003CMain_003E_0024(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
		string dataDir = SquirmifyPaths.ResolveDataDirectory(args);
		string connectionString = SquirmifyPaths.GetConnectionString(dataDir);
		Console.WriteLine("[Squirmify] Data directory: " + dataDir);
		builder.Services.AddSquirmifyServices(connectionString);
		builder.Services.AddSignalR();
		builder.Services.AddSingleton<BenchmarkHubNotifier>();
		builder.Services.AddControllers().AddJsonOptions(delegate(JsonOptions options)
		{
			options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
			options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
			options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
		});
		builder.Services.AddCors(delegate(CorsOptions options)
		{
			options.AddPolicy("DevCors", delegate(CorsPolicyBuilder policy)
			{
				policy.WithOrigins("http://localhost:5173", "http://localhost:5174").AllowAnyMethod().AllowAnyHeader()
					.AllowCredentials();
			});
		});
		WebApplication app = builder.Build();
		await app.Services.InitializeDatabaseAsync();
		IBenchmarkOrchestrator orchestrator = app.Services.GetRequiredService<IBenchmarkOrchestrator>();
		BenchmarkHubNotifier notifier = app.Services.GetRequiredService<BenchmarkHubNotifier>();
		orchestrator.OnProgressUpdate += async delegate(RunProgress progress)
		{
			try
			{
				await notifier.NotifyProgressAsync(progress.RunId, progress);
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Console.WriteLine("[SignalR] Progress notification failed: " + ex2.Message);
			}
		};
		orchestrator.OnLogEvent += async delegate(LogEvent logEvent)
		{
			try
			{
				await notifier.NotifyLogEventAsync(logEvent.RunId, logEvent);
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Console.WriteLine("[SignalR] Log notification failed: " + ex2.Message);
			}
		};
		app.UseCors("DevCors");
		if (!app.Environment.IsDevelopment())
		{
			app.UseHsts();
			app.UseHttpsRedirection();
		}
		app.UseDefaultFiles();
		app.UseStaticFiles();
		app.MapControllers();
		app.MapHub<BenchmarkHub>("/hubs/benchmark");
		app.MapFallbackToFile("index.html");
		app.Run();
	}
}
