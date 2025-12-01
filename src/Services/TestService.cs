using System.Text.Json;
using System.Text.RegularExpressions;
using ModelEvaluator.Models;
using Spectre.Console;

namespace ModelEvaluator.Services;

public class TestService
{
    private readonly ModelService _modelService;

    private static readonly List<InstructionTest> InstructionTests = new()
    {
        // === BASIC COMPLIANCE ===
        new()
        {
            Prompt = "Output exactly these three words in this order, separated by single spaces: Red Blue Green. Do not add punctuation, quotes, or anything else.",
            ExpectedResult = "Red Blue Green",
            ValidationType = "words",  // Changed: allows case/order flexibility
            StrictOrder = true
        },
        new()
        {
            Prompt = "Calculate: 7 + 8. Respond with the single integer result ONLY. No text, no symbols, no explanation.",
            ExpectedResult = "15",
            ValidationType = "numeric"
        },
        
        // === JSON OUTPUT ===
        new()
        {
            Prompt = "Return a JSON object with one field 'status' set to 'ok'. Output ONLY valid JSON, no markdown code blocks, no explanation, no text before or after.",
            ExpectedResult = "{\"status\":\"ok\"}",
            ValidationType = "json"
        },
        new()
        {
            Prompt = "Return a JSON array containing exactly the numbers 1,2,3 as integers, *NOT* strings. Output ONLY the JSON array, no markdown, no text, nothing else.",
            ExpectedResult = "[1,2,3]",
            ValidationType = "json"
        },
        new()
        {
            Prompt = "Create a JSON object with two fields: 'name' set to 'Alice' and 'age' set to the integer 25. Output ONLY the JSON, no markdown, no explanation.",
            ExpectedResult = "{\"name\":\"Alice\",\"age\":25}",
            ValidationType = "json"
        },
        
        // === TOOL CALLING FORMAT ===
        new()
        {
            Prompt = "You have a tool called 'get_weather' that takes a parameter 'city' (string). Call this tool for London. Return ONLY this JSON, nothing else: {\"tool\":\"get_weather\",\"parameters\":{\"city\":\"London\"}}",
            ExpectedResult = "{\"tool\":\"get_weather\",\"parameters\":{\"city\":\"London\"}}",
            ValidationType = "json"
        },
        new()
        {
            Prompt = "Call the function 'list_projects' with no parameters. Return ONLY the JSON tool call in this format: {\"tool\":\"list_projects\",\"parameters\":{}}",
            ExpectedResult = "{\"tool\":\"list_projects\",\"parameters\":{}}",
            ValidationType = "json"
        },
        new()
        {
            Prompt = "You have a function 'calculate' that takes two integer parameters: 'a' and 'b'. Call it with a=10 and b=20. Return ONLY: {\"tool\":\"calculate\",\"parameters\":{\"a\":10,\"b\":20}}",
            ExpectedResult = "{\"tool\":\"calculate\",\"parameters\":{\"a\":10,\"b\":20}}",
            ValidationType = "json"
        },
        
        // === FORMAT CONSTRAINTS ===
        new()
        {
            Prompt = "List three colors, one per line, no numbers, no bullets, no punctuation. Just the color names.",
            ExpectedResult = "Red\nBlue\nGreen",
            ValidationType = "lines",  // Changed: validates line-by-line content
            StrictOrder = false
        },
        new()
        {
            Prompt = "Output the word 'SUCCESS' in all caps. Nothing else. No punctuation, no explanation.",
            ExpectedResult = "SUCCESS",
            ValidationType = "exact"
        },
        
        // === SIMPLE CALCULATIONS ===
        new()
        {
            Prompt = "What is 12 * 3? Respond with only the number.",
            ExpectedResult = "36",
            ValidationType = "numeric"
        },
        new()
        {
            Prompt = "Calculate 100 - 37. Output only the integer result.",
            ExpectedResult = "63",
            ValidationType = "numeric"
        },
        
        // === BOOLEAN OUTPUT ===
        new()
        {
            Prompt = "Is 10 greater than 5? Respond with ONLY 'true' or 'false' in lowercase.",
            ExpectedResult = "true",
            ValidationType = "boolean"
        },
        new()
        {
            Prompt = "Is 'cat' the same as 'dog'? Respond with ONLY 'true' or 'false' in lowercase.",
            ExpectedResult = "false",
            ValidationType = "boolean"
        }
    };

    public TestService(ModelService modelService)
    {
        _modelService = modelService;
    }

