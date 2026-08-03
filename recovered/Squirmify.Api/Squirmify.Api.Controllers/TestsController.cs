using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;

namespace Squirmify.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", new string[] { })]
public class TestsController(ITestDefinitionRepository tests) : ControllerBase
{
	[HttpGet("instruction")]
	public async Task<ActionResult<IEnumerable<InstructionTest>>> GetInstructionTests([FromQuery] string? category)
	{
		IEnumerable<InstructionTest> enumerable = ((category == null) ? (await tests.GetInstructionTestsAsync()) : (await tests.GetInstructionTestsByCategoryAsync(category)));
		IEnumerable<InstructionTest> result = enumerable;
		return Ok(result);
	}

	[HttpGet("instruction/{id:int}")]
	public async Task<ActionResult<InstructionTest>> GetInstructionTest(int id)
	{
		InstructionTest test = await tests.GetInstructionTestByIdAsync(id);
		if (test == null)
		{
			return NotFound();
		}
		return Ok(test);
	}

	[HttpPost("instruction")]
	public async Task<ActionResult<InstructionTest>> CreateInstructionTest([FromBody] InstructionTest test)
	{
		int id = (test.Id = await tests.CreateInstructionTestAsync(test));
		return CreatedAtAction("GetInstructionTest", new { id }, test);
	}

	[HttpPatch("instruction/{id:int}")]
	public async Task<ActionResult> UpdateInstructionTest(int id, [FromBody] InstructionTest test)
	{
		if (await tests.GetInstructionTestByIdAsync(id) == null)
		{
			return NotFound();
		}
		test.Id = id;
		await tests.UpdateInstructionTestAsync(test);
		return NoContent();
	}

	[HttpDelete("instruction/{id:int}")]
	public async Task<ActionResult> DeleteInstructionTest(int id)
	{
		if (await tests.GetInstructionTestByIdAsync(id) == null)
		{
			return NotFound();
		}
		await tests.DeleteInstructionTestAsync(id);
		return NoContent();
	}

	[HttpGet("reasoning")]
	public async Task<ActionResult<IEnumerable<ReasoningTest>>> GetReasoningTests([FromQuery] string? category)
	{
		IEnumerable<ReasoningTest> enumerable = ((category == null) ? (await tests.GetReasoningTestsAsync()) : (await tests.GetReasoningTestsByCategoryAsync(category)));
		IEnumerable<ReasoningTest> result = enumerable;
		return Ok(result);
	}

	[HttpGet("reasoning/{id:int}")]
	public async Task<ActionResult<ReasoningTest>> GetReasoningTest(int id)
	{
		ReasoningTest test = await tests.GetReasoningTestByIdAsync(id);
		if (test == null)
		{
			return NotFound();
		}
		return Ok(test);
	}

	[HttpPost("reasoning")]
	public async Task<ActionResult<ReasoningTest>> CreateReasoningTest([FromBody] ReasoningTest test)
	{
		int id = (test.Id = await tests.CreateReasoningTestAsync(test));
		return CreatedAtAction("GetReasoningTest", new { id }, test);
	}

	[HttpPatch("reasoning/{id:int}")]
	public async Task<ActionResult> UpdateReasoningTest(int id, [FromBody] ReasoningTest test)
	{
		if (await tests.GetReasoningTestByIdAsync(id) == null)
		{
			return NotFound();
		}
		test.Id = id;
		await tests.UpdateReasoningTestAsync(test);
		return NoContent();
	}

	[HttpDelete("reasoning/{id:int}")]
	public async Task<ActionResult> DeleteReasoningTest(int id)
	{
		if (await tests.GetReasoningTestByIdAsync(id) == null)
		{
			return NotFound();
		}
		await tests.DeleteReasoningTestAsync(id);
		return NoContent();
	}

	[HttpGet("conversation")]
	public async Task<ActionResult<IEnumerable<ConversationTest>>> GetConversationTests()
	{
		return Ok(await tests.GetConversationTestsAsync());
	}

	[HttpGet("conversation/{id:int}")]
	public async Task<ActionResult<ConversationTestDetail>> GetConversationTest(int id)
	{
		ConversationTest test = await tests.GetConversationTestByIdAsync(id);
		if (test == null)
		{
			return NotFound();
		}
		IEnumerable<ConversationTurn> turns = await tests.GetConversationTurnsAsync(id);
		IEnumerable<ConversationJudgingCriterion> criteria = await tests.GetConversationCriteriaAsync(id);
		return Ok(new ConversationTestDetail
		{
			Test = test,
			Turns = turns.ToList(),
			Criteria = criteria.ToList()
		});
	}

