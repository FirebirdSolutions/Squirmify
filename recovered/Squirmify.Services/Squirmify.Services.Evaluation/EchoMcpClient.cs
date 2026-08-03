using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Squirmify.Core.DTOs;
using Squirmify.Core.Interfaces;

namespace Squirmify.Services.Evaluation;

public class EchoMcpClient : IEchoMcpClient
{
	private readonly IHttpClientFactory _httpClientFactory;

	private string _baseUrl;

	private string? _token;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public EchoMcpClient(IHttpClientFactory httpClientFactory, IConfiguration config)
	{
		_httpClientFactory = httpClientFactory;
		_baseUrl = config["EchoMcp:BaseUrl"] ?? "http://localhost:3005/api";
		_token = config["EchoMcp:Token"];
	}

	public void Configure(string? baseUrl, string? token)
	{
		if (!string.IsNullOrEmpty(baseUrl))
		{
			_baseUrl = baseUrl;
		}
		_token = token;
	}

	public void ConfigureTransport(string transportType, string? serverUrl = null, string? command = null, string? args = null)
	{
		Console.WriteLine("[EchoMcpClient] ConfigureTransport called with " + transportType + " (not supported by REST client)");
	}

	private HttpClient CreateClient()
	{
		HttpClient httpClient = _httpClientFactory.CreateClient();
		httpClient.Timeout = TimeSpan.FromSeconds(30L);
		if (!string.IsNullOrEmpty(_token))
		{
			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
		}
		return httpClient;
	}

	public async Task<McpExecutionResult> ExecuteToolAsync(string toolName, string command, object? parameters)
	{
		Stopwatch sw = Stopwatch.StartNew();
		try
		{
			using HttpClient client = CreateClient();
			McpToolCallRequest request = new McpToolCallRequest
			{
				Command = command,
				Parameters = parameters
			};
			string json = JsonSerializer.Serialize(request, JsonOptions);
			StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
			string url = _baseUrl + "/" + toolName;
			Console.WriteLine("[EchoMcpClient] POST " + url);
			Console.WriteLine("[EchoMcpClient] Body: " + json);
			HttpResponseMessage response = await client.PostAsync(url, content);
			string responseBody = await response.Content.ReadAsStringAsync();
			sw.Stop();
			Console.WriteLine($"[EchoMcpClient] Response ({response.StatusCode}): {responseBody.Substring(0, Math.Min(500, responseBody.Length))}");
			if (!response.IsSuccessStatusCode)
			{
				return new McpExecutionResult
				{
					Success = false,
					Error = $"HTTP {(int)response.StatusCode}: {responseBody}",
					ExecutionMs = sw.ElapsedMilliseconds
				};
			}
			try
			{
				McpToolCallResponse mcpResponse = JsonSerializer.Deserialize<McpToolCallResponse>(responseBody, JsonOptions);
				return new McpExecutionResult
				{
					Success = (mcpResponse?.Ok ?? true),
					Response = responseBody,
					Error = mcpResponse?.Error,
					ExecutionMs = sw.ElapsedMilliseconds
				};
			}
			catch
			{
				return new McpExecutionResult
				{
					Success = true,
					Response = responseBody,
					ExecutionMs = sw.ElapsedMilliseconds
				};
			}
		}
		catch (HttpRequestException ex)
		{
			HttpRequestException ex2 = ex;
			sw.Stop();
			Console.WriteLine("[EchoMcpClient] HTTP ERROR: " + ex2.Message);
			return new McpExecutionResult
			{
				Success = false,
				Error = "Connection failed: " + ex2.Message,
				ExecutionMs = sw.ElapsedMilliseconds
			};
		}
		catch (TaskCanceledException)
		{
			sw.Stop();
			Console.WriteLine("[EchoMcpClient] TIMEOUT");
			return new McpExecutionResult
			{
				Success = false,
				Error = "Request timed out",
				ExecutionMs = sw.ElapsedMilliseconds
			};
		}
		catch (Exception ex4)
		{
			Exception ex5 = ex4;
			sw.Stop();
			Console.WriteLine("[EchoMcpClient] ERROR: " + ex5.GetType().Name + " - " + ex5.Message);
			return new McpExecutionResult
			{
				Success = false,
				Error = ex5.GetType().Name + ": " + ex5.Message,
				ExecutionMs = sw.ElapsedMilliseconds
			};
		}
	}

	public async Task<string?> GetToolSchemaAsync(string toolName)
	{
		try
		{
			McpExecutionResult result = await ExecuteToolAsync("discovery_execute", "get_schema", new
			{
				toolId = toolName
			});
			if (result.Success && result.Response != null)
			{
				try
				{
					using JsonDocument doc = JsonDocument.Parse(result.Response);
					if (doc.RootElement.TryGetProperty("data", out var data))
					{
						return data.GetRawText();
					}
					return result.Response;
				}
				catch
				{
					return result.Response;
				}
			}
			return null;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Console.WriteLine("[EchoMcpClient] Failed to get schema for " + toolName + ": " + ex2.Message);
			return null;
		}
	}

	public async Task<IEnumerable<McpToolInfo>> ListToolsAsync()
	{
		try
		{
			McpExecutionResult result = await ExecuteToolAsync("discovery_execute", "list_tools", null);
			if (result.Success && result.Response != null)
			{
				using (JsonDocument doc = JsonDocument.Parse(result.Response))
				{
					List<McpToolInfo> tools = new List<McpToolInfo>();
					if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("tools", out var toolsArray))
					{
						foreach (JsonElement tool in toolsArray.EnumerateArray())
						{
							tools.Add(new McpToolInfo
							{
								Name = (tool.GetProperty("name").GetString() ?? ""),
								Description = (tool.TryGetProperty("description", out var desc) ? (desc.GetString() ?? "") : ""),
								Schema = tool.GetRawText()
							});
							desc = default(JsonElement);
						}
					}
					return tools;
				}
			}
			return Array.Empty<McpToolInfo>();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Console.WriteLine("[EchoMcpClient] Failed to list tools: " + ex2.Message);
			return Array.Empty<McpToolInfo>();
		}
	}

	public async Task<bool> CheckHealthAsync()
	{
		try
		{
			McpExecutionResult result = await ExecuteToolAsync("help_execute", "", null);
			return result.Success || result.Response != null;
		}
		catch
		{
			return false;
		}
	}
}
