using Microsoft.Data.SqlClient;

namespace YCC.SapAutomation.Infrastructure.Sql
{
  public interface IDbConnectionFactory
  {
    SqlConnection Create();
  }

  public sealed class SqlConnectionFactory : IDbConnectionFactory
  {
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
      _connectionString = connectionString;
    }

    public SqlConnection Create() => new SqlConnection(_connectionString);
  }
}
