using System.Collections.Generic;
using Squirmify.Core.Entities;

namespace Squirmify.Api.Controllers;

public class UpdateConfigRequest
{
	public TestSuiteConfig? Config { get; set; }

	public List<CategorySetting>? CategorySettings { get; set; }

	public List<TestTypeLimit>? TestTypeLimits { get; set; }
}
