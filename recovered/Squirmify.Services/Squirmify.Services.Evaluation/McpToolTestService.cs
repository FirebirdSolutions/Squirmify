using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Squirmify.Core.Entities;

namespace Squirmify.Services.Evaluation;

public class McpToolTestService
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = false
	};

	public string BuildPrompt(McpToolTest test, string? dynamicSchema = null)
	{
		string value = dynamicSchema ?? test.ToolSchema ?? "No schema provided";
		return $"You have access to the following MCP tool:\r\n\r\nTool: {test.ToolName}\r\nSchema:\r\n{value}\r\n\r\nTask: {test.ScenarioPrompt}\r\n\r\nRespond with ONLY the JSON tool call in this exact format:\r\n{{\"cmd\": \"<command_name>\", \"params\": {{...}}}}\r\n\r\nDo not include any explanation, markdown code blocks, or text before/after the JSON.\r\nOutput only valid JSON.";
	}

	public string GetSystemPrompt()
	{
		return "You are being tested on your ability to correctly call MCP tools.\r\nWhen given a tool schema and a task, respond with ONLY the JSON tool call.\r\nDo not include explanations, markdown, or any other text.\r\nFormat: {\"cmd\": \"<command>\", \"params\": {...}}";
	}

	public ToolCallParseResult ParseToolCall(string modelResponse)
	{
		if (string.IsNullOrWhiteSpace(modelResponse))
		{
			return new ToolCallParseResult
			{
				Success = false,
				Error = "Empty response"
			};
		}
		string text = modelResponse.Trim();
		try
		{
			JsonDocument jsonDocument = JsonDocument.Parse(text);
			return new ToolCallParseResult
			{
				Success = true,
				ToolCall = jsonDocument.RootElement.Clone(),
				RawJson = text
			};
		}
		catch
		{
		}
		Match match = Regex.Match(text, "```(?:json)?\\s*([\\s\\S]*?)\\s*```", RegexOptions.IgnoreCase);
		if (match.Success)
		{
			string text2 = match.Groups[1].Value.Trim();
			try
			{
				JsonDocument jsonDocument2 = JsonDocument.Parse(text2);
				return new ToolCallParseResult
				{
					Success = true,
					ToolCall = jsonDocument2.RootElement.Clone(),
					RawJson = text2
				};
			}
			catch
			{
			}
		}
		Match match2 = Regex.Match(text, "\\{[^{}]*(?:\\{[^{}]*\\}[^{}]*)*\\}", RegexOptions.Singleline);
		if (match2.Success)
		{
			try
			{
				JsonDocument jsonDocument3 = JsonDocument.Parse(match2.Value);
				return new ToolCallParseResult
				{
					Success = true,
					ToolCall = jsonDocument3.RootElement.Clone(),
					RawJson = match2.Value
				};
			}
			catch
			{
			}
		}
		Match match3 = Regex.Match(text, "\\{.*\"cmd\".*\\}", RegexOptions.Singleline);
		if (match3.Success)
		{
			try
			{
				JsonDocument jsonDocument4 = JsonDocument.Parse(match3.Value);
				return new ToolCallParseResult
				{
					Success = true,
					ToolCall = jsonDocument4.RootElement.Clone(),
					RawJson = match3.Value
				};
			}
			catch
			{
			}
		}
		return new ToolCallParseResult
		{
			Success = false,
			Error = "Could not extract valid JSON from response"
		};
	}

	public ToolCallValidation ValidateToolCall(JsonElement toolCall, McpToolTest test)
	{
		ToolCallValidation toolCallValidation = new ToolCallValidation();
		if (!toolCall.TryGetProperty("cmd", out var value))
		{
			toolCallValidation.CorrectCommand = false;
			toolCallValidation.Errors.Add("Missing 'cmd' field in tool call");
		}
		else
		{
			string text = value.GetString() ?? "";
			toolCallValidation.CorrectCommand = text.Equals(test.Command, StringComparison.OrdinalIgnoreCase);
			if (!toolCallValidation.CorrectCommand)
			{
				toolCallValidation.Errors.Add($"Expected command '{test.Command}', got '{text}'");
			}
		}
		if (toolCall.TryGetProperty("params", out var value2))
		{
			toolCallValidation.HasParams = true;
			if (!string.IsNullOrEmpty(test.ExpectedParams))
			{
				try
				{
					JsonDocument jsonDocument = JsonDocument.Parse(test.ExpectedParams);
					toolCallValidation.ParamsValid = ValidateParamsStructure(value2, jsonDocument.RootElement, toolCallValidation.Errors);
				}
				catch
				{
					toolCallValidation.ParamsValid = true;
				}
			}
			else
			{
				toolCallValidation.ParamsValid = true;
			}
		}
		else
		{
			toolCallValidation.HasParams = false;
			if (!string.IsNullOrEmpty(test.ExpectedParams))
			{
				toolCallValidation.ParamsValid = false;
				toolCallValidation.Errors.Add("Missing 'params' field but parameters were expected");
			}
			else
			{
				toolCallValidation.ParamsValid = true;
			}
		}
		return toolCallValidation;
	}

	private bool ValidateParamsStructure(JsonElement actual, JsonElement expected, List<string> errors)
	{
		if (expected.ValueKind != JsonValueKind.Object)
		{
			return true;
		}
		foreach (JsonProperty item in expected.EnumerateObject())
		{
			string name = item.Name;
			string text = item.Value.GetString() ?? "string";
			bool flag = text.EndsWith("?");
			string text2 = (flag ? text.TrimEnd('?') : text);
			if (!actual.TryGetProperty(name, out var value))
			{
				if (!flag)
				{
					errors.Add("Missing required parameter: " + name);
					return false;
				}
				continue;
			}
			string text3 = text2.ToLower();
			if (1 == 0)
			{
			}
			bool flag2;
			switch (text3)
			{
			case "string":
				flag2 = value.ValueKind == JsonValueKind.String;
				break;
			case "number":
				flag2 = value.ValueKind == JsonValueKind.Number;
				break;
			case "boolean":
			case "bool":
				flag2 = value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False;
				break;
			case "object":
				flag2 = value.ValueKind == JsonValueKind.Object;
				break;
			case "array":
				flag2 = value.ValueKind == JsonValueKind.Array;
				break;
			default:
				flag2 = true;
				break;
			}
			if (1 == 0)
			{
			}
			if (flag2)
			{
				continue;
			}
			errors.Add($"Parameter '{name}' has wrong type: expected {text2}, got {value.ValueKind}");
			return false;
		}
		return true;
	}

	public ResponseValidation ValidateToolResponse(string response, McpToolTest test)
	{
		ResponseValidation responseValidation = new ResponseValidation();
		if (string.IsNullOrWhiteSpace(response))
		{
			responseValidation.Valid = false;
			responseValidation.Reason = "Empty response from tool";
			return responseValidation;
		}
		switch (test.ResponseValidationType.ToLower())
		{
		case "success":
			responseValidation.Valid = !response.Contains("\"error\"") || response.Contains("\"ok\":true") || response.Contains("\"ok\": true");
			responseValidation.Reason = (responseValidation.Valid ? "Response indicates success" : "Response contains error");
			break;
		case "contains":
			if (!string.IsNullOrEmpty(test.ExpectedResponsePatterns))
			{
				try
				{
					List<string> list = JsonSerializer.Deserialize<List<string>>(test.ExpectedResponsePatterns);
					if (list != null)
					{
						List<string> list2 = list.Where((string p) => !response.Contains(p, StringComparison.OrdinalIgnoreCase)).ToList();
						responseValidation.Valid = list2.Count == 0;
						responseValidation.Reason = (responseValidation.Valid ? "All expected patterns found" : ("Missing patterns: " + string.Join(", ", list2)));
					}
					else
					{
						responseValidation.Valid = true;
						responseValidation.Reason = "No patterns to check";
					}
				}
				catch
				{
					responseValidation.Valid = true;
					responseValidation.Reason = "Could not parse expected patterns";
				}
			}
			else
			{
				responseValidation.Valid = true;
				responseValidation.Reason = "No patterns specified";
			}
			break;
		case "json_path":
			try
			{
				JsonDocument jsonDocument = JsonDocument.Parse(response);
				if (jsonDocument.RootElement.TryGetProperty("ok", out var value))
				{
					responseValidation.Valid = value.GetBoolean();
					responseValidation.Reason = (responseValidation.Valid ? "ok=true" : "ok=false");
				}
				else
				{
					responseValidation.Valid = true;
					responseValidation.Reason = "Valid JSON response";
				}
			}
			catch
			{
				responseValidation.Valid = false;
				responseValidation.Reason = "Invalid JSON response";
			}
			break;
		default:
			responseValidation.Valid = true;
			responseValidation.Reason = "No specific validation";
			break;
		}
		return responseValidation;
	}
}
