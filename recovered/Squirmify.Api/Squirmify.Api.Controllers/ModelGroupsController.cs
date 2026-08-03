using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;

namespace Squirmify.Api.Controllers;

[ApiController]
[Route("api/model-groups")]
[Produces("application/json", new string[] { })]
public class ModelGroupsController(IModelGroupRepository groups) : ControllerBase
{
	[HttpGet]
	public async Task<ActionResult<IEnumerable<ModelGroup>>> GetAll()
	{
		return Ok(await groups.GetAllAsync());
	}

	[HttpGet("{id:int}")]
	public async Task<ActionResult<ModelGroupDetail>> GetById(int id)
	{
		ModelGroup group = await groups.GetByIdAsync(id);
		if (group == null)
		{
			return NotFound();
		}
		IEnumerable<Model> models = await groups.GetModelsAsync(id);
		return Ok(new ModelGroupDetail
		{
			Group = group,
			Models = models.ToList()
		});
	}

	[HttpPost]
	public async Task<ActionResult<ModelGroup>> Create([FromBody] CreateModelGroupRequest request)
	{
		ModelGroup group = new ModelGroup
		{
			Name = request.Name,
			Description = request.Description
		};
		int id = (group.Id = await groups.CreateAsync(group));
		if (request.ModelIds?.Any() ?? false)
		{
			await groups.SetMembersAsync(id, request.ModelIds);
		}
		return CreatedAtAction("GetById", new { id }, group);
	}

	[HttpPatch("{id:int}")]
	public async Task<ActionResult> Update(int id, [FromBody] UpdateModelGroupRequest request)
	{
		ModelGroup group = await groups.GetByIdAsync(id);
		if (group == null)
		{
			return NotFound();
		}
		if (request.Name != null)
		{
			group.Name = request.Name;
		}
		if (request.Description != null)
		{
			group.Description = request.Description;
		}
		await groups.UpdateAsync(group);
		if (request.ModelIds != null)
		{
			await groups.SetMembersAsync(id, request.ModelIds);
		}
		return NoContent();
	}

	[HttpDelete("{id:int}")]
	public async Task<ActionResult> Delete(int id)
	{
		if (await groups.GetByIdAsync(id) == null)
		{
			return NotFound();
		}
		await groups.DeleteAsync(id);
		return NoContent();
	}

	[HttpPost("{id:int}/models")]
	public async Task<ActionResult> AddModel(int id, [FromBody] AddModelRequest request)
	{
		if (await groups.GetByIdAsync(id) == null)
		{
			return NotFound();
		}
		await groups.AddMemberAsync(id, request.ModelId);
		return NoContent();
	}

	[HttpDelete("{id:int}/models/{modelId:int}")]
	public async Task<ActionResult> RemoveModel(int id, int modelId)
	{
		if (await groups.GetByIdAsync(id) == null)
		{
			return NotFound();
		}
		await groups.RemoveMemberAsync(id, modelId);
		return NoContent();
	}

	[HttpGet("{id:int}/models")]
	public async Task<ActionResult<IEnumerable<Model>>> GetModels(int id)
	{
		if (await groups.GetByIdAsync(id) == null)
		{
			return NotFound();
		}
		return Ok(await groups.GetModelsAsync(id));
	}
}
