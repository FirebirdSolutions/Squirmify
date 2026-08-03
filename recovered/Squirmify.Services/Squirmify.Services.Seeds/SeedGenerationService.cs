using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;

namespace Squirmify.Services.Seeds;

public class SeedGenerationService
{
	private readonly ISeedRepository _seedRepo;

	private readonly IConfigRepository _configRepo;

	private static readonly Dictionary<string, string[]> VerbParaphrases = new Dictionary<string, string[]>
	{
		["Create"] = new string[5] { "Build", "Design", "Implement", "Develop", "Put together" },
		["Show"] = new string[4] { "Demonstrate", "Display", "Provide", "Give" },
		["Explain"] = new string[5] { "Describe", "Clarify", "Break down", "Walk through", "Run through" },
		["List"] = new string[4] { "Enumerate", "Outline", "Detail", "Catalog" },
		["Suggest"] = new string[4] { "Recommend", "Propose", "Advise", "Offer" },
		["Write"] = new string[4] { "Draft", "Compose", "Code", "Craft" },
		["Provide"] = new string[4] { "Give", "Supply", "Offer", "Present" },
		["Help"] = new string[3] { "Assist with", "Guide through", "Support with" }
	};

	private static readonly string[] ContextSuffixes = new string[8] { "Include a minimal code example.", "Focus on accessibility wins first.", "Suggest pitfalls to avoid.", "End with one actionable next step.", "Prefer bullet points and be concise.", "Show one tiny test case.", "Assume .NET 9 and Blazor.", "Keep it under 200 words." };

	private static readonly string[] SupportSuffixes = new string[4] { "Keep it under 150 words.", "End with a one-sentence encouragement.", "Use a warm, empathetic tone.", "Suggest one tiny action the user can take right now." };

	private static readonly string[] KiwiStarters = new string[5] { "Hey mate, ", "Yo, ", "G'day, ", "Kia ora, ", "" };

	private static readonly string[] KiwiEndings = new string[4] { " Cheers!", " Sweet as.", " Keen as to see what you come up with.", "" };

	private static readonly Dictionary<string, string[]> KiwiPhrases = new Dictionary<string, string[]>
	{
		["good job"] = new string[4] { "good on ya", "nice one", "sweet as", "choice" },
		["great"] = new string[4] { "choice", "bloody brilliant", "primo", "mean as" },
		["okay"] = new string[4] { "sweet as", "no worries", "she'll be right", "all good" },
		["yes"] = new string[4] { "yeah nah", "yep", "for sure", "keen as" },
		["help"] = new string[3] { "give you a hand", "sort you out", "help out" },
		["understand"] = new string[3] { "get it", "sus it out", "work it out" },
		["fixed"] = new string[3] { "sorted", "all sorted", "back on track" },
		["broken"] = new string[3] { "munted", "rooted", "not going to plan" }
	};

	public SeedGenerationService(ISeedRepository seedRepo, IConfigRepository configRepo)
	{
		_seedRepo = seedRepo;
		_configRepo = configRepo;
	}

