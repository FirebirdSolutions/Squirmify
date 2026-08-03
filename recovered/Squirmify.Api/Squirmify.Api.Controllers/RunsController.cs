using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Squirmify.Api.Hubs;
using Squirmify.Core.DTOs;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;

namespace Squirmify.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", new string[] { })]
public class RunsController(IBenchmarkRepository benchmarkRepo, IBenchmarkOrchestrator orchestrator, IAnalysisService analysis, IResultsRepository results, BenchmarkHubNotifier notifier) : ControllerBase
{
	[HttpGet]
	public async Task<ActionResult<IEnumerable<BenchmarkRun>>> GetAll([FromQuery] int count = 20)
	{
		return Ok(await benchmarkRepo.GetRecentRunsAsync(count));
	}

	[HttpGet("{id:int}")]
	public async Task<ActionResult<RunDetailResponse>> GetById(int id)
	{
		BenchmarkRun run = await benchmarkRepo.GetRunByIdAsync(id);
		if (run == null)
		{
			return NotFound();
		}
		IEnumerable<BenchmarkRunModel> models = await benchmarkRepo.GetRunModelsAsync(id);
		IEnumerable<BenchmarkAutoJudge> autoJudges = await benchmarkRepo.GetAutoJudgesAsync(id);
		return Ok(new RunDetailResponse
		{
			Run = run,
			Models = models.ToList(),
			AutoJudges = autoJudges.ToList()
		});
	}

	[HttpPost]
	public async Task<ActionResult> StartRun([FromBody] StartRunRequest request)
	{
		Task.Run(async delegate
		{
			try
			{
				int runId = await orchestrator.StartRunAsync(request.ConfigId, request.ProviderId, request.ModelGroupId, request.JudgeModelId, request.Name);
				await notifier.NotifyRunCompleteAsync(runId, "completed");
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Console.WriteLine("[Run] Failed: " + ex2.Message);
			}
		});
		return Accepted(new
		{
			message = "Benchmark run started"
		});
	}

	[HttpPost("{id:int}/cancel")]
	public async Task<ActionResult> CancelRun(int id)
	{
		if (await benchmarkRepo.GetRunByIdAsync(id) == null)
		{
			return NotFound();
		}
		await orchestrator.CancelRunAsync(id);
		return Ok(new
		{
			message = "Run cancellation requested"
		});
	}

	[HttpPost("{id:int}/rescore")]
	public async Task<ActionResult> RescoreRun(int id)
	{
		if (await benchmarkRepo.GetRunByIdAsync(id) == null)
		{
			return NotFound();
		}
		Task.Run(async delegate
		{
			try
			{
				await orchestrator.RescoreRunAsync(id);
				await notifier.NotifyRunCompleteAsync(id, "rescored");
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Console.WriteLine("[Rescore] Failed: " + ex2.Message);
			}
		});
		return Accepted(new
		{
			message = "Rescore started"
		});
	}

	[HttpGet("{id:int}/progress")]
	public ActionResult<RunProgress> GetProgress(int id)
	{
		RunProgress currentProgress = orchestrator.GetCurrentProgress(id);
		if (currentProgress == null)
		{
			return NotFound();
		}
		return Ok(currentProgress);
	}

	[HttpGet("{id:int}/logs")]
	public async Task<ActionResult<IEnumerable<RunLog>>> GetLogs(int id)
	{
		if (await benchmarkRepo.GetRunByIdAsync(id) == null)
		{
			return NotFound();
		}
		return Ok(await benchmarkRepo.GetRunLogsAsync(id));
	}

	[HttpGet("{id:int}/results")]
	public async Task<ActionResult<RunAnalysis>> GetResults(int id)
	{
		if (await benchmarkRepo.GetRunByIdAsync(id) == null)
		{
			return NotFound();
		}
		return Ok(await analysis.AnalyzeRunAsync(id));
	}

	[HttpGet("{id:int}/results/instruction")]
	public async Task<ActionResult<IEnumerable<InstructionTestResult>>> GetInstructionResults(int id)
	{
		return Ok(await results.GetInstructionResultsAsync(id));
	}

	[HttpGet("{id:int}/results/reasoning")]
	public async Task<ActionResult<IEnumerable<ReasoningTestResult>>> GetReasoningResults(int id)
	{
		return Ok(await results.GetReasoningResultsAsync(id));
	}

	[HttpGet("{id:int}/results/conversation")]
	public async Task<ActionResult<IEnumerable<ConversationTestResult>>> GetConversationResults(int id)
	{
		return Ok(await results.GetConversationResultsAsync(id));
	}

	[HttpGet("{id:int}/results/context-window")]
	public async Task<ActionResult<IEnumerable<ContextWindowTestResult>>> GetContextWindowResults(int id)
	{
		return Ok(await results.GetContextWindowResultsAsync(id));
	}

	[HttpGet("{id:int}/results/generation")]
	public async Task<ActionResult<IEnumerable<GenerationResult>>> GetGenerationResults(int id, [FromQuery] bool highQualityOnly = false)
	{
		IEnumerable<GenerationResult> enumerable = ((!highQualityOnly) ? (await results.GetGenerationResultsAsync(id)) : (await results.GetHighQualityResultsAsync(id)));
		IEnumerable<GenerationResult> generationResults = enumerable;
		return Ok(generationResults);
	}

	[HttpDelete("{id:int}")]
	public async Task<ActionResult> Delete(int id)
	{
		if (await benchmarkRepo.GetRunByIdAsync(id) == null)
		{
			return NotFound();
		}
		await benchmarkRepo.DeleteRunAsync(id);
		return NoContent();
	}
}
