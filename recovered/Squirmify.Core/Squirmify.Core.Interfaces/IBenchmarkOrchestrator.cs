using System;
using System.Threading;
using System.Threading.Tasks;
using Squirmify.Core.DTOs;

namespace Squirmify.Core.Interfaces;

public interface IBenchmarkOrchestrator
{
	event Action<RunProgress>? OnProgressUpdate;

	event Action<LogEvent>? OnLogEvent;

	Task<int> StartRunAsync(int configId, int providerId, string? runName = null, CancellationToken cancellationToken = default(CancellationToken));

	Task<int> StartRunAsync(int configId, int providerId, int? modelGroupId, int? judgeModelId, string? runName = null, CancellationToken cancellationToken = default(CancellationToken));

	Task CancelRunAsync(int runId);

	Task RescoreRunAsync(int runId, CancellationToken cancellationToken = default(CancellationToken));

	RunProgress? GetCurrentProgress(int runId);
}
