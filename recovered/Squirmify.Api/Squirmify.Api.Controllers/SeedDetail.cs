using System.Collections.Generic;
using Squirmify.Core.Entities;

namespace Squirmify.Api.Controllers;

public class SeedDetail
{
	public Seed Seed { get; set; } = null;

	public List<string> Tags { get; set; } = new List<string>();
}
