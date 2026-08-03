using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;

namespace Squirmify.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", new string[] { })]
public class ModelsController(IModelRepository models) : ControllerBase
{
	[HttpGet]
	public async Task<ActionResult<IEnumerable<Model>>> GetAll([FromQuery] int? providerId)
	{
		IEnumerable<Model> enumerable = ((!providerId.HasValue) ? (await models.GetAllAsync()) : (await models.GetByProviderAsync(providerId.Value)));
		IEnumerable<Model> result = enumerable;
		return Ok(result);
	}

	[HttpGet("{id:int}")]
	public async Task<ActionResult<Model>> GetById(int id)
	{
		Model model = await models.GetByIdAsync(id);
		if (model == null)
		{
			return NotFound();
		}
		return Ok(model);
	}

	[HttpGet("provider/{providerId:int}")]
	public async Task<ActionResult<IEnumerable<Model>>> GetByProvider(int providerId, [FromQuery] bool testableOnly = false)
	{
		IEnumerable<Model> enumerable = ((!testableOnly) ? (await models.GetByProviderAsync(providerId)) : (await models.GetTestableByProviderAsync(providerId)));
		IEnumerable<Model> result = enumerable;
		return Ok(result);
	}

	[HttpPatch("{id:int}/disabled")]
	public async Task<ActionResult> SetDisabled(int id, [FromBody] ToggleRequest request)
	{
		if (await models.GetByIdAsync(id) == null)
		{
			return NotFound();
		}
		await models.SetDisabledAsync(id, request.Value);
		return NoContent();
	}

	[HttpPatch("{id:int}/available")]
	public async Task<ActionResult> SetAvailable(int id, [FromBody] ToggleRequest request)
	{
		if (await models.GetByIdAsync(id) == null)
		{
			return NotFound();
		}
		await models.SetAvailableAsync(id, request.Value);
		return NoContent();
	}

	[HttpDelete("{id:int}")]
	public async Task<ActionResult> SoftDelete(int id)
	{
		if (await models.GetByIdAsync(id) == null)
		{
			return NotFound();
		}
		await models.SoftDeleteAsync(id);
		return NoContent();
	}

	[HttpPost("{id:int}/restore")]
	public async Task<ActionResult> Restore(int id)
	{
		await models.RestoreAsync(id);
		return NoContent();
	}
}