    public async Task<List<InstructionTestResult>> RunInstructionTestsAsync(List<string> models)
    {
        AnsiConsole.MarkupLine("\n[bold cyan]═══ Running Instruction Following Tests ═══[/]\n");

        var results = new List<InstructionTestResult>();

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Testing models[/]", maxValue: models.Count);

                foreach (var model in models)
                {
                    task.Description = $"[yellow]Testing {model}[/]";

                    var result = await RunTestsForModelAsync(model);
                    if (result != null)
                        results.Add(result);

                    var instructionFile = Path.Combine(Config.OutputDir, "instruction_results.json");
                    var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(instructionFile, json);

                    task.Increment(1);
                }
            });

        DisplayTestResults(results);

        return results;
    }

    private async Task<InstructionTestResult?> RunTestsForModelAsync(string modelName)
    {
        var warmupOk = await _modelService.WarmUpModelAsync(modelName);
        if (!warmupOk)
        {
            AnsiConsole.MarkupLine($"[red]✗ Failed to warm up {modelName}[/]");
            return null;
        }

        var result = new InstructionTestResult
        {
            ModelName = modelName,
            TotalTests = InstructionTests.Count
        };

        var perfMetrics = new List<PerfMetrics>();

        foreach (var test in InstructionTests)
        {
            var response = await _modelService.CompletionAsync(
                modelName,
                "You are an instruction-following test system. Output ONLY the exact requested content. Any deviation = failure. No preamble. No explanation. Raw output only.",
                test.Prompt,
                Config.InstructionTestTemperature,
                Config.InstructionTestTopP,
                300
            );

            if (response == null)
            {
                result.FailureDetails.Add($"API error on test: {Truncate(test.Prompt, 50)}...");
                continue;
            }

            var (responseText, perf) = response.Value;
            perfMetrics.Add(perf);

            var validation = ValidateResponse(responseText, test);

            if (validation.StrictPass)
            {
                result.PassedTests++;
                //AnsiConsole.MarkupLine($"[green]✓ PASS[/]: {Truncate(responseText, 60)}");
            }
            else if (validation.LenientPass)
            {
                result.PassedTests++; // Count lenient as pass
                result.LenientPasses++;
                //AnsiConsole.MarkupLine($"[yellow]~ LENIENT[/]: Expected '{Truncate(test.ExpectedResult, 40)}', Got '{Truncate(responseText, 40)}' ({validation.Reason})");
            }
            else
            {
                //AnsiConsole.MarkupLine($"[red]✗ FAIL[/]: Expected '{Truncate(test.ExpectedResult, 40)}', Got '{Truncate(responseText, 40)}'");
                result.FailureDetails.Add($"Expected: {test.ExpectedResult}, Got: {Truncate(responseText, 100)} - {validation.Reason}");
            }
        }

        if (perfMetrics.Any())
        {
            result.AvgTokensPerSec = perfMetrics
                .Where(p => p.tokens_per_sec.HasValue)
                .DefaultIfEmpty()
                .Average(p => p?.tokens_per_sec ?? 0);

            result.AvgLatencyMs = perfMetrics.Average(p => p.total_ms);
        }

        return result;
    }

    private ValidationResult ValidateResponse(string response, InstructionTest test)
    {
        // Clean up response - strip common model quirks
        response = CleanResponse(response);

        return test.ValidationType switch
        {
            "exact" => ValidateExact(response, test.ExpectedResult),
            "words" => ValidateWords(response, test.ExpectedResult, test.StrictOrder),
            "lines" => ValidateLines(response, test.ExpectedResult, test.StrictOrder),
            "json" => ValidateJson(response, test.ExpectedResult),
            "numeric" => ValidateNumeric(response, test.ExpectedResult),
            "boolean" => ValidateBoolean(response, test.ExpectedResult),
            _ => new ValidationResult { StrictPass = false, Reason = "Unknown validation type" }
        };
    }

    private string CleanResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return string.Empty;

        response = response.Trim();

        // Remove markdown code blocks
        response = Regex.Replace(response, @"^```(?:json)?\s*", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        response = Regex.Replace(response, @"\s*```$", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // Remove surrounding quotes (single or double)
        if ((response.StartsWith('"') && response.EndsWith('"')) ||
            (response.StartsWith('\'') && response.EndsWith('\'')))
        {
            response = response[1..^1];
        }

        // Remove trailing punctuation that wasn't requested
        response = response.TrimEnd('.', '!', ';');

        // Normalize whitespace
        response = Regex.Replace(response, @"[\u00A0\uFEFF]", " ");
        response = Regex.Replace(response, @"[ \t]+", " ");
        response = Regex.Replace(response, @"\r\n|\r", "\n");
        response = Regex.Replace(response, @"\n{2,}", "\n");

        return response.Trim();
    }

    private ValidationResult ValidateExact(string response, string expected)
    {
        var normResponse = Normalize(response);
        var normExpected = Normalize(expected);

        if (string.Equals(normResponse, normExpected, StringComparison.Ordinal))
            return new ValidationResult { StrictPass = true };

        if (string.Equals(normResponse, normExpected, StringComparison.OrdinalIgnoreCase))
            return new ValidationResult { LenientPass = true, Reason = "case difference" };

        return new ValidationResult { Reason = "content mismatch" };
    }

    private ValidationResult ValidateWords(string response, string expected, bool strictOrder)
    {
        var responseWords = ExtractWords(response);
        var expectedWords = ExtractWords(expected);

        // Check exact match first
        if (responseWords.SequenceEqual(expectedWords, StringComparer.OrdinalIgnoreCase))
            return new ValidationResult { StrictPass = true };

        // Check same words, case-insensitive
        if (responseWords.Select(w => w.ToLowerInvariant()).SequenceEqual(
            expectedWords.Select(w => w.ToLowerInvariant())))
            return new ValidationResult { StrictPass = true };

        // If order doesn't matter or we're being lenient, check set equality
        var responseSet = responseWords.Select(w => w.ToLowerInvariant()).ToHashSet();
        var expectedSet = expectedWords.Select(w => w.ToLowerInvariant()).ToHashSet();

        if (responseSet.SetEquals(expectedSet))
        {
            if (strictOrder)
                return new ValidationResult { LenientPass = true, Reason = "correct words, wrong order" };
            else
                return new ValidationResult { StrictPass = true };
        }

        // Check if all expected words are present (model added extra)
        if (expectedSet.IsSubsetOf(responseSet))
            return new ValidationResult { LenientPass = true, Reason = "extra words added" };

        return new ValidationResult { Reason = $"word mismatch: expected {expectedWords.Count}, got {responseWords.Count}" };
    }

    private ValidationResult ValidateLines(string response, string expected, bool strictOrder)
    {
        var responseLines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();
        var expectedLines = expected.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();

        // Exact match
        if (responseLines.SequenceEqual(expectedLines, StringComparer.OrdinalIgnoreCase))
            return new ValidationResult { StrictPass = true };

        // Same content, different order
        var responseSet = responseLines.Select(l => l.ToLowerInvariant()).ToHashSet();
        var expectedSet = expectedLines.Select(l => l.ToLowerInvariant()).ToHashSet();

        if (responseSet.SetEquals(expectedSet))
        {
            if (strictOrder)
                return new ValidationResult { LenientPass = true, Reason = "correct lines, wrong order" };
            else
                return new ValidationResult { StrictPass = true };
        }

        // Check for common formatting issues (bullets, numbers)
        var cleanedLines = responseLines
            .Select(l => Regex.Replace(l, @"^[\d\.\-\*\•\→]+\s*", "").Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        var cleanedSet = cleanedLines.Select(l => l.ToLowerInvariant()).ToHashSet();
        if (cleanedSet.SetEquals(expectedSet))
            return new ValidationResult { LenientPass = true, Reason = "correct content with formatting" };

        return new ValidationResult { Reason = $"line mismatch: expected {expectedLines.Count}, got {responseLines.Count}" };
    }

    private ValidationResult ValidateJson(string response, string expected)
    {
        try
        {
            // Try to extract JSON if wrapped in other content
            var jsonMatch = Regex.Match(response, @"[\{\[].*[\}\]]", RegexOptions.Singleline);
            if (jsonMatch.Success)
                response = jsonMatch.Value;

            var responseObj = JsonSerializer.Deserialize<JsonElement>(response);
            var expectedObj = JsonSerializer.Deserialize<JsonElement>(expected);

            if (JsonElementEquals(responseObj, expectedObj, strict: true))
                return new ValidationResult { StrictPass = true };

            if (JsonElementEquals(responseObj, expectedObj, strict: false))
                return new ValidationResult { LenientPass = true, Reason = "JSON structure matches with type coercion" };

            return new ValidationResult { Reason = "JSON content mismatch" };
        }
        catch (JsonException)
        {
            return new ValidationResult { Reason = "invalid JSON" };
        }
    }

    private bool JsonElementEquals(JsonElement a, JsonElement b, bool strict)
    {
        if (a.ValueKind != b.ValueKind)
        {
            if (!strict)
            {
                // Allow string "25" to match number 25
                var aStr = a.ToString();
                var bStr = b.ToString();
                return string.Equals(aStr, bStr, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        return a.ValueKind switch
        {
            JsonValueKind.Object => JsonObjectEquals(a, b, strict),
            JsonValueKind.Array => JsonArrayEquals(a, b, strict),
            JsonValueKind.String => string.Equals(a.GetString(), b.GetString(),
                strict ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => a.GetDecimal() == b.GetDecimal(),
            JsonValueKind.True or JsonValueKind.False => a.GetBoolean() == b.GetBoolean(),
            JsonValueKind.Null => true,
            _ => a.ToString() == b.ToString()
        };
    }

    private bool JsonObjectEquals(JsonElement a, JsonElement b, bool strict)
    {
        var aProps = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var bProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        if (aProps.Count != bProps.Count) return false;

        foreach (var (key, aVal) in aProps)
        {
            if (!bProps.TryGetValue(key, out var bVal)) return false;
            if (!JsonElementEquals(aVal, bVal, strict)) return false;
        }
        return true;
    }

    private bool JsonArrayEquals(JsonElement a, JsonElement b, bool strict)
    {
        var aArr = a.EnumerateArray().ToList();
        var bArr = b.EnumerateArray().ToList();

        if (aArr.Count != bArr.Count) return false;

        for (int i = 0; i < aArr.Count; i++)
        {
            if (!JsonElementEquals(aArr[i], bArr[i], strict)) return false;
        }
        return true;
    }

    private ValidationResult ValidateNumeric(string response, string expected)
    {
        // Extract number from response
        var numMatch = Regex.Match(response, @"-?\d+\.?\d*");
        if (!numMatch.Success)
            return new ValidationResult { Reason = "no number found" };

        if (!double.TryParse(numMatch.Value, out var responseNum))
            return new ValidationResult { Reason = "could not parse number" };

        if (!double.TryParse(expected, out var expectedNum))
            return new ValidationResult { Reason = "invalid expected value" };

        if (Math.Abs(responseNum - expectedNum) < 1e-9)
            return new ValidationResult { StrictPass = true };

        // Check if it's a formatting difference (36.0 vs 36)
        if (Math.Abs(responseNum - expectedNum) < 0.01)
            return new ValidationResult { LenientPass = true, Reason = "minor numeric difference" };

        return new ValidationResult { Reason = $"expected {expectedNum}, got {responseNum}" };
    }

    private ValidationResult ValidateBoolean(string response, string expected)
    {
        var normalized = response.ToLowerInvariant().Trim();
        var expectedNorm = expected.ToLowerInvariant().Trim();

        // Direct match
        if (normalized == expectedNorm)
            return new ValidationResult { StrictPass = true };

        // Common boolean representations
        var trueValues = new HashSet<string> { "true", "yes", "1", "correct", "affirmative" };
        var falseValues = new HashSet<string> { "false", "no", "0", "incorrect", "negative" };

        var responseIsTrue = trueValues.Contains(normalized);
        var responseIsFalse = falseValues.Contains(normalized);
        var expectedIsTrue = trueValues.Contains(expectedNorm);

        if ((responseIsTrue && expectedIsTrue) || (responseIsFalse && !expectedIsTrue))
        {
            if (normalized == expectedNorm)
                return new ValidationResult { StrictPass = true };
            else
                return new ValidationResult { LenientPass = true, Reason = "boolean equivalent" };
        }

        return new ValidationResult { Reason = "boolean mismatch" };
    }

    private static List<string> ExtractWords(string text)
    {
        return Regex.Split(text, @"[\s,]+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .ToList();
    }

    private static string Normalize(string s) => s?
        .Trim()
        .Replace("\r\n", "\n")
        .Replace("\r", "\n")
        ?? string.Empty;

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "...";

    private void DisplayTestResults(List<InstructionTestResult> results)
    {
        if (!results.Any()) return;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Model[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Pass Rate[/]").Centered())
            .AddColumn(new TableColumn("[bold]Strict/Lenient[/]").Centered())
            .AddColumn(new TableColumn("[bold]Avg t/s[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Avg Latency[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Status[/]").Centered());

        foreach (var result in results.OrderByDescending(r => r.PassRate).ThenByDescending(r => r.AvgTokensPerSec))
        {
            var strictPasses = result.PassedTests - result.LenientPasses;
            var statusIcon = result.PassRate >= 0.8 ? "[green]✓[/]" :
                             result.PassRate >= 0.6 ? "[yellow]~[/]" : "[red]✗[/]";

            table.AddRow(
                result.ModelName,
                $"{result.PassRate:P0}",
                $"{strictPasses}/{result.LenientPasses}/{result.TotalTests}",
                result.AvgTokensPerSec.ToString("F1"),
                $"{result.AvgLatencyMs:F0}ms",
                statusIcon
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[dim]Strict/Lenient: strict passes / lenient passes / total tests[/]");
        AnsiConsole.WriteLine();
    }

    public string SelectBaseJudge(
     List<InstructionTestResult> instructionResults,
     List<ReasoningTestSummary>? reasoningResults = null)
    {
        // If we have reasoning results, use composite scoring (intelligence + formatting)
        if (reasoningResults != null && reasoningResults.Any())
        {
            var qualified = instructionResults
                .Where(i => i.PassRate >= 0.8) // Must follow instructions (can output JSON)
                .Join(
                    reasoningResults.Where(r => r.AvgOverallScore >= 7.0), // Must be smart
                    i => i.ModelName,
                    r => r.ModelName,
                    (i, r) => new
                    {
                        ModelName = i.ModelName,
                        InstructionRate = i.PassRate,
                        ReasoningScore = r.AvgOverallScore,
                        TokensPerSec = i.AvgTokensPerSec,
                        // Composite score: 60% reasoning (intelligence), 40% instruction (formatting)
                        CompositeScore = (r.AvgOverallScore / 10.0 * 0.6) + (i.PassRate * 0.4)
                    })
                .OrderByDescending(j => j.CompositeScore) // Prioritize intelligence over speed
                .ThenByDescending(j => j.TokensPerSec) // Speed only as tiebreaker
                .ToList();

            if (qualified.Any())
            {
                var judge = qualified.First();
                AnsiConsole.MarkupLine($"\n[bold green]✓ Selected Base Judge: {judge.ModelName}[/]");
                AnsiConsole.MarkupLine($"  Instruction: {judge.InstructionRate:P0}, Reasoning: {judge.ReasoningScore:F1}/10, Composite: {judge.CompositeScore:P0}, Avg t/s: {judge.TokensPerSec:F1}\n");
                AnsiConsole.MarkupLine($"[dim]  (Selected based on intelligence + formatting ability, not just speed)[/]\n");

                return judge.ModelName;
            }

            AnsiConsole.MarkupLine("[yellow]⚠ No models qualified as judges (need 80%+ instruction AND 7.0+ reasoning)[/]");
            AnsiConsole.MarkupLine("[yellow]  Falling back to instruction-only selection...[/]\n");
        }

        // Fallback: instruction-following only (original logic)
        var fallbackQualified = instructionResults
            .Where(r => r.PassRate >= 0.8) // Must pass at least 80% of tests
            .OrderByDescending(r => r.PassRate)
            .ThenByDescending(r => r.AvgTokensPerSec)
            .ToList();

        if (!fallbackQualified.Any())
        {
            AnsiConsole.MarkupLine("[red]✗ No models passed the instruction tests sufficiently![/]");
            return instructionResults.OrderByDescending(r => r.PassRate).First().ModelName;
        }

        var fallbackJudge = fallbackQualified.First();
        AnsiConsole.MarkupLine($"\n[bold yellow]⚠ Selected Base Judge (instruction-only): {fallbackJudge.ModelName}[/]");
        AnsiConsole.MarkupLine($"  Pass Rate: {fallbackJudge.PassRate:P0}, Avg t/s: {fallbackJudge.AvgTokensPerSec:F1}\n");
        AnsiConsole.MarkupLine($"[dim]  (Warning: Selected without reasoning test validation)[/]\n");

        return fallbackJudge.ModelName;
    }
}

// Updated model classes
public class InstructionTest
{
    public string Prompt { get; set; } = "";
    public string ExpectedResult { get; set; } = "";
    public string ValidationType { get; set; } = "exact";
    public bool StrictOrder { get; set; } = true;
}

public class ValidationResult
{
    public bool StrictPass { get; set; }
    public bool LenientPass { get; set; }
    public string Reason { get; set; } = "";
}

public class InstructionTestResult
{
    public string ModelName { get; set; } = "";
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int LenientPasses { get; set; }
    public double PassRate => TotalTests > 0 ? (double)PassedTests / TotalTests : 0;
    public double StrictPassRate => TotalTests > 0 ? (double)(PassedTests - LenientPasses) / TotalTests : 0;
    public double AvgTokensPerSec { get; set; }
    public double AvgLatencyMs { get; set; }
    public List<string> FailureDetails { get; set; } = new();
}
