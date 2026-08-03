using System.Collections.Generic;
using System.Threading.Tasks;
using Squirmify.Core.Entities;

namespace Squirmify.Core.Interfaces;

public interface ISeedRepository
{
	Task<IEnumerable<Seed>> GetAllAsync();

	Task<IEnumerable<Seed>> GetByCategoryAsync(string category);

	Task<Seed?> GetByIdAsync(int id);

	Task<IEnumerable<string>> GetTagsAsync(int seedId);

	Task<int> CreateAsync(Seed seed, IEnumerable<string>? tags = null);

	Task UpdateAsync(Seed seed, IEnumerable<string>? tags = null);

	Task DeleteAsync(int id);

	Task DeleteAllAsync();

	Task DeleteAugmentedSeedsAsync();

	Task<int> GetBaseSeedCountAsync();

	Task<int> GetAugmentedSeedCountAsync();
}
