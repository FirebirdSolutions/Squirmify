using System;
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
public class ProvidersController(IProviderRepository providers, ILlmClient llmClient) : ControllerBase
{
	[HttpGet]
	public async Task<ActionResult<IEnumerable<Provider>>> GetAll()
	{
		return Ok(await providers.GetAllAsync());
	}

	[HttpGet("{id:int}")]
	public async Task<ActionResult<Provider>> GetById(int id)
	{
		Provider provider = await providers.GetByIdAsync(id);
		if (provider == null)
		{
			return NotFound();
		}
		return Ok(provider);
	}

	[HttpPost]
	public async Task<ActionResult<Provider>> Create([FromBody] CreateProviderRequest request)
	{
		Provider provider = new Provider
		{
			Name = request.Name,
			BaseUrl = request.BaseUrl,
			AuthToken = request.AuthToken,
			UseAuth = request.UseAuth,
			TimeoutMinutes = request.TimeoutMinutes
		};
		int id = (provider.Id = await providers.CreateAsync(provider));
		return CreatedAtAction("GetById", new { id }, provider);
	}

	[HttpPatch("{id:int}")]
	public async Task<ActionResult> Update(int id, [FromBody] UpdateProviderRequest request)
	{
		Provider provider = await providers.GetByIdAsync(id);
		if (provider == null)
		{
			return NotFound();
		}
		if (request.Name != null)
		{
			provider.Name = request.Name;
		}
		if (request.BaseUrl != null)
		{
			provider.BaseUrl = request.BaseUrl;
		}
		if (request.AuthToken != null)
		{
			provider.AuthToken = request.AuthToken;
		}
		if (request.UseAuth.HasValue)
		{
			provider.UseAuth = request.UseAuth.Value;
		}
		if (request.TimeoutMinutes.HasValue)
		{
			provider.TimeoutMinutes = request.TimeoutMinutes.Value;
		}
		if (request.IsActive.HasValue)
		{
			provider.IsActive = request.IsActive.Value;
		}
		provider.UpdatedAt = DateTime.UtcNow;
		await providers.UpdateAsync(provider);
		return Ok(provider);
	}

	[HttpDelete("{id:int}")]
	public async Task<ActionResult> Delete(int id)
	{
		if (await providers.GetByIdAsync(id) == null)
		{
			return NotFound();
		}
		await providers.DeleteAsync(id);
		return NoContent();
	}

	[HttpPost("{id:int}/sync-models")]
	public async Task<ActionResult<IEnumerable<string>>> SyncModels(int id)
	{
		Provider provider = await providers.GetByIdAsync(id);
		if (provider == null)
		{
			return NotFound();
		}
		IEnumerable<string> models = await llmClient.LoadModelsFromServerAsync(provider);
		return Ok(new
		{
			models = models,
			count = models.Count()
		});
	}
}
