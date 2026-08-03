using System.Collections.Generic;
using Squirmify.Core.Entities;

namespace Squirmify.Api.Controllers;

public class ConfigDetailResponse
{
	public TestSuiteConfig Config { get; set; } = null;

	public List<CategorySetting> CategorySettings { get; set; } = new List<CategorySetting>();

	public List<TestTypeLimit> TestTypeLimits { get; set; } = new List<TestTypeLimit>();
}