	public async Task<int> GenerateAugmentedSeedsAsync(int configId, Action<string>? log = null)
	{
		TestSuiteConfig config = await _configRepo.GetConfigByIdAsync(configId);
		if (config == null)
		{
			log?.Invoke("Config not found");
			return 0;
		}
		List<CategorySetting> categorySettings = (await _configRepo.GetCategorySettingsAsync(configId)).ToList();
		Dictionary<string, double> weights = categorySettings.ToDictionary((CategorySetting categorySetting) => categorySetting.Category, (CategorySetting categorySetting) => categorySetting.Weight);
		List<Seed> baseSeeds = (await _seedRepo.GetAllAsync()).Where((Seed s) => !s.IsAugmented).ToList();
		if (!baseSeeds.Any())
		{
			log?.Invoke("No base seeds found. Import base_seeds.jsonl first.");
			return 0;
		}
		log?.Invoke($"Found {baseSeeds.Count} base seeds");
		if (config.OverwriteSeeds)
		{
			await _seedRepo.DeleteAugmentedSeedsAsync();
			log?.Invoke("Cleared existing augmented seeds");
		}
		int targetCount = config.TargetSeedCount;
		string[] categories = new string[4] { "code", "instruction", "chat", "support" };
		Dictionary<string, double> normalizedWeights = NormalizeWeights(weights, categories);
		Dictionary<string, int> quotas = ComputeQuotas(normalizedWeights, targetCount, categories);
		log?.Invoke($"Target: {targetCount} seeds with weights - " + string.Join(", ", categories.Select((string text) => $"{text}: {quotas[text]}")));
		Dictionary<string, List<Seed>> byCat = (from s in baseSeeds
			group s by s.Category.ToLowerInvariant()).ToDictionary<IGrouping<string, Seed>, string, List<Seed>>((IGrouping<string, Seed> g) => g.Key, (IGrouping<string, Seed> g) => g.ToList(), StringComparer.OrdinalIgnoreCase);
		string[] array = categories;
		foreach (string c in array)
		{
			if (!byCat.ContainsKey(c))
			{
				byCat[c] = new List<Seed>();
			}
		}
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<Seed> generated = new List<Seed>();
		Dictionary<string, int> counts = categories.ToDictionary((string result) => result, (string _) => 0);
		foreach (Seed seed in baseSeeds)
		{
			string cat = seed.Category.ToLowerInvariant();
			if (!categories.Contains(cat))
			{
				cat = "instruction";
			}
			if (counts[cat] < quotas[cat] && seen.Add(seed.Instruction))
			{
				generated.Add(seed);
				counts[cat]++;
			}
		}
		log?.Invoke($"Added {generated.Count} base seeds");
		List<(string type, string? param)> augTypes = new List<(string, string)>
		{
			("paraphrase", null),
			("complexity", null),
			("kiwi_casual", null)
		};
		string[] contextSuffixes = ContextSuffixes;
		foreach (string suffix in contextSuffixes)
		{
			augTypes.Add(("context_suffix", suffix));
		}
		int stagnation = 0;
		Random rnd = Random.Shared;
		while (generated.Count < targetCount && stagnation < 5)
		{
			bool progress = false;
			string[] array2 = categories;
			foreach (string cat2 in array2)
			{
				if (generated.Count >= targetCount)
				{
					break;
				}
				if (counts[cat2] >= quotas[cat2])
				{
					continue;
				}
				List<Seed> catSeeds = byCat.GetValueOrDefault(cat2, new List<Seed>());
				if (!catSeeds.Any())
				{
					continue;
				}
				Seed baseSeed = catSeeds[rnd.Next(catSeeds.Count)];
				bool isSupport = cat2 == "support";
				foreach (var (augType, param) in augTypes)
				{
					if (generated.Count >= targetCount || counts[cat2] >= quotas[cat2])
					{
						break;
					}
					string augmented = Augment(baseSeed.Instruction, augType, param, isSupport, rnd);
					if (seen.Add(augmented))
					{
						Seed newSeed = new Seed
						{
							Category = cat2,
							Instruction = augmented,
							Temperature = baseSeed.Temperature,
							TopP = baseSeed.TopP,
							MaxTokens = baseSeed.MaxTokens,
							IsAugmented = true,
							SourceSeedId = baseSeed.Id,
							CreatedAt = DateTime.UtcNow
						};
						newSeed.Id = await _seedRepo.CreateAsync(newSeed);
						generated.Add(newSeed);
						counts[cat2]++;
						progress = true;
					}
				}
			}
			if (!progress)
			{
				stagnation++;
			}
		}
		while (generated.Count < targetCount && baseSeeds.Any())
		{
			Seed baseSeed2 = baseSeeds[rnd.Next(baseSeeds.Count)];
			string augmented2 = Augment(isSupport: baseSeed2.Category.Equals("support", StringComparison.OrdinalIgnoreCase), baseInstr: baseSeed2.Instruction, type: "paraphrase", param: null, rnd: rnd);
			if (seen.Add(augmented2))
			{
				Seed newSeed2 = new Seed
				{
					Category = baseSeed2.Category,
					Instruction = augmented2,
					IsAugmented = true,
					SourceSeedId = baseSeed2.Id,
					CreatedAt = DateTime.UtcNow
				};
				await _seedRepo.CreateAsync(newSeed2);
				generated.Add(newSeed2);
				continue;
			}
			break;
		}
		log?.Invoke($"Generated {generated.Count} total seeds (including base seeds)");
		log?.Invoke("By category: " + string.Join(", ", categories.Select((string text) => $"{text}: {counts[text]}")));
		return generated.Count;
	}

