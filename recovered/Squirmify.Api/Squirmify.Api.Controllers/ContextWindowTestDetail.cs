using System.Collections.Generic;
using Squirmify.Core.Entities;

namespace Squirmify.Api.Controllers;

public class ContextWindowTestDetail
{
	public ContextWindowTest Test { get; set; } = null;

	public List<ContextWindowCheckpoint> Checkpoints { get; set; } = new List<ContextWindowCheckpoint>();
}
