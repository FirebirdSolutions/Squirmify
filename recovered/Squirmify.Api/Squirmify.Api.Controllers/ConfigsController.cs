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
public class ConfigsController(IConfigRepository configs) : ControllerBase
{
	[HttpGet]
	public async Task<ActionResult<IEnumerable<TestSuiteConfig>>> GetAll()
	{
		return Ok(await configs.GetAllConfigsAsync());
	}

	[HttpGet("{id:int}")]
	public async Task<ActionResult<ConfigDetailResponse>> GetById(int id)
	{
		TestSuiteConfig config = await configs.GetConfigByIdAsync(id);
		if (config == null)
		{
			return NotFound();
		}
		IEnumerable<CategorySetting> categorySettings = await configs.GetCategorySettingsAsync(id);
		IEnumerable<TestTypeLimit> testTypeLimits = await configs.GetTestTypeLimitsAsync(id);
		return Ok(new ConfigDetailResponse
		{
			Config = config,
			CategorySettings = categorySettings.ToList(),
			TestTypeLimits = testTypeLimits.ToList()
		});
	}

	[HttpPost]
	public async Task<ActionResult<TestSuiteConfig>> Create([FromBody] CreateConfigRequest request)
	{
		TestSuiteConfig config = request.Config;
		int id = (config.Id = await configs.CreateConfigAsync(config));
		if (request.CategorySettings?.Any() ?? false)
		{
			await configs.SaveCategorySettingsAsync(id, request.CategorySettings);
		}
		if (request.TestTypeLimits?.Any() ?? false)
		{
			await configs.SaveTestTypeLimitsAsync(id, request.TestTypeLimits);
		}
		return CreatedAtAction("GetById", new { id }, config);
	}

	[HttpPatch("{id:int}")]
	public async Task<ActionResult> Update(int id, [FromBody] UpdateConfigRequest request)
	{
		if (await configs.GetConfigByIdAsync(id) == null)
		{
			return NotFound();
		}
		if (request.Config != null)
		{
			request.Config.Id = id;
			request.Config.UpdatedAt = DateTime.UtcNow;
			await configs.UpdateConfigAsync(request.Config);
		}
		if (request.CategorySettings != null)
		{
			await configs.SaveCategorySettingsAsync(id, request.CategorySettings);
		}
		if (request.TestTypeLimits != null)
		{
			await configs.SaveTestTypeLimitsAsync(id, request.TestTypeLimits);
		}
		return NoContent();
	}

	[HttpDelete("{id:int}")]
	public async Task<ActionResult> Delete(int id)
	{
		if (await configs.GetConfigByIdAsync(id) == null)
		{
			return NotFound();
		}
		await configs.DeleteConfigAsync(id);
		return NoContent();
	}

	[HttpGet("{id:int}/category-settings")]
	public async Task<ActionResult<IEnumerable<CategorySetting>>> GetCategorySettings(int id)
	{
		return Ok(await configs.GetCategorySettingsAsync(id));
	}

	[HttpGet("{id:int}/test-type-limits")]
	public async Task<ActionResult<IEnumerable<TestTypeLimit>>> GetTestTypeLimits(int id)
	{
		return Ok(await configs.GetTestTypeLimitsAsync(id));
	}
}
