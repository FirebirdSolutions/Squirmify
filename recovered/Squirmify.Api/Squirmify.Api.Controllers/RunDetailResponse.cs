using System.Collections.Generic;
using Squirmify.Core.Entities;

namespace Squirmify.Api.Controllers;

public class RunDetailResponse
{
	public BenchmarkRun Run { get; set; } = null;

	public List<BenchmarkRunModel> Models { get; set; } = new List<BenchmarkRunModel>();

	public List<BenchmarkAutoJudge> AutoJudges { get; set; } = new List<BenchmarkAutoJudge>();
}
