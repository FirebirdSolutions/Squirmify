using System.Collections.Generic;
using Squirmify.Core.Entities;

namespace Squirmify.Api.Controllers;

public class CreateConversationTestRequest
{
	public ConversationTest Test { get; set; } = null;

	public List<ConversationTurn> Turns { get; set; } = new List<ConversationTurn>();

	public List<ConversationJudgingCriterion> Criteria { get; set; } = new List<ConversationJudgingCriterion>();
}
