using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Squirmify.Core.DTOs;
using Squirmify.Core.Interfaces;

namespace Squirmify.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", new string[] { })]
public class AnalysisController(IAnalysisService analysis) : ControllerBase
{
	[HttpPost("compare")]
	public async Task<ActionResult<RunComparison>> CompareRuns([FromBody] CompareRunsRequest request)
	{
		if (request.RunIds.Count < 2)
		{
			return BadRequest("At least 2 run IDs are required for comparison");
		}
		return Ok(await analysis.CompareRunsAsync(request.RunIds));
	}

	[HttpPost("model-comparison")]
	public async Task<ActionResult<ModelComparisonAcrossRuns>> CompareModel([FromBody] CompareModelRequest request)
	{
		return Ok(await analysis.CompareModelAcrossRunsAsync(request.ModelId, request.RunIds));
	}
}
