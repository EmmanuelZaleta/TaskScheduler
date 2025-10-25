using Microsoft.Data.SqlClient;
using System.Data;
using YCC.SapAutomation.Abstractions.DbScheduling;
using YCC.SapAutomation.Infrastructure.Sql;

namespace YCC.SapAutomation.Infrastructure.Persistence;

public sealed class SqlJobDefinitionStore : IJobDefinitionStore
{
  private readonly IDbConnectionFactory _factory;
  public SqlJobDefinitionStore(IDbConnectionFactory factory) => _factory = factory;

  public async Task<IReadOnlyCollection<JobDefinition>> LoadEnabledAsync(CancellationToken cancellationToken)
  {
    var list = new List<JobDefinition>();

    await using var cn = _factory.Create();
    await cn.OpenAsync(cancellationToken);

    var jobsCmd = cn.CreateCommand();
    jobsCmd.CommandText = @"
SELECT j.JobId, j.Name, j.OperationCode,
       js.ScheduleType, js.IntervalMinutes, js.RunAtTime, js.DaysOfWeekMask
FROM dbo.Job j
JOIN dbo.JobSchedule js ON js.JobId = j.JobId
WHERE j.Enabled = 1";
    jobsCmd.CommandType = CommandType.Text;

    var jobs = new List<(int JobId,string Name,string Op,string SType,int? Interval, TimeSpan? RunAt, byte? Mask)>();
    await using (var rd = await jobsCmd.ExecuteReaderAsync(cancellationToken))
    {
      while (await rd.ReadAsync(cancellationToken))
      {
        int jobId = rd.GetInt32(0);
        string name = rd.GetString(1);
        string op = rd.GetString(2);
        string sType = rd.GetString(3);
        int? interval = rd.IsDBNull(4) ? (int?)null : rd.GetInt32(4);
        TimeSpan? runAt = rd.IsDBNull(5) ? (TimeSpan?)null : (TimeSpan)rd[5];
        byte? mask = rd.IsDBNull(6) ? (byte?)null : (byte)rd[6];

        jobs.Add((jobId,name,op,sType,interval,runAt,mask));
      }
    }

    foreach (var j in jobs)
    {
      var paramCmd = cn.CreateCommand();
      paramCmd.CommandText = "SELECT [Key],[Value] FROM dbo.JobParam WHERE JobId=@id";
      paramCmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int){Value=j.JobId});
      var env = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
      string? cmd = null; string? args = null; string? wd = null; bool show = false;

      await using (var rd = await paramCmd.ExecuteReaderAsync(cancellationToken))
      {
        while (await rd.ReadAsync(cancellationToken))
        {
          var key = rd.GetString(0);
          var val = rd.IsDBNull(1) ? null : rd.GetString(1);
          if (key.Equals("Command", StringComparison.OrdinalIgnoreCase)) cmd = val;
          else if (key.Equals("Arguments", StringComparison.OrdinalIgnoreCase)) args = val;
          else if (key.Equals("WorkingDirectory", StringComparison.OrdinalIgnoreCase)) wd = val;
          else if (key.Equals("ShowWindow", StringComparison.OrdinalIgnoreCase)) bool.TryParse(val, out show);
          else if (key.StartsWith("Env:", StringComparison.OrdinalIgnoreCase) && val is not null)
          {
            env[key.Substring(4)] = val;
          }
        }
      }

      list.Add(new JobDefinition
      {
        JobId = j.JobId,
        Name = j.Name,
        OperationCode = j.Op,
        ScheduleType = j.SType,
        IntervalMinutes = j.Interval,
        RunAtTime = j.RunAt,
        DaysOfWeekMask = j.Mask,
        Command = cmd,
        Arguments = args,
        WorkingDirectory = wd,
        ShowWindow = show,
        Environment = env
      });
    }

    return list;
  }
}

