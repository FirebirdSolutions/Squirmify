using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Squirmify.Core.Entities;
using Squirmify.Core.Interfaces;
using Squirmify.Data.Database;

namespace Squirmify.Data.Repositories;

public class ModelGroupRepository : IModelGroupRepository
{
	private readonly IDbConnectionFactory _connectionFactory;

	public ModelGroupRepository(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public async Task<IEnumerable<ModelGroup>> GetAllAsync()
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<ModelGroup>("SELECT * FROM ModelGroups ORDER BY Name");
	}

	public async Task<ModelGroup?> GetByIdAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QuerySingleOrDefaultAsync<ModelGroup>("SELECT * FROM ModelGroups WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<int> CreateAsync(ModelGroup group)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.ExecuteScalarAsync<int>("INSERT INTO ModelGroups (Name, Description, CreatedAt)\r\nVALUES (@Name, @Description, @CreatedAt);\r\nSELECT last_insert_rowid();", group);
	}

	public async Task UpdateAsync(ModelGroup group)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		group.UpdatedAt = DateTime.UtcNow;
		await connection.ExecuteAsync("UPDATE ModelGroups SET\r\n    Name = @Name,\r\n    Description = @Description,\r\n    UpdatedAt = @UpdatedAt\r\nWHERE Id = @Id", group);
	}

	public async Task DeleteAsync(int id)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM ModelGroupMembers WHERE GroupId = @Id", new
		{
			Id = id
		});
		await connection.ExecuteAsync("DELETE FROM ModelGroups WHERE Id = @Id", new
		{
			Id = id
		});
	}

	public async Task<IEnumerable<ModelGroupMember>> GetMembersAsync(int groupId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<ModelGroupMember>("SELECT * FROM ModelGroupMembers WHERE GroupId = @GroupId ORDER BY AddedAt", new
		{
			GroupId = groupId
		});
	}

	public async Task<IEnumerable<Model>> GetModelsAsync(int groupId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		return await connection.QueryAsync<Model>("SELECT m.* FROM Models m\r\nINNER JOIN ModelGroupMembers mgm ON m.Id = mgm.ModelId\r\nWHERE mgm.GroupId = @GroupId AND m.IsDeleted = 0\r\nORDER BY m.Identifier", new
		{
			GroupId = groupId
		});
	}

	public async Task AddMemberAsync(int groupId, int modelId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("INSERT OR IGNORE INTO ModelGroupMembers (GroupId, ModelId, AddedAt)\r\nVALUES (@GroupId, @ModelId, @AddedAt)", new
		{
			GroupId = groupId,
			ModelId = modelId,
			AddedAt = DateTime.UtcNow
		});
	}

	public async Task RemoveMemberAsync(int groupId, int modelId)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM ModelGroupMembers WHERE GroupId = @GroupId AND ModelId = @ModelId", new
		{
			GroupId = groupId,
			ModelId = modelId
		});
	}

	public async Task SetMembersAsync(int groupId, IEnumerable<int> modelIds)
	{
		using DbConnection connection = _connectionFactory.CreateConnection();
		connection.Open();
		using DbTransaction transaction = connection.BeginTransaction();
		await connection.ExecuteAsync("DELETE FROM ModelGroupMembers WHERE GroupId = @GroupId", new
		{
			GroupId = groupId
		}, transaction);
		DateTime now = DateTime.UtcNow;
		foreach (int modelId in modelIds)
		{
			await connection.ExecuteAsync("INSERT INTO ModelGroupMembers (GroupId, ModelId, AddedAt)\r\nVALUES (@GroupId, @ModelId, @AddedAt)", new
			{
				GroupId = groupId,
				ModelId = modelId,
				AddedAt = now
			}, transaction);
		}
		transaction.Commit();
	}
}
