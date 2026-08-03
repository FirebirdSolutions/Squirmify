using System.Collections.Generic;
using System.Threading.Tasks;
using Squirmify.Core.Entities;

namespace Squirmify.Core.Interfaces;

public interface IConfigRepository
{
	Task<IEnumerable<TestSuiteConfig>> GetAllConfigsAsync();

	Task<TestSuiteConfig?> GetConfigByIdAsync(int id);

	Task<int> CreateConfigAsync(TestSuiteConfig config);

	Task UpdateConfigAsync(TestSuiteConfig config);

	Task DeleteConfigAsync(int id);

	Task<IEnumerable<CategorySetting>> GetCategorySettingsAsync(int configId);

	Task SaveCategorySettingsAsync(int configId, IEnumerable<CategorySetting> settings);

	Task<IEnumerable<TestTypeLimit>> GetTestTypeLimitsAsync(int configId);

	Task SaveTestTypeLimitsAsync(int configId, IEnumerable<TestTypeLimit> limits);
}
