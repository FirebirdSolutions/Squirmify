using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Squirmify.Api.Hubs;

public class BenchmarkHub : Hub
{
	public async Task SubscribeToRun(int runId)
	{
		await base.Groups.AddToGroupAsync(base.Context.ConnectionId, $"run-{runId}");
	}

	public async Task UnsubscribeFromRun(int runId)
	{
		await base.Groups.RemoveFromGroupAsync(base.Context.ConnectionId, $"run-{runId}");
	}
}
