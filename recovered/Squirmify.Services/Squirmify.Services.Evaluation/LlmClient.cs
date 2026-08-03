using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Squirmify.Core.DTOs;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;

namespace Squirmify.Services.Evaluation;

public class LlmClient : ILlmClient
{
	private readonly IHttpClientFactory _httpClientFactory;

	private readonly Dictionary<string, int> _modelErrors = new Dictionary<string, int>();

	private const int MaxModelErrors = 3;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	public LlmClient(IHttpClientFactory httpClientFactory)
	{
		_httpClientFactory = httpClientFactory;
	}

	private HttpClient CreateClient(Provider provider)
	{
		HttpClient httpClient = _httpClientFactory.CreateClient();
		httpClient.Timeout = TimeSpan.FromMinutes(provider.TimeoutMinutes);
		if (provider.UseAuth && !string.IsNullOrEmpty(provider.AuthToken))
		{
			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.AuthToken);
		}
		return httpClient;
	}

	public async Task<IEnumerable<string>> LoadModelsFromServerAsync(Provider provider)
	{
		using HttpClient client = CreateClient(provider);
		string url = provider.BaseUrl + "/models";
		Console.WriteLine("[LlmClient] GET " + url);
		try
		{
			string json = await client.GetStringAsync(url);
			Console.WriteLine($"[LlmClient] Response received ({json.Length} chars)");
			List<string> models = JsonSerializer.Deserialize<ModelsResponse>(json, JsonOptions)?.Data?.Select((ModelInfo m) => m.Id).ToList() ?? new List<string>();
			Console.WriteLine($"[LlmClient] Parsed {models.Count} model(s): {string.Join(", ", models.Take(5))}{((models.Count > 5) ? "..." : "")}");
			return models;
		}
		catch (HttpRequestException ex)
		{
			HttpRequestException ex2 = ex;
			Console.WriteLine("[LlmClient] HTTP ERROR: " + ex2.Message);
			throw new InvalidOperationException($"Failed to connect to {provider.Name} at {provider.BaseUrl}: {ex2.Message}", ex2);
		}
		catch (TaskCanceledException ex3)
		{
			TaskCanceledException ex4 = ex3;
			Console.WriteLine("[LlmClient] TIMEOUT: " + ex4.Message);
			throw new InvalidOperationException($"Request to {provider.Name} timed out after {provider.TimeoutMinutes} minutes", ex4);
		}
		catch (Exception ex5)
		{
			Exception ex6 = ex5;
			Console.WriteLine("[LlmClient] ERROR: " + ex6.GetType().Name + " - " + ex6.Message);
			throw new InvalidOperationException("Error loading models from " + provider.Name + ": " + ex6.Message, ex6);
		}
	}

	public async Task<bool> WarmUpModelAsync(Provider provider, string modelIdentifier)
	{
		using HttpClient client = CreateClient(provider);
		ChatRequest request = new ChatRequest
		{
			Model = modelIdentifier,
			Messages = new List<Message>
			{
				new Message("user", "Hi")
			},
			Temperature = 0.1,
			MaxTokens = 5
		};
		try
		{
			string json = JsonSerializer.Serialize(request);
			return (await client.PostAsync(content: new StringContent(json, Encoding.UTF8, "application/json"), requestUri: provider.BaseUrl + "/chat/completions")).IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}

	public async Task<CompletionResult?> CompletionAsync(Provider provider, string modelIdentifier, string systemPrompt, string userPrompt, double temperature, double topP, int maxTokens)
	{
		if (_modelErrors.GetValueOrDefault(modelIdentifier, 0) >= 3)
		{
			return null;
		}
		using HttpClient client = CreateClient(provider);
		try
		{
			Stopwatch sw = Stopwatch.StartNew();
			ChatRequest request = new ChatRequest
			{
				Model = modelIdentifier,
				Messages = new List<Message>
				{
					new Message("system", systemPrompt),
					new Message("user", userPrompt)
				},
				Temperature = temperature,
				TopP = topP,
				MaxTokens = maxTokens,
				Stream = false
			};
			string json = JsonSerializer.Serialize(request);
			HttpResponseMessage httpResponse = await client.PostAsync(content: new StringContent(json, Encoding.UTF8, "application/json"), requestUri: provider.BaseUrl + "/chat/completions");
			httpResponse.EnsureSuccessStatusCode();
			ChatResponse chatResponse = JsonSerializer.Deserialize<ChatResponse>(await httpResponse.Content.ReadAsStringAsync(), JsonOptions);
			sw.Stop();
			if (chatResponse?.Choices == null || chatResponse.Choices.Length == 0)
			{
				RecordError(modelIdentifier);
				return null;
			}
			Message message = chatResponse.Choices[0].Message;
			string responseText = message.Content;
			if (string.IsNullOrWhiteSpace(responseText) && !string.IsNullOrWhiteSpace(message.ReasoningContent))
			{
				responseText = message.ReasoningContent;
			}
			if (responseText?.Contains("<think>") ?? false)
			{
				Match thinkMatch = Regex.Match(responseText, "</think>\\s*(.*)", RegexOptions.Singleline);
				if (thinkMatch.Success && !string.IsNullOrWhiteSpace(thinkMatch.Groups[1].Value))
				{
					responseText = thinkMatch.Groups[1].Value.Trim();
				}
				else
				{
					Match insideThink = Regex.Match(responseText, "<think>(.*?)</think>", RegexOptions.Singleline);
					if (insideThink.Success)
					{
						responseText = insideThink.Groups[1].Value.Trim();
					}
				}
			}
			UsageInfo usage = chatResponse.Usage;
			CompletionResult obj = new CompletionResult
			{
				Response = responseText
			};
			PerfMetrics obj2 = new PerfMetrics
			{
				TotalMs = sw.Elapsed.TotalMilliseconds
			};
			obj2.TokensPerSec = ((usage != null && usage.CompletionTokens > 0 && sw.Elapsed.TotalSeconds > 0.0) ? new double?((double)usage.CompletionTokens / sw.Elapsed.TotalSeconds) : ((double?)null));
			obj2.PromptTokens = usage?.PromptTokens;
			obj2.CompletionTokens = usage?.CompletionTokens;
			obj.Perf = obj2;
			return obj;
		}
		catch
		{
			RecordError(modelIdentifier);
			return null;
		}
	}

	private void RecordError(string modelIdentifier)
	{
		_modelErrors[modelIdentifier] = _modelErrors.GetValueOrDefault(modelIdentifier, 0) + 1;
	}

	public bool IsModelFlagged(string modelIdentifier)
	{
		return _modelErrors.GetValueOrDefault(modelIdentifier, 0) >= 3;
	}
}
