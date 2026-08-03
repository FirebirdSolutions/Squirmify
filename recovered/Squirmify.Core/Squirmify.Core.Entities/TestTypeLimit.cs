namespace Squirmify.Core.Entities;

public class TestTypeLimit
{
	public int Id { get; set; }

	public int ConfigId { get; set; }

	public string TestType { get; set; } = string.Empty;

	public string Category { get; set; } = string.Empty;

	public int MaxTests { get; set; }

	public double? Temperature { get; set; }

	public double? TopP { get; set; }

	public int? MaxTokens { get; set; }

	public double? PassThreshold { get; set; }

	public double? MinScore { get; set; }
}