	private string Augment(string baseInstr, string type, string? param, bool isSupport, Random rnd)
	{
		if (1 == 0)
		{
		}
		string result = type switch
		{
			"context_suffix" => AddContextSuffix(baseInstr, param, isSupport, rnd), 
			"paraphrase" => ParaphraseVerb(baseInstr, rnd), 
			"complexity" => AddComplexity(baseInstr, rnd), 
			"kiwi_casual" => MakeKiwiCasual(baseInstr, rnd), 
			_ => baseInstr, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private string AddContextSuffix(string baseInstr, string? param, bool isSupport, Random rnd)
	{
		string[] array = (isSupport ? SupportSuffixes.Concat(ContextSuffixes).ToArray() : ContextSuffixes);
		string text = param ?? array[rnd.Next(array.Length)];
		return string.IsNullOrEmpty(text) ? baseInstr : (baseInstr + " " + text);
	}

	private string ParaphraseVerb(string baseInstr, Random rnd)
	{
		foreach (var (text2, array2) in VerbParaphrases)
		{
			if (baseInstr.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
			{
				string text3 = array2[rnd.Next(array2.Length)];
				return text3 + baseInstr.Substring(text2.Length);
			}
		}
		return baseInstr;
	}

	private string AddComplexity(string baseInstr, Random rnd)
	{
		return (rnd.Next(2) == 0) ? (baseInstr + " Keep it simple for beginners.") : (baseInstr + " Include advanced patterns and edge cases.");
	}

	private string MakeKiwiCasual(string baseInstr, Random rnd)
	{
		string text = KiwiStarters[rnd.Next(KiwiStarters.Length)];
		string text2 = KiwiEndings[rnd.Next(KiwiEndings.Length)];
		if (baseInstr.StartsWith("hey", StringComparison.OrdinalIgnoreCase) || baseInstr.StartsWith("hi", StringComparison.OrdinalIgnoreCase) || baseInstr.StartsWith("yo", StringComparison.OrdinalIgnoreCase) || baseInstr.StartsWith("g'day", StringComparison.OrdinalIgnoreCase) || baseInstr.StartsWith("kia ora", StringComparison.OrdinalIgnoreCase))
		{
			return Kiwiify(baseInstr, rnd) + text2;
		}
		return text + Kiwiify(baseInstr, rnd) + text2;
	}

	private string Kiwiify(string text, Random rnd)
	{
		foreach (var (text3, array2) in KiwiPhrases)
		{
			if (text.Contains(text3, StringComparison.OrdinalIgnoreCase))
			{
				string replacement = array2[rnd.Next(array2.Length)];
				text = Regex.Replace(text, "\\b" + Regex.Escape(text3) + "\\b", replacement, RegexOptions.IgnoreCase);
			}
		}
		return text;
	}

	private static Dictionary<string, double> NormalizeWeights(Dictionary<string, double> raw, string[] categories)
	{
		Dictionary<string, double> dictionary = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
		foreach (string key in categories)
		{
			dictionary[key] = Math.Max(0.0, raw.GetValueOrDefault(key, 0.25));
		}
		double num = dictionary.Values.Sum();
		if (num <= 0.0)
		{
			double value = 1.0 / (double)dictionary.Count;
			foreach (string item in dictionary.Keys.ToList())
			{
				dictionary[item] = value;
			}
			return dictionary;
		}
		foreach (string item2 in dictionary.Keys.ToList())
		{
			dictionary[item2] /= num;
		}
		return dictionary;
	}

	private static Dictionary<string, int> ComputeQuotas(Dictionary<string, double> weights, int total, string[] categories)
	{
		Dictionary<string, double> source = categories.ToDictionary((string c) => c, (string c) => weights.GetValueOrDefault(c, 0.0) * (double)total);
		Dictionary<string, int> floor = source.ToDictionary((KeyValuePair<string, double> kv) => kv.Key, (KeyValuePair<string, double> kv) => (int)Math.Floor(kv.Value));
		int num = floor.Values.Sum();
		int num2 = total - num;
		List<(string, double)> list = (from kv in source
			select (cat: kv.Key, frac: kv.Value - (double)floor[kv.Key]) into x
			orderby x.frac descending
			select x).ThenBy<(string, double), string>(((string cat, double frac) x) => x.cat, StringComparer.OrdinalIgnoreCase).ToList();
		for (int num3 = 0; num3 < num2; num3++)
		{
			floor[list[num3 % list.Count].Item1]++;
		}
		return floor;
	}
}
