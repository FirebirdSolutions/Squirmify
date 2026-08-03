using System.Collections.Generic;
using System.Threading.Tasks;
using Squirmify.Core.Entities;

namespace Squirmify.Core.Interfaces;

public interface IProviderRepository
{
	Task<IEnumerable<Provider>> GetAllAsync();

	Task<IEnumerable<Provider>> GetActiveAsync();

	Task<Provider?> GetByIdAsync(int id);

	Task<int> CreateAsync(Provider provider);

	Task UpdateAsync(Provider provider);

	Task DeleteAsync(int id);
}
