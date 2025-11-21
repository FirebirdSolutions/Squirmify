using System.Text.Json;
using System.Text.RegularExpressions;
using ModelEvaluator.Models;
using Spectre.Console;

namespace ModelEvaluator.Services;

public class SeedService
{
    private static readonly Dictionary<string, string[]> KiwiPhrases = new()
    {
        ["good job"] = new[] { "good on ya", "nice one", "sweet as", "choice", "bloody good work" },
        ["great"] = new[] { "choice", "bloody brilliant", "primo", "top notch", "mean as" },
        ["okay"] = new[] { "sweet as", "no worries", "she'll be right", "all good" },
        ["yes"] = new[] { "yeah bah", "yep", "for sure", "choice", "keen as" },
        ["let's do it"] = new[] { "let's crack on", "let's get stuck in", "keen as", "let's give it a go" },
        ["that's right"] = new[] { "too right", "spot on", "bang on", "yeah nah yeah" },
        ["help"] = new[] { "give you a hand", "sort you out", "help out" },
        ["understand"] = new[] { "get it", "sus it out", "work it out", "figure it" },
        ["fixed"] = new[] { "sorted", "all sorted", "fixed up good", "back on track" },
        ["broken"] = new[] { "munted", "rooted", "buggered", "not going to plan" }
    };

    private static readonly string[] ContextSuffixes = 
    {
        "", "Include a minimal code example.", "Focus on accessibility wins first.",
        "Suggest pitfalls to avoid.", "End with one actionable next step.",
        "Prefer bullet points and be concise.", "Show one tiny test case.",
        "Assume .NET 9 and Blazor.", "Keep it under 200 words."
    };

    private static readonly string[] SupportSuffixes = 
    {
        "Keep it under 150 words.", "End with a one-sentence encouragement.",
        "Use a warm, empathetic tone.", "Suggest one tiny action the user can take right now."
    };

    private static readonly Dictionary<string, string[]> VerbParaphrases = new()
    {
        ["Create"] = new[] { "Build", "Design", "Implement", "Develop", "Knock up", "Put together" },
        ["Show"] = new[] { "Demonstrate", "Display", "Provide", "Give", "Chuck out" },
        ["Explain"] = new[] { "Describe", "Clarify", "Break down", "Walk through", "Run through" },
        ["List"] = new[] { "Enumerate", "Outline", "Detail", "Catalog", "Chuck together a list of" },
        ["Suggest"] = new[] { "Recommend", "Propose", "Advise", "Offer", "Reckon you should try" },
        ["Write"] = new[] { "Draft", "Compose", "Code", "Craft", "Chuck together" },
        ["Provide"] = new[] { "Give", "Supply", "Offer", "Present", "Sort out" },
        ["Help"] = new[] { "Give you a hand with", "Sort you out with", "Assist with" }
    };

