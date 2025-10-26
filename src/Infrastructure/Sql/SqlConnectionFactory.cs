using Microsoft.Data.SqlClient;

namespace YCC.SapAutomation.Infrastructure.Sql
{
  public interface IDbConnectionFactory
  {
    SqlConnection Create();
  }

  public interface ISqlConnectionFactory
  {
    Task<SqlConnection> CreateAsync(CancellationToken cancellationToken = default);
  }

  public sealed class SqlConnectionFactory : IDbConnectionFactory, ISqlConnectionFactory
  {
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
      _connectionString = connectionString;
    }

    public SqlConnection Create() => new SqlConnection(_connectionString);

    public async Task<SqlConnection> CreateAsync(CancellationToken cancellationToken = default)
    {
      var connection = new SqlConnection(_connectionString);
      await connection.OpenAsync(cancellationToken);
      return connection;
    }
  }
}
