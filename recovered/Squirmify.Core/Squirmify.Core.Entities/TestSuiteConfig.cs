using System;

namespace Squirmify.Core.Entities;

public class TestSuiteConfig
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	public bool RunPromptTests { get; set; } = true;

	public bool RunContextWindowTests { get; set; }

	public bool RunConversationTests { get; set; } = true;

	public bool RunQualificationTests { get; set; } = true;

	public int MaxInstructionTests { get; set; } = 10;

	public int MaxReasoningTests { get; set; } = 10;

	public int MaxConversationTests { get; set; } = 10;

	public double HighQualityThreshold { get; set; } = 7.5;

	public double InstructionPassThreshold { get; set; } = 0.8;

	public int TopJudgeCount { get; set; } = 2;

	public bool RunMcpToolTests { get; set; } = false;

	public int MaxMcpToolTests { get; set; } = 10;

	public string? EchoMcpBaseUrl { get; set; }

	public string? EchoMcpToken { get; set; }

	public bool FetchSchemasFromEchoMcp { get; set; } = true;

	public string McpTransportType { get; set; } = "sse";

	public string? McpServerUrl { get; set; }

	public string? McpServerCommand { get; set; }

	public string? McpServerArgs { get; set; }

	public string ContextWindowLevel { get; set; } = "shallow";

	public string ContextWindowTestType { get; set; } = "Multi-Needle Recall";

	public int ContextWindowTargetTokens { get; set; } = 32000;

	public int ContextWindowProbeCount { get; set; } = 10;

	public int ContextWindowCheckpoints { get; set; } = 4;

	public int ContextWindowMaxTests { get; set; } = 5;

	public string? ContextWindowTestIds { get; set; }

	public int TargetSeedCount { get; set; } = 50;

	public bool OverwriteSeeds { get; set; } = true;

	public double GlobalTemperature { get; set; } = 0.5;

	public double GlobalTopP { get; set; } = 0.9;

	public int GlobalMaxTokens { get; set; } = 512;

	public int MaxParallelRequests { get; set; } = 1;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? UpdatedAt { get; set; }
}