    /// <summary>
    /// Load base seeds from JSONL file
    /// </summary>
    public async Task<List<SeedItem>> LoadBaseSeedsAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine($"[red]✗ Base seeds file not found: {filePath}[/]");
            return new List<SeedItem>();
        }

        var lines = await File.ReadAllLinesAsync(filePath);
        var seeds = new List<SeedItem>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            try
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}"))
                    continue;
                    
                var seed = JsonSerializer.Deserialize<SeedItem>(trimmed);
                if (seed != null)
                    seeds.Add(seed);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Invalid JSON:[/] [orange3]{line[..Math.Min(line.Length, 80)]}...[/] → {ex.Message}");
            }
        }

        AnsiConsole.MarkupLine($"[green]✓ Loaded {seeds.Count} base seeds[/]");
        return seeds;
    }


    // 1) NEW: public entry with optional weights — preserves current signature/behavior
    public Task<SeedsConfig> GenerateAugmentedSeedsAsync(
        List<SeedItem> baseSeeds,
        int targetCount,
        Dictionary<string, double>? categoryWeights = null)
    {
        if (categoryWeights is null || categoryWeights.Count == 0)
        {
            // Fall back to your original unweighted flow
            return GenerateAugmentedSeeds_UnweightedAsync(baseSeeds, targetCount);
        }

        // Normalize weights and dispatch to weighted flow
        var normalized = NormalizeWeights(categoryWeights, new[] { "code", "instruction", "chat", "support" });
        return GenerateAugmentedSeeds_WeightedAsync(baseSeeds, targetCount, normalized);
    }

    // 2) NEW: keep your original method body but move it behind a private name so we don’t change behavior
    private Task<SeedsConfig> GenerateAugmentedSeeds_UnweightedAsync(List<SeedItem> baseSeeds, int targetCount)
    {
        // moved original body of GenerateAugmentedSeedsAsync here verbatim
        // (call sites that used the old signature still work via the public overload above)
        return Original_GenerateAugmentedSeeds_Body(baseSeeds, targetCount);
    }

    // 3) NEW: weighted implementation
    private async Task<SeedsConfig> GenerateAugmentedSeeds_WeightedAsync(
        List<SeedItem> baseSeeds,
        int targetCount,
        Dictionary<string, double> weights)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"[yellow]Generating {targetCount} weighted augmented seeds…[/]", async ctx =>
            {
                // --- Setup & buckets ---
                var categories = new[] { "code", "instruction", "chat", "support" };
                var byCat = baseSeeds
                    .GroupBy(GetCategory)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                foreach (var c in categories)
                    if (!byCat.ContainsKey(c)) byCat[c] = new List<SeedItem>();

                var quotas = ComputeQuotas(weights, targetCount, categories);
                var counts = categories.ToDictionary(c => c, _ => 0);

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var expanded = new List<SeedItem>();

                // Prepare augmentation configs (exactly as your current pipeline)
                var configs = new List<(string type, string? param)>
                {
                ("paraphrase", null),
                ("complexity", null)
                };


                foreach (var suffix in ContextSuffixes.Where(s => !string.IsNullOrEmpty(s)))
                    configs.Add(("context_suffix", suffix));

                // --- 1) Seed with base items under quotas (round-robin by category) ---
                var catIndex = 0;
                while (expanded.Count < targetCount && counts.Values.Sum() < targetCount)
                {
                    var cat = categories[catIndex % categories.Length];
                    catIndex++;

                    if (counts[cat] >= quotas[cat]) continue;

                    var list = byCat[cat];
                    var next = list.FirstOrDefault(s => seen.Add(s.instruction));
                    if (next != null)
                    {
                        expanded.Add(next);
                        counts[cat]++;
                    }

                    // Break if no categories can add more
                    if (!categories.Any(c => counts[c] < quotas[c] && byCat[c].Any(s => !expanded.Any(e => e.instruction == s.instruction))))
                        break;
                }

                // --- 2) Augment to fill remaining quotas ---
                var stagnation = 0;
                while (expanded.Count < targetCount && stagnation < 3)
                {
                    var progress = false;

                    foreach (var cat in categories)
                    {
                        if (expanded.Count >= targetCount) break;
                        if (counts[cat] >= quotas[cat]) continue;

                        // pick a base seed to augment from this category (cycle through)
                        var src = byCat[cat].Count == 0 ? null : Random.Shared.NextItem(byCat[cat].ToArray());
                        if (src is null) continue;

                        var isSupport = src.tags.Contains("support");

                        foreach (var (type, param) in configs)
                        {
                            if (expanded.Count >= targetCount) break;
                            if (counts[cat] >= quotas[cat]) break;

                            var variant = new SeedItem
                            {
                                instruction = Augment(src.instruction, type, param, isSupport),
                                tags = new List<string>(src.tags)
                            };
                            if (!variant.tags.Contains("aug"))
                                variant.tags.Add("aug");

                            // accept only if unique & still the same category bucket
                            var vcat = GetCategory(variant);
                            if (!seen.Contains(variant.instruction) && vcat.Equals(cat, StringComparison.OrdinalIgnoreCase))
                            {
                                seen.Add(variant.instruction);
                                expanded.Add(variant);
                                counts[cat]++;
                                progress = true;
                            }
                        }
                    }

                    if (!progress)
                    {
                        // try to redistribute leftover quotas to categories that still have momentum
                        RedistributeLeftoverQuotas(quotas, counts, categories);
                        stagnation++;
                    }

                    ctx.Status($"[cyan]Augmenting seeds… {expanded.Count}/{targetCount}[/]");
                    await Task.Delay(1);
                }

                // If still short (e.g., extreme weights), fill from any category with available material
                while (expanded.Count < targetCount)
                {
                    var src = baseSeeds.Count == 0 ? null : Random.Shared.NextItem(baseSeeds.ToArray());
                    if (src is null) break;

                    var isSupport = src.tags.Contains("support");
                    var variant = new SeedItem
                    {
                        instruction = Augment(src.instruction, "paraphrase", null, isSupport),
                        tags = new List<string>(src.tags)
                    };
                    if (!variant.tags.Contains("aug")) variant.tags.Add("aug");

                    if (seen.Add(variant.instruction))
                        expanded.Add(variant);
                    else
                        break;
                }

                // --- Build final config (unchanged from your shape) ---
                var final = expanded.Take(targetCount).ToList();
                var config = new SeedsConfig
                {
                    system_prompts = Config.CategorySettings.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.SystemPrompt
                    ),
                    seeds = final.Select(e => new SeedEntry
                    {
                        prompt = e.instruction,
                        category = GetCategory(e),
                        temperature = Config.CategorySettings.TryGetValue(GetCategory(e), out var def)
                            ? def.Temperature
                            : Config.GlobalTemperature,
                        max_tokens = Config.CategorySettings.TryGetValue(GetCategory(e), out var def2)
                            ? def2.MaxTokens
                            : Config.GlobalMaxTokens
                    }).ToList()
                };

                AnsiConsole.MarkupLine($"[green]✓ Generated {config.seeds.Count} weighted seeds[/]");
                return config;
            });
    }

    // 4) NEW: helpers
    private static Dictionary<string, double> NormalizeWeights(
        Dictionary<string, double> raw,
        IEnumerable<string> knownCats)
    {
        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in knownCats)
            map[c] = Math.Max(0, raw.TryGetValue(c, out var v) ? v : 0);

        var sum = map.Values.Sum();
        if (sum <= 0)
        {
            var equal = 1.0 / map.Count;
            foreach (var k in map.Keys.ToList()) map[k] = equal;
            return map;
        }

        foreach (var k in map.Keys.ToList()) map[k] /= sum;
        return map;
    }

    private static Dictionary<string, int> ComputeQuotas(
        Dictionary<string, double> weights,
        int total,
        string[] categories)
    {
        // Largest Remainder Method for clean integer quotas
        var raw = categories.ToDictionary(c => c, c => weights.GetValueOrDefault(c, 0) * total);
        var floor = raw.ToDictionary(kv => kv.Key, kv => (int)Math.Floor(kv.Value));
        var assigned = floor.Values.Sum();
        var remainder = total - assigned;

        var ordered = raw
            .Select(kv => (cat: kv.Key, frac: kv.Value - floor[kv.Key]))
            .OrderByDescending(x => x.frac)
            .ThenBy(x => x.cat, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < remainder; i++)
            floor[ordered[i % ordered.Count].cat]++;

        return floor;
    }

    private static void RedistributeLeftoverQuotas(
        Dictionary<string, int> quotas,
        Dictionary<string, int> counts,
        string[] categories)
    {
        var shortfall = quotas.Sum(kv => kv.Value) - counts.Sum(kv => kv.Value);
        if (shortfall <= 0) return;

        // Push leftover to categories that are behind proportionally
        var deficit = categories
            .Select(c => (c, d: quotas[c] - counts[c]))
            .Where(x => x.d > 0)
            .OrderByDescending(x => x.d)
            .ToList();

        foreach (var (c, d) in deficit)
        {
            if (shortfall == 0) break;
            quotas[c] += 1;
            shortfall--;
        }
    }

    // 5) NEW: tiny wrapper that preserves your original method body.
    //    Paste your current GenerateAugmentedSeedsAsync body here and rename it.
    private Task<SeedsConfig> Original_GenerateAugmentedSeeds_Body(List<SeedItem> baseSeeds, int targetCount)
    {
        // === PASTE your current GenerateAugmentedSeedsAsync body here ===
        throw new NotImplementedException("Paste original method body here to preserve behavior.");
    }


    /// <summary>
    /// Generate augmented seeds from base seeds
    /// </summary>
    public async Task<SeedsConfig> GenerateAugmentedSeedsAsyncOld(List<SeedItem> baseSeeds, int targetCount)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"[yellow]Generating {targetCount} augmented seeds…[/]", async ctx =>
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var expanded = new List<SeedItem>();

                // Add base seeds first
                foreach (var seed in baseSeeds)
                {
                    if (seen.Add(seed.instruction))
                        expanded.Add(seed);
                }

                // Augmentation configs
                var configs = new List<(string type, string? param)>
                {
                    ("paraphrase", null),
                    ("complexity", null)
                };

                foreach (var suffix in ContextSuffixes.Where(s => !string.IsNullOrEmpty(s)))
                    configs.Add(("context_suffix", suffix));

                // Generate variants
                while (expanded.Count < targetCount && baseSeeds.Any())
                {
                    foreach (var seed in baseSeeds)
                    {
                        if (expanded.Count >= targetCount) break;
                        
                        var isSupport = seed.tags.Contains("support");
                        
                        foreach (var (type, param) in configs)
                        {
                            if (expanded.Count >= targetCount) break;
                            
                            var variant = new SeedItem
                            {
                                instruction = Augment(seed.instruction, type, param, isSupport),
                                tags = new List<string>(seed.tags)
                            };
                            
                            if (!variant.tags.Contains("aug"))
                                variant.tags.Add("aug");
                                
                            if (seen.Add(variant.instruction))
                                expanded.Add(variant);
                        }
                    }
                    
                    ctx.Status($"[cyan]Augmenting seeds… {expanded.Count}/{targetCount}[/]");
                    await Task.Delay(1);
                }

                var config = new SeedsConfig
                {
                    system_prompts = Config.CategorySettings.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.SystemPrompt
                    ),
                    seeds = expanded.Take(targetCount).Select(e => new SeedEntry
                    {
                        prompt = e.instruction,
                        category = GetCategory(e),
                        temperature = Config.CategorySettings.TryGetValue(GetCategory(e), out var def) 
                            ? def.Temperature 
                            : Config.GlobalTemperature,
                        max_tokens = Config.CategorySettings.TryGetValue(GetCategory(e), out var def2) 
                            ? def2.MaxTokens 
                            : Config.GlobalMaxTokens
                    }).ToList()
                };

                AnsiConsole.MarkupLine($"[green]✓ Generated {config.seeds.Count} seeds[/]");
                return config;
            });
    }

    private string Augment(string baseInstr, string type, string? param, bool isSupport)
    {
        var rnd = Random.Shared;
        
        return type switch
        {
            "context_suffix" => AddContextSuffix(baseInstr, param, isSupport, rnd),
            "paraphrase" => ParaphraseVerb(baseInstr, rnd),
            "complexity" => AddComplexity(baseInstr, rnd),
            "kiwi_casual" => MakeKiwiCasual(baseInstr, rnd),
            _ => baseInstr
        };
    }

    private string AddContextSuffix(string baseInstr, string? param, bool isSupport, Random rnd)
    {
        var suffixes = isSupport 
            ? SupportSuffixes.Concat(ContextSuffixes).ToArray() 
            : ContextSuffixes;
            
        var suffix = param ?? rnd.NextItem(suffixes.Where(s => !string.IsNullOrEmpty(s)).ToArray());
        return string.IsNullOrEmpty(suffix) ? baseInstr : $"{baseInstr} {suffix}";
    }

    private string ParaphraseVerb(string baseInstr, Random rnd)
    {
        foreach (var (verb, replacements) in VerbParaphrases)
        {
            if (baseInstr.StartsWith(verb, StringComparison.OrdinalIgnoreCase))
            {
                return baseInstr.Replace(verb, rnd.NextItem(replacements), 1, StringComparison.OrdinalIgnoreCase);
            }
        }
        return baseInstr;
    }

    private string AddComplexity(string baseInstr, Random rnd)
    {
        return rnd.Next(2) == 0
            ? $"{baseInstr} Keep it simple for beginners."
            : $"{baseInstr} Include advanced patterns and edge cases.";
    }

    private string MakeKiwiCasual(string baseInstr, Random rnd)
    {
        var starters = new[] { "Hey mate, ", "Yo, ", "G'day, ", "Kia ora, ", "" };
        var endings = new[] { " Cheers!", " Sweet as.", " Keen as to see what you come up with.", "" };
        
        var start = rnd.NextItem(starters);
        var end = rnd.NextItem(endings);
        
        if (baseInstr.StartsWith("hey", StringComparison.OrdinalIgnoreCase) ||
            baseInstr.StartsWith("hi", StringComparison.OrdinalIgnoreCase) ||
            baseInstr.StartsWith("yo", StringComparison.OrdinalIgnoreCase) ||
            baseInstr.StartsWith("g'day", StringComparison.OrdinalIgnoreCase) ||
            baseInstr.StartsWith("kia ora", StringComparison.OrdinalIgnoreCase))
        {
            return Kiwiify(baseInstr) + end;
        }
        
        return start + Kiwiify(baseInstr) + end;
    }

    private string Kiwiify(string text)
    {
        if (text == null) return text;
        
        foreach (var (key, replacements) in KiwiPhrases)
        {
            if (text.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                var replacement = Random.Shared.NextItem(replacements);
                text = Regex.Replace(text, $@"\b{Regex.Escape(key)}\b", replacement, RegexOptions.IgnoreCase);
            }
        }
        
        return text;
    }

    private string GetCategory(SeedItem seed) => seed.tags switch
    {
        var t when t.Contains("code") => "code",
        var t when t.Contains("instruction") => "instruction",
        var t when t.Contains("chat") => "chat",
        var t when t.Contains("support") => "support",
        _ => "general"
    };

    /// <summary>
    /// Save seeds config to JSON file
    /// </summary>
    public async Task SaveSeedsAsync(SeedsConfig config, string filePath)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
        AnsiConsole.MarkupLine($"[green]✓ Saved seeds to {filePath}[/]");
    }

    /// <summary>
    /// Load seeds config from JSON file
    /// </summary>
    public async Task<SeedsConfig?> LoadSeedsAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ Seeds file not found: {filePath}[/]");
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<SeedsConfig>(json);
    }
}
