using System.Collections.Generic;
using System.Threading.Tasks;
using Squirmify.Core.Entities;

namespace Squirmify.Core.Interfaces;

public interface IModelGroupRepository
{
	Task<IEnumerable<ModelGroup>> GetAllAsync();

	Task<ModelGroup?> GetByIdAsync(int id);

	Task<int> CreateAsync(ModelGroup group);

	Task UpdateAsync(ModelGroup group);

	Task DeleteAsync(int id);

	Task<IEnumerable<ModelGroupMember>> GetMembersAsync(int groupId);

	Task<IEnumerable<Model>> GetModelsAsync(int groupId);

	Task AddMemberAsync(int groupId, int modelId);

	Task RemoveMemberAsync(int groupId, int modelId);

	Task SetMembersAsync(int groupId, IEnumerable<int> modelIds);
}
