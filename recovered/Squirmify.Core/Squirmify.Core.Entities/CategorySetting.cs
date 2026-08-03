namespace Squirmify.Core.Entities;

public class CategorySetting
{
	public int Id { get; set; }

	public int ConfigId { get; set; }

	public string Category { get; set; } = string.Empty;

	public double? Temperature { get; set; }

	public double? TopP { get; set; }

	public int? MaxTokens { get; set; }

	public string? SystemPrompt { get; set; }

	public double Weight { get; set; } = 0.25;
}
