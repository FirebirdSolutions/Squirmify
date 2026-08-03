using System.Collections.Generic;
using System.Threading.Tasks;
using Squirmify.Core.Entities;

namespace Squirmify.Core.Interfaces;

public interface IModelRepository
{
	Task<IEnumerable<Model>> GetAllAsync();

	Task<IEnumerable<Model>> GetByProviderAsync(int providerId);

	Task<IEnumerable<Model>> GetAvailableByProviderAsync(int providerId);

	Task<IEnumerable<Model>> GetAllByProviderIncludingDeletedAsync(int providerId);

	Task<IEnumerable<Model>> GetTestableByProviderAsync(int providerId);

	Task<Model?> GetByIdAsync(int id);

	Task<Model?> GetByIdentifierAsync(int providerId, string identifier);

	Task<int> CreateAsync(Model model);

	Task UpdateAsync(Model model);

	Task SetDisabledAsync(int id, bool disabled);

	Task SetAvailableAsync(int id, bool available);

	Task SoftDeleteAsync(int id);

	Task RestoreAsync(int id);

	Task DeleteAsync(int id);

	Task<int> UpsertAsync(Model model);
}
