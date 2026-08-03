using System.Collections.Generic;
using System.Threading.Tasks;
using Squirmify.Core.DTOs;

namespace Squirmify.Core.Interfaces;

public interface IAnalysisService
{
	Task<RunAnalysis> AnalyzeRunAsync(int runId);

	Task<RunComparison> CompareRunsAsync(IEnumerable<int> runIds);

	Task<ModelComparisonAcrossRuns> CompareModelAcrossRunsAsync(int modelId, IEnumerable<int> runIds);
}
