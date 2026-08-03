using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;
using Squirmify.Data.Database;

namespace Squirmify.Data.Repositories;

public class ModelRepository : IModelRepository
{
	private readonly IDbConnectionFactory _connectionFactory;

	public ModelRepository(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public async Task<IEnumerable<Model>> GetAllAsync()
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<Model>("SELECT * FROM Models WHERE IsDeleted = 0 ORDER BY Identifier");
	}

	public async Task<IEnumerable<Model>> GetByProviderAsync(int providerId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<Model>("SELECT * FROM Models WHERE ProviderId = @ProviderId AND IsDeleted = 0 ORDER BY Identifier", new
		{
			ProviderId = providerId
		});
	}

	public async Task<IEnumerable<Model>> GetAvailableByProviderAsync(int providerId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<Model>("SELECT * FROM Models WHERE ProviderId = @ProviderId AND IsAvailable = 1 AND IsDeleted = 0 ORDER BY Identifier", new
		{
			ProviderId = providerId
		});
	}

	public async Task<IEnumerable<Model>> GetAllByProviderIncludingDeletedAsync(int providerId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<Model>("SELECT * FROM Models WHERE ProviderId = @ProviderId ORDER BY Identifier", new
		{
			ProviderId = providerId
		});
	}

	public async Task<IEnumerable<Model>> GetTestableByProviderAsync(int providerId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<Model>("SELECT * FROM Models WHERE ProviderId = @ProviderId AND IsDisabled = 0 AND IsAvailable = 1 AND IsDeleted = 0 ORDER BY Identifier", new
		{
			ProviderId = providerId
		});
	}

	public async Task<Model?> GetByIdAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<Model>("SELECT * FROM Models WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<Model?> GetByIdentifierAsync(int providerId, string identifier)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<Model>("SELECT * FROM Models WHERE ProviderId = @ProviderId AND Identifier = @Identifier", new
		{
			ProviderId = providerId,
			Identifier = identifier
		});
	}

	public async Task<int> CreateAsync(Model model)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT INTO Models (ProviderId, Identifier, DisplayName, IsDisabled, IsAvailable, IsDeleted, CreatedAt)\r\nVALUES (@ProviderId, @Identifier, @DisplayName, @IsDisabled, @IsAvailable, @IsDeleted, @CreatedAt);\r\nSELECT last_insert_rowid();", model);
	}

	public async Task UpdateAsync(Model model)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE Models SET\r\n    Identifier = @Identifier,\r\n    DisplayName = @DisplayName,\r\n    IsDisabled = @IsDisabled,\r\n    IsAvailable = @IsAvailable,\r\n    IsDeleted = @IsDeleted\r\nWHERE Id = @Id", model);
	}

	public async Task SetDisabledAsync(int id, bool disabled)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE Models SET IsDisabled = @Disabled WHERE Id = @Id", new
		{
			Id = id,
			Disabled = (disabled ? 1 : 0)
		});
	}

	public async Task SetAvailableAsync(int id, bool available)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE Models SET IsAvailable = @Available WHERE Id = @Id", new
		{
			Id = id,
			Available = (available ? 1 : 0)
		});
	}

	public async Task SoftDeleteAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE Models SET IsDeleted = 1 WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task RestoreAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE Models SET IsDeleted = 0 WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task DeleteAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM Models WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<int> UpsertAsync(Model model)
	{
		Model existing = await GetByIdentifierAsync(model.ProviderId, model.Identifier);
		if (existing != null)
		{
			model.Id = existing.Id;
			await UpdateAsync(model);
			return existing.Id;
		}
		return await CreateAsync(model);
	}
}
