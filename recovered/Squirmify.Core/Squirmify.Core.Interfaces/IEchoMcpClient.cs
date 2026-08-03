using System.Collections.Generic;
using System.Threading.Tasks;
using Squirmify.Core.DTOs;

namespace Squirmify.Core.Interfaces;

public interface IEchoMcpClient
{
	void Configure(string? baseUrl, string? token);

	void ConfigureTransport(string transportType, string? serverUrl = null, string? command = null, string? args = null);

	Task<McpExecutionResult> ExecuteToolAsync(string toolName, string command, object? parameters);

	Task<string?> GetToolSchemaAsync(string toolName);

	Task<IEnumerable<McpToolInfo>> ListToolsAsync();

	Task<bool> CheckHealthAsync();
}
