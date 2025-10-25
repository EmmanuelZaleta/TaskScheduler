using Microsoft.Data.SqlClient;
using System.Data;
using YCC.SapAutomation.Application.Abstractions;
using YCC.SapAutomation.Domain.Tqmbulk;
using YCC.SapAutomation.Infrastructure.Sql;

namespace YCC.SapAutomation.Infrastructure.Persistence
{
  public sealed class SqlTqmbulkRepository : ITqmbulkRepository
  {
    private readonly IDbConnectionFactory _connectionFactory;

    public SqlTqmbulkRepository(IDbConnectionFactory connectionFactory) =>
      _connectionFactory = connectionFactory;

    public async Task UpsertAsync(IEnumerable<TqmbulkEntry> entries, CancellationToken cancellationToken = default)
    {
      var materializedEntries = entries as IList<TqmbulkEntry> ?? entries.ToList();
      if (materializedEntries.Count == 0)
      {
        return;
      }

      await using var connection = _connectionFactory.Create();
      await connection.OpenAsync(cancellationToken);

      await using (var truncate = connection.CreateCommand())
      {
        truncate.CommandText = "TRUNCATE TABLE dbo.TQMBulk_In";
        await truncate.ExecuteNonQueryAsync(cancellationToken);
      }

      using (var bulkCopy = new SqlBulkCopy(connection))
      {
        bulkCopy.DestinationTableName = "dbo.TQMBulk_In";
        bulkCopy.BulkCopyTimeout = 0;

        var table = new DataTable();
        table.Columns.Add("HU", typeof(string));
        table.Columns.Add("StationNo", typeof(string));
        table.Columns.Add("DateReg", typeof(DateTime));
        table.Columns.Add("TimeReg", typeof(TimeSpan));
        table.Columns.Add("Material", typeof(string));
        table.Columns.Add("MfgLnNum", typeof(string));

        foreach (var entry in materializedEntries)
        {
          table.Rows.Add(
            entry.HandlingUnit,
            entry.StationNumber,
            entry.RegistrationDate.ToDateTime(TimeOnly.MinValue),
            entry.RegistrationTime.ToTimeSpan(),
            entry.Material ?? (object)DBNull.Value,
            entry.ManufacturingLine ?? (object)DBNull.Value);
        }

        await bulkCopy.WriteToServerAsync(table, cancellationToken);
      }

      const string mergeSql = @"
MERGE dbo.TQMBulk_Tran AS T
USING (
  SELECT HU,
         StationNo,
         CAST(DateReg AS date) AS DateReg,
         CAST(TimeReg AS time(0)) AS TimeReg,
         Material,
         MfgLnNum
  FROM dbo.TQMBulk_In
) AS S
ON (T.HU = S.HU AND T.StationNo = S.StationNo AND T.DateReg = S.DateReg AND T.TimeReg = S.TimeReg)
WHEN MATCHED THEN
  UPDATE SET T.Material = S.Material, T.MfgLnNum = S.MfgLnNum
WHEN NOT MATCHED BY TARGET THEN
  INSERT (HU, StationNo, DateReg, TimeReg, Material, MfgLnNum)
  VALUES (S.HU, S.StationNo, S.DateReg, S.TimeReg, S.Material, S.MfgLnNum);";

      await using (var merge = connection.CreateCommand())
      {
        merge.CommandText = mergeSql;
        await merge.ExecuteNonQueryAsync(cancellationToken);
      }
    }
  }
}
