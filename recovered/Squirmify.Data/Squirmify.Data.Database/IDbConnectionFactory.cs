using System.Data.Common;

namespace Squirmify.Data.Database;

public interface IDbConnectionFactory
{
	DbConnection CreateConnection();
}
