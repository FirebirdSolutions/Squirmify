using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using ModelEvaluator.Models;
using Spectre.Console;

namespace ModelEvaluator.Services;

public class ModelService
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, int> _modelErrors = new();

    public ModelService()
    {
        _http = new HttpClient { Timeout = Config.RequestTimeout, };
        // Add the Authorization header with the Bearer token
        if (Config.UseAuth)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Config.BaseAuthToken);
        }
    }

    /// <summary>
    /// Load models - either from targetModels config or from LM Studio /models endpoint
    /// </summary>
    public async Task<List<string>> LoadModelsAsync(IEnumerable<string>? exclude = null)
    {
        var exclusions = new HashSet<string>(exclude ?? Enumerable.Empty<string>(),
                                             StringComparer.OrdinalIgnoreCase);

        // If targetModels is specified in config, use those instead of querying /models
        if (Config.TargetModels.Any())
        {
            var filtered = Config.TargetModels.Where(m => !exclusions.Contains(m)).ToList();
            AnsiConsole.MarkupLine($"[green]✓ Using {filtered.Count} target model(s) from config[/]");
            if (filtered.Any())
            {
                var shortList = string.Join(", ", filtered.Take(5));
                var more = filtered.Count > 5 ? " [dim]…[/]" : "";
                AnsiConsole.MarkupLine($"[dim]{shortList}{more}[/]");
            }
            return filtered;
        }

        return await AnsiConsole.Status()
            .StartAsync("[yellow]Querying LM Studio for loaded models…[/]", async ctx =>
            {
                try
                {
                    var json = await _http.GetStringAsync($"{Config.BaseUrl}/models");
                    var resp = JsonSerializer.Deserialize<ModelsResponse>(json);
                    var models = resp?.Data?.Select(m => m.Id).ToList() ?? new List<string>();

                    if (models.Any())
                    {
                        // filter excluded models
                        var filtered = models.Where(m => !exclusions.Contains(m)).ToList();

                        var shortList = string.Join(", ", filtered.Take(5));
                        var more = filtered.Count > 5 ? " [dim]…[/]" : "";

                        AnsiConsole.MarkupLine($"[green]✓ Found {filtered.Count} model(s)[/]");
                        if (filtered.Any())
                            AnsiConsole.MarkupLine($"[dim]{shortList}{more}[/]");

                        return filtered;
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]✗ No models loaded in LM Studio[/]");
                        return new List<string>();
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Failed to contact LM Studio: {ex.Message}[/]");
                    return new List<string>();
                }
            });
    }


    /// <summary>
    /// Warm up a model with a simple ping request
    /// </summary>
    public async Task<bool> WarmUpModelAsync(string modelName)
    {
        var request = new ChatRequest
        {
            Model = modelName,
            Messages = new List<Message> { new("user", "Hi") },
            Temperature = 0.1,
            MaxTokens = 5
        };
        try
        {


            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"{Config.BaseUrl}/chat/completions", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine($"Error: {ex.Message}");
            AnsiConsole.WriteLine($"Error: {request.Messages[0].Content}");
            return false;
        }
    }

    /// <summary>
    /// Send a completion request to the model
    /// </summary>
    public async Task<(string response, PerfMetrics perf)?> CompletionAsync(
        string modelName,
        string systemPrompt,
        string userPrompt,
        double temperature,
        double topP,
        int maxTokens)
    {
        // Check if model has exceeded error threshold
        if (_modelErrors.GetValueOrDefault(modelName, 0) >= Config.MaxModelErrors)
        {
            return null;
        }

        try
        {
            var sw = Stopwatch.StartNew();

            var request = new ChatRequest
            {
                Model = modelName,
                Messages = new List<Message>
                {
                    new("system", systemPrompt),
                    new("user", userPrompt)
                },
                Temperature = temperature,
                TopP = topP,
                MaxTokens = maxTokens,
                Stream = false
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var httpResponse = await _http.PostAsync($"{Config.BaseUrl}/chat/completions", content);
            httpResponse.EnsureSuccessStatusCode();

            var responseJson = await httpResponse.Content.ReadAsStringAsync();
            var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseJson);

            sw.Stop();

            if (chatResponse?.Choices == null || chatResponse.Choices.Length == 0)
            {
                RecordError(modelName);
                return null;
            }

            var responseText = chatResponse.Choices[0].Message.Content;
            var usage = chatResponse.Usage;

            var perf = new PerfMetrics
            {
                total_ms = sw.Elapsed.TotalMilliseconds,
                tokens_per_sec = usage?.CompletionTokens != null && sw.Elapsed.TotalSeconds > 0
                    ? usage.CompletionTokens / sw.Elapsed.TotalSeconds
                    : null,
                prompt_tokens = usage?.PromptTokens,
                completion_tokens = usage?.CompletionTokens
            };

            return (responseText, perf);
        }
        catch (Exception ex)
        {
            RecordError(modelName);
            AnsiConsole.MarkupLine($"[red]Error with {modelName}: {ex.Message}[/]");
            return null;
        }
    }

    private void RecordError(string modelName)
    {
        _modelErrors[modelName] = _modelErrors.GetValueOrDefault(modelName, 0) + 1;

        if (_modelErrors[modelName] >= Config.MaxModelErrors)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ Model {modelName} has been flagged (too many errors)[/]");
        }
    }

    public bool IsModelFlagged(string modelName)
    {
        return _modelErrors.GetValueOrDefault(modelName, 0) >= Config.MaxModelErrors;
    }
}


