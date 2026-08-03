using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Squirmify.Core.DTOs;
using Squirmify.Core.Interfaces;

namespace Squirmify.Services.Evaluation;

public class McpSdkClient : IEchoMcpClient, IAsyncDisposable
{
	private McpClient? _client;

	private IClientTransport? _transport;

	private string? _serverUrl;

	private string? _command;

	private string[]? _args;

	private string _transportType = "sse";

	public void Configure(string? baseUrl, string? token)
	{
		_serverUrl = baseUrl;
	}

	public void ConfigureTransport(string transportType, string? serverUrl = null, string? command = null, string? args = null)
	{
		_transportType = transportType.ToLowerInvariant();
		_serverUrl = serverUrl;
		_command = command;
		_args = args?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
	}

	private async Task EnsureConnectedAsync()
	{
		if (_client != null)
		{
			return;
		}
		switch (_transportType)
		{
		case "stdio":
			if (string.IsNullOrEmpty(_command))
			{
				throw new InvalidOperationException("Command is required for stdio transport");
			}
			_transport = new StdioClientTransport(new StdioClientTransportOptions
			{
				Name = "Squirmify-MCP-Client",
				Command = _command,
				Arguments = (_args ?? Array.Empty<string>())
			});
			break;
		case "sse":
		case "http":
			if (string.IsNullOrEmpty(_serverUrl))
			{
				throw new InvalidOperationException("Server URL is required for HTTP transport");
			}
			_transport = new HttpClientTransport(new HttpClientTransportOptions
			{
				Endpoint = new Uri(_serverUrl)
			});
			break;
		default:
			throw new ArgumentException("Unknown transport type: " + _transportType);
		}
		_client = await McpClient.CreateAsync(_transport);
		Console.WriteLine("[McpSdkClient] Connected via " + _transportType + " transport");
	}

	public async Task<McpExecutionResult> ExecuteToolAsync(string toolName, string command, object? parameters)
	{
		Stopwatch sw = Stopwatch.StartNew();
		try
		{
			await EnsureConnectedAsync();
			Dictionary<string, object?> paramDict = new Dictionary<string, object> { ["cmd"] = command };
			if (parameters != null)
			{
				Dictionary<string, object?> dict = parameters as Dictionary<string, object>;
				if (dict != null)
				{
					paramDict["params"] = dict;
				}
				else
				{
					string json = JsonSerializer.Serialize(parameters);
					Dictionary<string, object?> deserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
					paramDict["params"] = deserialized;
				}
			}
			Console.WriteLine("[McpSdkClient] Calling tool: " + toolName + " with command: " + command);
			CallToolResult result = await _client.CallToolAsync(toolName, paramDict, null, null, CancellationToken.None);
			sw.Stop();
			string responseText = string.Join("\n", from t in result.Content.OfType<TextContentBlock>()
				select t.Text);
			Console.WriteLine("[McpSdkClient] Response: " + responseText.Substring(0, Math.Min(200, responseText.Length)));
			return new McpExecutionResult
			{
				Success = (result.IsError != true),
				Response = responseText,
				ExecutionMs = sw.ElapsedMilliseconds
			};
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			sw.Stop();
			Console.WriteLine("[McpSdkClient] Error: " + ex2.Message);
			return new McpExecutionResult
			{
				Success = false,
				Error = ex2.Message,
				ExecutionMs = sw.ElapsedMilliseconds
			};
		}
	}

	public async Task<string?> GetToolSchemaAsync(string toolName)
	{
		try
		{
			await EnsureConnectedAsync();
			McpClientTool tool = (await _client.ListToolsAsync()).FirstOrDefault((McpClientTool t) => t.Name == toolName);
			if (tool == null)
			{
				return null;
			}
			return JsonSerializer.Serialize(tool, new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			});
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Console.WriteLine("[McpSdkClient] Failed to get schema for " + toolName + ": " + ex2.Message);
			return null;
		}
	}

	public async Task<IEnumerable<McpToolInfo>> ListToolsAsync()
	{
		try
		{
			await EnsureConnectedAsync();
			return (await _client.ListToolsAsync()).Select((McpClientTool t) => new McpToolInfo
			{
				Name = (t.Name ?? ""),
				Description = (t.Description ?? ""),
				Schema = JsonSerializer.Serialize(t, new JsonSerializerOptions
				{
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase
				})
			});
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Console.WriteLine("[McpSdkClient] Failed to list tools: " + ex2.Message);
			return Array.Empty<McpToolInfo>();
		}
	}

	public async Task<bool> CheckHealthAsync()
	{
		try
		{
			await EnsureConnectedAsync();
			return (await _client.ListToolsAsync()).Any();
		}
		catch
		{
			return false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_client != null)
		{
			await _client.DisposeAsync();
			_client = null;
		}
		IClientTransport transport = _transport;
		if (transport is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync();
		}
		else
		{
			transport = _transport;
			if (transport is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}
		_transport = null;
	}
}
