using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Squirmify.Core.DTOs;

namespace Squirmify.Api.Hubs;

public class BenchmarkHubNotifier(IHubContext<BenchmarkHub> hubContext)
{
	public async Task NotifyProgressAsync(int runId, RunProgress progress)
	{
		await hubContext.Clients.Group($"run-{runId}").SendAsync("ProgressUpdate", progress);
	}

	public async Task NotifyLogEventAsync(int runId, LogEvent logEvent)
	{
		await hubContext.Clients.Group($"run-{runId}").SendAsync("LogEvent", logEvent);
	}

	public async Task NotifyRunCompleteAsync(int runId, string status)
	{
		await hubContext.Clients.Group($"run-{runId}").SendAsync("RunComplete", runId, status);
	}
}
