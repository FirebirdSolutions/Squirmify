using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;
using Squirmify.Services.Seeds;

namespace Squirmify.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", new string[] { })]
public class SeedsController(ISeedRepository seeds, SeedGenerationService seedGen) : ControllerBase
{
	[HttpGet]
	public async Task<ActionResult<IEnumerable<Seed>>> GetAll([FromQuery] string? category)
	{
		IEnumerable<Seed> enumerable = ((category == null) ? (await seeds.GetAllAsync()) : (await seeds.GetByCategoryAsync(category)));
		IEnumerable<Seed> result = enumerable;
		return Ok(result);
	}

	[HttpGet("stats")]
	public async Task<ActionResult> GetStats()
	{
		int baseCount = await seeds.GetBaseSeedCountAsync();
		int augmentedCount = await seeds.GetAugmentedSeedCountAsync();
		return Ok(new
		{
			baseCount = baseCount,
			augmentedCount = augmentedCount,
			total = baseCount + augmentedCount
		});
	}

	[HttpGet("{id:int}")]
	public async Task<ActionResult<SeedDetail>> GetById(int id)
	{
		Seed seed = await seeds.GetByIdAsync(id);
		if (seed == null)
		{
			return NotFound();
		}
		IEnumerable<string> tags = await seeds.GetTagsAsync(id);
		return Ok(new SeedDetail
		{
			Seed = seed,
			Tags = tags.ToList()
		});
	}

	[HttpPost]
	public async Task<ActionResult<Seed>> Create([FromBody] CreateSeedRequest request)
	{
		Seed seed = new Seed
		{
			Category = request.Category,
			Instruction = request.Instruction,
			Temperature = request.Temperature,
			TopP = request.TopP,
			MaxTokens = request.MaxTokens,
			IsAugmented = false
		};
		int id = (seed.Id = await seeds.CreateAsync(seed, request.Tags));
		return CreatedAtAction("GetById", new { id }, seed);
	}

	[HttpPatch("{id:int}")]
	public async Task<ActionResult> Update(int id, [FromBody] UpdateSeedRequest request)
	{
		Seed seed = await seeds.GetByIdAsync(id);
		if (seed == null)
		{
			return NotFound();
		}
		if (request.Category != null)
		{
			seed.Category = request.Category;
		}
		if (request.Instruction != null)
		{
			seed.Instruction = request.Instruction;
		}
		if (request.Temperature.HasValue)
		{
			seed.Temperature = request.Temperature;
		}
		if (request.TopP.HasValue)
		{
			seed.TopP = request.TopP;
		}
		if (request.MaxTokens.HasValue)
		{
			seed.MaxTokens = request.MaxTokens;
		}
		await seeds.UpdateAsync(seed, request.Tags);
		return NoContent();
	}

	[HttpDelete("{id:int}")]
	public async Task<ActionResult> Delete(int id)
	{
		if (await seeds.GetByIdAsync(id) == null)
		{
			return NotFound();
		}
		await seeds.DeleteAsync(id);
		return NoContent();
	}

	[HttpDelete("augmented")]
	public async Task<ActionResult> DeleteAugmented()
	{
		await seeds.DeleteAugmentedSeedsAsync();
		return NoContent();
	}

	[HttpPost("augment")]
	public async Task<ActionResult> Augment([FromBody] AugmentRequest request)
	{
		await seedGen.GenerateAugmentedSeedsAsync(request.ConfigId);
		var stats = new
		{
			baseCount = await seeds.GetBaseSeedCountAsync(),
			augmentedCount = await seeds.GetAugmentedSeedCountAsync()
		};
		return Ok(stats);
	}
}
