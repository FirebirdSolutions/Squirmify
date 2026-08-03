using System.Collections.Generic;
using Squirmify.Core.Entities;

namespace Squirmify.Api.Controllers;

public class CreateConfigRequest
{
	public TestSuiteConfig Config { get; set; } = null;

	public List<CategorySetting>? CategorySettings { get; set; }

	public List<TestTypeLimit>? TestTypeLimits { get; set; }
}