	[HttpPost("conversation")]
	public async Task<ActionResult<ConversationTest>> CreateConversationTest([FromBody] CreateConversationTestRequest request)
	{
		int id = await tests.CreateConversationTestAsync(request.Test, request.Turns, request.Criteria);
		request.Test.Id = id;
		return CreatedAtAction("GetConversationTest", new { id }, request.Test);
	}

	[HttpPatch("conversation/{id:int}")]
	public async Task<ActionResult> UpdateConversationTest(int id, [FromBody] CreateConversationTestRequest request)
	{
		if (await tests.GetConversationTestByIdAsync(id) == null)
		{
			return NotFound();
		}
		request.Test.Id = id;
		await tests.UpdateConversationTestAsync(request.Test, request.Turns, request.Criteria);
		return NoContent();
	}

	[HttpDelete("conversation/{id:int}")]
	public async Task<ActionResult> DeleteConversationTest(int id)
	{
		if (await tests.GetConversationTestByIdAsync(id) == null)
		{
			return NotFound();
		}
		await tests.DeleteConversationTestAsync(id);
		return NoContent();
	}

	[HttpGet("context-window")]
	public async Task<ActionResult<IEnumerable<ContextWindowTest>>> GetContextWindowTests()
	{
		return Ok(await tests.GetContextWindowTestsAsync());
	}

	[HttpGet("context-window/{id:int}")]
	public async Task<ActionResult<ContextWindowTestDetail>> GetContextWindowTest(int id)
	{
		ContextWindowTest test = await tests.GetContextWindowTestByIdAsync(id);
		if (test == null)
		{
			return NotFound();
		}
		IEnumerable<ContextWindowCheckpoint> checkpoints = await tests.GetContextWindowCheckpointsAsync(id);
		return Ok(new ContextWindowTestDetail
		{
			Test = test,
			Checkpoints = checkpoints.ToList()
		});
	}

	[HttpPost("context-window")]
	public async Task<ActionResult<ContextWindowTest>> CreateContextWindowTest([FromBody] CreateContextWindowTestRequest request)
	{
		int id = await tests.CreateContextWindowTestAsync(request.Test, request.Checkpoints);
		request.Test.Id = id;
		return CreatedAtAction("GetContextWindowTest", new { id }, request.Test);
	}

	[HttpPatch("context-window/{id:int}")]
	public async Task<ActionResult> UpdateContextWindowTest(int id, [FromBody] CreateContextWindowTestRequest request)
	{
		if (await tests.GetContextWindowTestByIdAsync(id) == null)
		{
			return NotFound();
		}
		request.Test.Id = id;
		await tests.UpdateContextWindowTestAsync(request.Test, request.Checkpoints);
		return NoContent();
	}

	[HttpGet("mcp-tool")]
	public async Task<ActionResult<IEnumerable<McpToolTest>>> GetMcpToolTests([FromQuery] string? category)
	{
		IEnumerable<McpToolTest> enumerable = ((category == null) ? (await tests.GetMcpToolTestsAsync()) : (await tests.GetMcpToolTestsByCategoryAsync(category)));
		IEnumerable<McpToolTest> result = enumerable;
		return Ok(result);
	}

	[HttpGet("mcp-tool/{id:int}")]
	public async Task<ActionResult<McpToolTest>> GetMcpToolTest(int id)
	{
		McpToolTest test = await tests.GetMcpToolTestByIdAsync(id);
		if (test == null)
		{
			return NotFound();
		}
		return Ok(test);
	}

	[HttpPost("mcp-tool")]
	public async Task<ActionResult<McpToolTest>> CreateMcpToolTest([FromBody] McpToolTest test)
	{
		int id = (test.Id = await tests.CreateMcpToolTestAsync(test));
		return CreatedAtAction("GetMcpToolTest", new { id }, test);
	}

	[HttpPatch("mcp-tool/{id:int}")]
	public async Task<ActionResult> UpdateMcpToolTest(int id, [FromBody] McpToolTest test)
	{
		if (await tests.GetMcpToolTestByIdAsync(id) == null)
		{
			return NotFound();
		}
		test.Id = id;
		await tests.UpdateMcpToolTestAsync(test);
		return NoContent();
	}

	[HttpDelete("mcp-tool/{id:int}")]
	public async Task<ActionResult> DeleteMcpToolTest(int id)
	{
		if (await tests.GetMcpToolTestByIdAsync(id) == null)
		{
			return NotFound();
		}
		await tests.DeleteMcpToolTestAsync(id);
		return NoContent();
	}
}
