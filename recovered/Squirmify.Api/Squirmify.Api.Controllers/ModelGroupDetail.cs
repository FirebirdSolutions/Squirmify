using System.Collections.Generic;
using Squirmify.Core.Entities;

namespace Squirmify.Api.Controllers;

public class ModelGroupDetail
{
	public ModelGroup Group { get; set; } = null;

	public List<Model> Models { get; set; } = new List<Model>();
}
