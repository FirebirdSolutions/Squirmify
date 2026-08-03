using System.Collections.Generic;
using System.Threading.Tasks;
using Squirmify.Core.Entities;

namespace Squirmify.Core.Interfaces;

public interface IBenchmarkRepository
{
	Task<IEnumerable<BenchmarkRun>> GetAllRunsAsync();

	Task<IEnumerable<BenchmarkRun>> GetRecentRunsAsync(int count = 10);

	Task<BenchmarkRun?> GetRunByIdAsync(int id);

	Task<int> CreateRunAsync(BenchmarkRun run);

	Task UpdateRunAsync(BenchmarkRun run);

	Task UpdateRunStatusAsync(int runId, string status);

	Task UpdateRunProgressAsync(int runId, int completedTests, int errorCount);

	Task DeleteRunAsync(int id);

	Task<IEnumerable<BenchmarkRunModel>> GetRunModelsAsync(int runId);

	Task<BenchmarkRunModel?> GetRunModelAsync(int runId, int modelId);

	Task AddRunModelAsync(BenchmarkRunModel runModel);

	Task UpdateRunModelAsync(BenchmarkRunModel runModel);

	Task<IEnumerable<BenchmarkAutoJudge>> GetAutoJudgesAsync(int runId);

	Task AddAutoJudgeAsync(BenchmarkAutoJudge autoJudge);

	Task<IEnumerable<RunLog>> GetRunLogsAsync(int runId);

	Task AddRunLogAsync(RunLog log);
}
