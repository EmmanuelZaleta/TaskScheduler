using Microsoft.Data.SqlClient;
using System.Data;
using YCC.SapAutomation.Application.Abstractions;
using YCC.SapAutomation.Infrastructure.Sql;

namespace YCC.SapAutomation.Infrastructure.Persistence
{
  public sealed class SqlJobRunStore : IJobRunStore
  {
    private readonly IDbConnectionFactory _connectionFactory;

    public SqlJobRunStore(IDbConnectionFactory connectionFactory) =>
      _connectionFactory = connectionFactory;

    public async Task<long> StartAsync(string jobName, CancellationToken cancellationToken = default)
    {
      await using var connection = _connectionFactory.Create();
      await connection.OpenAsync(cancellationToken);
      await using var command = connection.CreateCommand();

      command.CommandText = @"
INSERT INTO dbo.JobRuns (JobName, Status)
VALUES (@job, 'Running');
SELECT CAST(SCOPE_IDENTITY() AS bigint);";

      command.Parameters.Add(new SqlParameter("@job", SqlDbType.NVarChar, 128) { Value = jobName });

      var result = await command.ExecuteScalarAsync(cancellationToken);
      return Convert.ToInt64(result);
    }

    public async Task CompleteAsync(long jobRunId, string status, string? message = null, CancellationToken cancellationToken = default)
    {
      await using var connection = _connectionFactory.Create();
      await connection.OpenAsync(cancellationToken);
      await using var command = connection.CreateCommand();

      command.CommandText = @"
UPDATE dbo.JobRuns
SET Status = @status,
    FinishedUtc = SYSUTCDATETIME(),
    Message = @message
WHERE JobRunId = @id;";

      command.Parameters.Add(new SqlParameter("@status", SqlDbType.VarChar, 20) { Value = status });
      command.Parameters.Add(new SqlParameter("@message", SqlDbType.NVarChar, -1) { Value = (object?)message ?? DBNull.Value });
      command.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = jobRunId });

      await command.ExecuteNonQueryAsync(cancellationToken);
    }
  }
}
