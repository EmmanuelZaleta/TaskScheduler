using Microsoft.Data.SqlClient;
using System.Data;
using YCC.SapAutomation.Application.Abstractions;
using YCC.SapAutomation.Infrastructure.Sql;

namespace YCC.SapAutomation.Infrastructure.Persistence
{
  public sealed class SqlExcludedHuStore : IExcludedHuStore
  {
    private readonly IDbConnectionFactory _connectionFactory;

    public SqlExcludedHuStore(IDbConnectionFactory connectionFactory) =>
      _connectionFactory = connectionFactory;

    public async Task<bool> IsExcludedAsync(string handlingUnit, CancellationToken cancellationToken = default)
    {
      await using var connection = _connectionFactory.Create();
      await connection.OpenAsync(cancellationToken);
      await using var command = connection.CreateCommand();

      command.CommandText = "SELECT 1 FROM dbo.ExcludedHU WITH (READPAST) WHERE HU = @hu";
      command.Parameters.Add(new SqlParameter("@hu", SqlDbType.NVarChar, 50) { Value = handlingUnit });

      var value = await command.ExecuteScalarAsync(cancellationToken);
      return value != null && value != DBNull.Value;
    }

    public async Task AddAsync(string handlingUnit, string reason, CancellationToken cancellationToken = default)
    {
      await using var connection = _connectionFactory.Create();
      await connection.OpenAsync(cancellationToken);
      await using var command = connection.CreateCommand();

      command.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM dbo.ExcludedHU WHERE HU = @hu)
BEGIN
  INSERT INTO dbo.ExcludedHU (HU, Reason)
  VALUES (@hu, @reason);
END";

      command.Parameters.Add(new SqlParameter("@hu", SqlDbType.NVarChar, 50) { Value = handlingUnit });
      command.Parameters.Add(new SqlParameter("@reason", SqlDbType.NVarChar, 200) { Value = reason });

      await command.ExecuteNonQueryAsync(cancellationToken);
    }
  }
}
