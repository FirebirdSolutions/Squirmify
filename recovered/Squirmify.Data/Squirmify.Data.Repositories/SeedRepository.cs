using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;
using Squirmify.Data.Database;

namespace Squirmify.Data.Repositories;

public class SeedRepository : ISeedRepository
{
	private readonly IDbConnectionFactory _connectionFactory;

	public SeedRepository(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public async Task<IEnumerable<Seed>> GetAllAsync()
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<Seed>("SELECT * FROM Seeds ORDER BY Category, Id");
	}

	public async Task<IEnumerable<Seed>> GetByCategoryAsync(string category)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<Seed>("SELECT * FROM Seeds WHERE Category = @Category ORDER BY Id", new
		{
			Category = category
		});
	}

	public async Task<Seed?> GetByIdAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<Seed>("SELECT * FROM Seeds WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<IEnumerable<string>> GetTagsAsync(int seedId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<string>("SELECT Tag FROM SeedTags WHERE SeedId = @SeedId ORDER BY Tag", new
		{
			SeedId = seedId
		});
	}

	public async Task<int> CreateAsync(Seed seed, IEnumerable<string>? tags = null)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		int id = await connection.ExecuteScalarAsync<int>("INSERT INTO Seeds (Category, Instruction, Temperature, TopP, MaxTokens, IsAugmented, SourceSeedId, CreatedAt)\r\nVALUES (@Category, @Instruction, @Temperature, @TopP, @MaxTokens, @IsAugmented, @SourceSeedId, @CreatedAt);\r\nSELECT last_insert_rowid();", seed, transaction);
		if (tags != null)
		{
			foreach (string tag in tags)
			{
				await connection.ExecuteAsync("INSERT INTO SeedTags (SeedId, Tag) VALUES (@SeedId, @Tag)", new
				{
					SeedId = id,
					Tag = tag
				}, transaction);
			}
		}
		transaction.Commit();
		return id;
	}

	public async Task UpdateAsync(Seed seed, IEnumerable<string>? tags = null)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.OpenAsync();
		using DbTransaction transaction = connection.BeginTransaction();
		await connection.ExecuteAsync("UPDATE Seeds SET\r\n    Category = @Category,\r\n    Instruction = @Instruction,\r\n    Temperature = @Temperature,\r\n    TopP = @TopP,\r\n    MaxTokens = @MaxTokens,\r\n    IsAugmented = @IsAugmented,\r\n    SourceSeedId = @SourceSeedId\r\nWHERE Id = @Id", seed, transaction);
		if (tags != null)
		{
			await connection.ExecuteAsync("DELETE FROM SeedTags WHERE SeedId = @SeedId", new
			{
				SeedId = seed.Id
			}, transaction);
			foreach (string tag in tags)
			{
				await connection.ExecuteAsync("INSERT INTO SeedTags (SeedId, Tag) VALUES (@SeedId, @Tag)", new
				{
					SeedId = seed.Id,
					Tag = tag
				}, transaction);
			}
		}
		transaction.Commit();
	}

	public async Task DeleteAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM Seeds WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task DeleteAllAsync()
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM SeedTags");
		await connection.ExecuteAsync("DELETE FROM Seeds");
	}

	public async Task DeleteAugmentedSeedsAsync()
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM SeedTags WHERE SeedId IN (SELECT Id FROM Seeds WHERE IsAugmented = 1)");
		await connection.ExecuteAsync("DELETE FROM Seeds WHERE IsAugmented = 1");
	}

	public async Task<int> GetBaseSeedCountAsync()
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Seeds WHERE IsAugmented = 0");
	}

	public async Task<int> GetAugmentedSeedCountAsync()
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Seeds WHERE IsAugmented = 1");
	}
}
