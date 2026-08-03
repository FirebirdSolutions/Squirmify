using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Squirmify.Data.Database;

public class SqliteConnectionFactory : IDbConnectionFactory
{
	private readonly string _connectionString;

	public SqliteConnectionFactory(string connectionString)
	{
		_connectionString = connectionString;
	}

	public DbConnection CreateConnection()
	{
		return new SqliteConnection(_connectionString);
	}
}
