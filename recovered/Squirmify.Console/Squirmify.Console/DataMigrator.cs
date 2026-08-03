using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;

namespace Squirmify.Console;

public class DataMigrator
{
	private class InstructionTestsFile
	{
		public List<InstructionTestJson>? Tests { get; set; }

		public string? SystemPrompt { get; set; }
	}

	private class InstructionTestJson
	{
		public string Prompt { get; set; } = "";

		public string? ExpectedResult { get; set; }

		public string? ValidationType { get; set; }

		public bool StrictOrder { get; set; }

		public string? Category { get; set; }
	}

	private class ReasoningTestsFile
	{
		public List<ReasoningTestJson>? Tests { get; set; }

		public string? SystemPrompt { get; set; }
	}

	private class ReasoningTestJson
	{
		public string? Category { get; set; }

		public string? Description { get; set; }

		public string Prompt { get; set; } = "";

		public string CorrectAnswer { get; set; } = "";
	}

	private class ConversationTestsFile
	{
		public List<ConversationTestJson>? Tests { get; set; }
	}

	private class ConversationTestJson
	{
		public string? Category { get; set; }

		public string? Description { get; set; }

		public string? SystemPrompt { get; set; }

		public List<TurnJson>? Turns { get; set; }

		public List<string>? JudgingCriteria { get; set; }
	}

	private class TurnJson
	{
		public string UserMessage { get; set; } = "";
	}

	private readonly ITestDefinitionRepository _testRepo;

	private readonly string _configPath;

	public DataMigrator(ITestDefinitionRepository testRepo, string configPath)
	{
		_testRepo = testRepo;
		_configPath = configPath;
	}

	public async Task<int> MigrateAllAsync()
	{
		int total = 0;
		int num = total;
		total = num + await MigrateInstructionTestsAsync();
		int num2 = total;
		total = num2 + await MigrateReasoningTestsAsync();
		int num3 = total;
		return num3 + await MigrateConversationTestsAsync();
	}

	public async Task<int> MigrateInstructionTestsAsync()
	{
		string path = Path.Combine(_configPath, "tests", "instruction_tests.json");
		if (!File.Exists(path))
		{
			return 0;
		}
		InstructionTestsFile data = JsonSerializer.Deserialize<InstructionTestsFile>(await File.ReadAllTextAsync(path), new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		});
		if (data?.Tests == null)
		{
			return 0;
		}
		int count = 0;
		foreach (InstructionTestJson test in data.Tests)
		{
			await _testRepo.CreateInstructionTestAsync(new InstructionTest
			{
				Category = (test.Category ?? "general"),
				Prompt = test.Prompt,
				ExpectedResult = (test.ExpectedResult ?? ""),
				ValidationType = (test.ValidationType ?? "exact"),
				StrictOrder = test.StrictOrder,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});
			count++;
		}
		return count;
	}

	public async Task<int> MigrateReasoningTestsAsync()
	{
		string path = Path.Combine(_configPath, "tests", "reasoning_tests.json");
		if (!File.Exists(path))
		{
			return 0;
		}
		ReasoningTestsFile data = JsonSerializer.Deserialize<ReasoningTestsFile>(await File.ReadAllTextAsync(path), new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		});
		if (data?.Tests == null)
		{
			return 0;
		}
		int count = 0;
		foreach (ReasoningTestJson test in data.Tests)
		{
			await _testRepo.CreateReasoningTestAsync(new ReasoningTest
			{
				Category = (test.Category ?? "general"),
				Description = test.Description,
				Prompt = test.Prompt,
				CorrectAnswer = test.CorrectAnswer,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});
			count++;
		}
		return count;
	}

	public async Task<int> MigrateConversationTestsAsync()
	{
		string path = Path.Combine(_configPath, "tests", "conversation_tests.json");
		if (!File.Exists(path))
		{
			return 0;
		}
		ConversationTestsFile data = JsonSerializer.Deserialize<ConversationTestsFile>(await File.ReadAllTextAsync(path), new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		});
		if (data?.Tests == null)
		{
			return 0;
		}
		int count = 0;
		foreach (ConversationTestJson test in data.Tests)
		{
			ConversationTest conversationTest = new ConversationTest
			{
				Category = (test.Category ?? "general"),
				Description = test.Description,
				SystemPrompt = test.SystemPrompt,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};
			List<ConversationTurn> turns = new List<ConversationTurn>();
			if (test.Turns != null)
			{
				for (int i = 0; i < test.Turns.Count; i++)
				{
					turns.Add(new ConversationTurn
					{
						TurnNumber = i + 1,
						UserMessage = test.Turns[i].UserMessage,
						ExpectedTheme = null
					});
				}
			}
			List<ConversationJudgingCriterion> criteria = new List<ConversationJudgingCriterion>();
			if (test.JudgingCriteria != null)
			{
				for (int j = 0; j < test.JudgingCriteria.Count; j++)
				{
					criteria.Add(new ConversationJudgingCriterion
					{
						Criterion = test.JudgingCriteria[j],
						SortOrder = j
					});
				}
			}
			await _testRepo.CreateConversationTestAsync(conversationTest, turns, criteria);
			count++;
		}
		return count;
	}
}
