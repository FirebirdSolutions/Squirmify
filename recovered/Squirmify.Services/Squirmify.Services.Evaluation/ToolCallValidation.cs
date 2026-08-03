using System.Collections.Generic;

namespace Squirmify.Services.Evaluation;

public class ToolCallValidation
{
	public bool CorrectCommand { get; set; }

	public bool HasParams { get; set; }

	public bool ParamsValid { get; set; }

	public List<string> Errors { get; set; } = new List<string>();

	public bool IsValid => CorrectCommand && ParamsValid;
}
