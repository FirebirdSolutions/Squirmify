using System.Collections.Generic;
using System.Threading.Tasks;
using Squirmify.Core.DTOs;
using Squirmify.Core.Entities;

namespace Squirmify.Core.Interfaces;

public interface ILlmClient
{
	Task<IEnumerable<string>> LoadModelsFromServerAsync(Provider provider);

	Task<bool> WarmUpModelAsync(Provider provider, string modelIdentifier);

	Task<CompletionResult?> CompletionAsync(Provider provider, string modelIdentifier, string systemPrompt, string userPrompt, double temperature, double topP, int maxTokens);

	bool IsModelFlagged(string modelIdentifier);
}
