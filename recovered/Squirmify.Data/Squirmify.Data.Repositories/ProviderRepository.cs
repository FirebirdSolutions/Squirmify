using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;
using Squirmify.Data.Database;

namespace Squirmify.Data.Repositories;

public class ProviderRepository : IProviderRepository
{
	private readonly IDbConnectionFactory _connectionFactory;

	public ProviderRepository(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public async Task<IEnumerable<Provider>> GetAllAsync()
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<Provider>("SELECT * FROM Providers ORDER BY Name");
	}

	public async Task<IEnumerable<Provider>> GetActiveAsync()
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<Provider>("SELECT * FROM Providers WHERE IsActive = 1 ORDER BY Name");
	}

	public async Task<Provider?> GetByIdAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<Provider>("SELECT * FROM Providers WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<int> CreateAsync(Provider provider)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT INTO Providers (Name, BaseUrl, AuthToken, UseAuth, TimeoutMinutes, IsActive, CreatedAt)\r\nVALUES (@Name, @BaseUrl, @AuthToken, @UseAuth, @TimeoutMinutes, @IsActive, @CreatedAt);\r\nSELECT last_insert_rowid();", provider);
	}

	public async Task UpdateAsync(Provider provider)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		provider.UpdatedAt = DateTime.UtcNow;
		await connection.ExecuteAsync("UPDATE Providers SET\r\n    Name = @Name,\r\n    BaseUrl = @BaseUrl,\r\n    AuthToken = @AuthToken,\r\n    UseAuth = @UseAuth,\r\n    TimeoutMinutes = @TimeoutMinutes,\r\n    IsActive = @IsActive,\r\n    UpdatedAt = @UpdatedAt\r\nWHERE Id = @Id", provider);
	}

	public async Task DeleteAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM Providers WHERE Id = @Id", new
		{
			Id = id
		});
	}
}
