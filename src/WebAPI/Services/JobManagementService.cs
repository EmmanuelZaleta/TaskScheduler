using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using YCC.SapAutomation.Abstractions.DbScheduling;
using YCC.SapAutomation.Infrastructure.Sql;
using YCC.SapAutomation.WebAPI.DTOs;
using Newtonsoft.Json;

namespace YCC.SapAutomation.WebAPI.Services;

public sealed class JobManagementService : IJobManagementService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<JobManagementService> _logger;

    public JobManagementService(
        ISqlConnectionFactory connectionFactory,
        ILogger<JobManagementService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<JobDefinitionDto>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);

        var sql = @"
            SELECT
                j.JobId, j.Name, j.OperationCode, j.Enabled,
                j.Command, j.Arguments, j.WorkingDirectory, j.ShowWindow, j.Environment,
                s.ScheduleType, s.IntervalMinutes, s.RunAtTime, s.DaysOfWeekMask
            FROM dbo.Job j
            LEFT JOIN dbo.JobSchedule s ON j.JobId = s.JobId
            ORDER BY j.Name";

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var jobs = new List<JobDefinitionDto>();

        while (await reader.ReadAsync(cancellationToken))
        {
            jobs.Add(MapToDto(reader));
        }

        await LoadJobParamsAsync(connection, jobs, cancellationToken);

        return jobs;
    }

    public async Task<JobDefinitionDto?> GetJobByIdAsync(int jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);

        var sql = @"
            SELECT
                j.JobId, j.Name, j.OperationCode, j.Enabled,
                j.Command, j.Arguments, j.WorkingDirectory, j.ShowWindow, j.Environment,
                s.ScheduleType, s.IntervalMinutes, s.RunAtTime, s.DaysOfWeekMask
            FROM dbo.Job j
            LEFT JOIN dbo.JobSchedule s ON j.JobId = s.JobId
            WHERE j.JobId = @JobId";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@JobId", jobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            var job = MapToDto(reader);
            await LoadJobParamsAsync(connection, new[] { job }, cancellationToken);
            return job;
        }

        return null;
    }

    public async Task<JobDefinitionDto> CreateJobAsync(CreateJobDto createDto, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);

        var jobId = 0;

        // Insert Job
        var insertJobSql = @"
            INSERT INTO Job (Name, OperationCode, Enabled, Command, Arguments, WorkingDirectory, ShowWindow, Environment)
            OUTPUT INSERTED.JobId
            VALUES (@Name, @OperationCode, @Enabled, @Command, @Arguments, @WorkingDirectory, @ShowWindow, @Environment)";

        await using (var command = new SqlCommand(insertJobSql, connection))
        {
            command.Parameters.AddWithValue("@Name", createDto.Name);
            command.Parameters.AddWithValue("@OperationCode", createDto.OperationCode);
            command.Parameters.AddWithValue("@Enabled", createDto.Enabled);
            command.Parameters.AddWithValue("@Command", (object?)createDto.Command ?? DBNull.Value);
            command.Parameters.AddWithValue("@Arguments", (object?)createDto.Arguments ?? DBNull.Value);
            command.Parameters.AddWithValue("@WorkingDirectory", (object?)createDto.WorkingDirectory ?? DBNull.Value);
            command.Parameters.AddWithValue("@ShowWindow", createDto.ShowWindow);

            var envJson = createDto.Environment != null
                ? JsonConvert.SerializeObject(createDto.Environment)
                : null;
            command.Parameters.AddWithValue("@Environment", (object?)envJson ?? DBNull.Value);

            jobId = (int)await command.ExecuteScalarAsync(cancellationToken);
        }

        // Insert JobSchedule
        var insertScheduleSql = @"
            INSERT INTO JobSchedule (JobId, ScheduleType, IntervalMinutes, RunAtTime, DaysOfWeekMask)
            VALUES (@JobId, @ScheduleType, @IntervalMinutes, @RunAtTime, @DaysOfWeekMask)";

        await using (var command = new SqlCommand(insertScheduleSql, connection))
        {
            command.Parameters.AddWithValue("@JobId", jobId);
            command.Parameters.AddWithValue("@ScheduleType", createDto.ScheduleType);
            command.Parameters.AddWithValue("@IntervalMinutes", (object?)createDto.IntervalMinutes ?? DBNull.Value);
            command.Parameters.AddWithValue("@RunAtTime", (object?)createDto.RunAtTime ?? DBNull.Value);
            command.Parameters.AddWithValue("@DaysOfWeekMask", (object?)createDto.DaysOfWeekMask ?? DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("Created job {JobId}: {JobName}", jobId, createDto.Name);

        return (await GetJobByIdAsync(jobId, cancellationToken))!;
    }

    public async Task<JobDefinitionDto?> UpdateJobAsync(int jobId, UpdateJobDto updateDto, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);

        var updates = new List<string>();
        var command = new SqlCommand { Connection = connection };

        if (updateDto.Name != null)
        {
            updates.Add("Name = @Name");
            command.Parameters.AddWithValue("@Name", updateDto.Name);
        }

        if (updateDto.OperationCode != null)
        {
            updates.Add("OperationCode = @OperationCode");
            command.Parameters.AddWithValue("@OperationCode", updateDto.OperationCode);
        }

        if (updateDto.Enabled.HasValue)
        {
            updates.Add("Enabled = @Enabled");
            command.Parameters.AddWithValue("@Enabled", updateDto.Enabled.Value);
        }

        if (updateDto.Command != null)
        {
            updates.Add("Command = @Command");
            command.Parameters.AddWithValue("@Command", updateDto.Command);
        }

        if (updateDto.Arguments != null)
        {
            updates.Add("Arguments = @Arguments");
            command.Parameters.AddWithValue("@Arguments", updateDto.Arguments);
        }

        if (updateDto.WorkingDirectory != null)
        {
            updates.Add("WorkingDirectory = @WorkingDirectory");
            command.Parameters.AddWithValue("@WorkingDirectory", updateDto.WorkingDirectory);
        }

        if (updateDto.ShowWindow.HasValue)
        {
            updates.Add("ShowWindow = @ShowWindow");
            command.Parameters.AddWithValue("@ShowWindow", updateDto.ShowWindow.Value);
        }

        if (updateDto.Environment != null)
        {
            updates.Add("Environment = @Environment");
            var envJson = JsonConvert.SerializeObject(updateDto.Environment);
            command.Parameters.AddWithValue("@Environment", envJson);
        }

        if (updates.Count > 0)
        {
            command.CommandText = $"UPDATE Job SET {string.Join(", ", updates)} WHERE JobId = @JobId";
            command.Parameters.AddWithValue("@JobId", jobId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // Update schedule if needed
        var scheduleUpdates = new List<string>();
        var scheduleCommand = new SqlCommand { Connection = connection };

        if (updateDto.ScheduleType != null)
        {
            scheduleUpdates.Add("ScheduleType = @ScheduleType");
            scheduleCommand.Parameters.AddWithValue("@ScheduleType", updateDto.ScheduleType);
        }

        if (updateDto.IntervalMinutes.HasValue)
        {
            scheduleUpdates.Add("IntervalMinutes = @IntervalMinutes");
            scheduleCommand.Parameters.AddWithValue("@IntervalMinutes", updateDto.IntervalMinutes.Value);
        }

        if (updateDto.RunAtTime != null)
        {
            scheduleUpdates.Add("RunAtTime = @RunAtTime");
            scheduleCommand.Parameters.AddWithValue("@RunAtTime", updateDto.RunAtTime);
        }

        if (updateDto.DaysOfWeekMask.HasValue)
        {
            scheduleUpdates.Add("DaysOfWeekMask = @DaysOfWeekMask");
            scheduleCommand.Parameters.AddWithValue("@DaysOfWeekMask", updateDto.DaysOfWeekMask.Value);
        }

        if (scheduleUpdates.Count > 0)
        {
            scheduleCommand.CommandText = $"UPDATE JobSchedule SET {string.Join(", ", scheduleUpdates)} WHERE JobId = @JobId";
            scheduleCommand.Parameters.AddWithValue("@JobId", jobId);
            await scheduleCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("Updated job {JobId}", jobId);

        return await GetJobByIdAsync(jobId, cancellationToken);
    }

    public async Task<bool> DeleteJobAsync(int jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);

        // Delete schedule first (FK constraint)
        var deleteScheduleSql = "DELETE FROM JobSchedule WHERE JobId = @JobId";
        await using (var command = new SqlCommand(deleteScheduleSql, connection))
        {
            command.Parameters.AddWithValue("@JobId", jobId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // Delete job
        var deleteJobSql = "DELETE FROM Job WHERE JobId = @JobId";
        await using (var command = new SqlCommand(deleteJobSql, connection))
        {
            command.Parameters.AddWithValue("@JobId", jobId);
            var rows = await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Deleted job {JobId}", jobId);
            return rows > 0;
        }
    }

    public async Task<IEnumerable<JobRunDto>> GetJobRunsAsync(int? jobId = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);

        var sql = @"
            SELECT TOP (@Limit)
                JobRunId, JobName, CorrelationId, StartedUtc, FinishedUtc, Status, Message
            FROM JobRuns
            WHERE (@JobId IS NULL OR JobName = (SELECT Name FROM Job WHERE JobId = @JobId))
            ORDER BY StartedUtc DESC";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);
        command.Parameters.AddWithValue("@JobId", (object?)jobId ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var runs = new List<JobRunDto>();

        while (await reader.ReadAsync(cancellationToken))
        {
            runs.Add(new JobRunDto
            {
                JobRunId = reader.GetInt64(0),
                JobName = reader.GetString(1),
                CorrelationId = reader.GetGuid(2),
                StartedUtc = reader.GetDateTime(3),
                FinishedUtc = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                Status = reader.GetString(5),
                Message = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return runs;
    }

    private static async Task LoadJobParamsAsync(SqlConnection connection, IReadOnlyCollection<JobDefinitionDto> jobs, CancellationToken cancellationToken)
    {
        if (jobs.Count == 0)
        {
            return;
        }

        var jobArray = jobs.ToArray();
        var parameterNames = jobArray
            .Select((job, index) => new { job.JobId, ParameterName = $"@JobId{index}" })
            .ToArray();

        var sql = $@"
            SELECT JobId, [Key], [Value]
            FROM dbo.JobParam
            WHERE JobId IN ({string.Join(", ", parameterNames.Select(p => p.ParameterName))})";

        await using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameterNames)
        {
            command.Parameters.AddWithValue(parameter.ParameterName, parameter.JobId);
        }

        var jobsById = jobArray.ToDictionary(job => job.JobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var jobId = reader.GetInt32(0);
            if (!jobsById.TryGetValue(jobId, out var job))
            {
                continue;
            }

            var key = reader.GetString(1);
            var value = reader.IsDBNull(2) ? null : reader.GetString(2);

            ApplyJobParam(job, key, value);
        }
    }

    private static void ApplyJobParam(JobDefinitionDto job, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (key.StartsWith("Env:", StringComparison.OrdinalIgnoreCase))
        {
            if (value is null)
            {
                return;
            }

            job.Environment ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            job.Environment[key.Substring(4)] = value;
            return;
        }

        if (key.Equals("Command", StringComparison.OrdinalIgnoreCase))
        {
            job.Command = value;
            return;
        }

        if (key.Equals("Arguments", StringComparison.OrdinalIgnoreCase))
        {
            job.Arguments = value;
            return;
        }

        if (key.Equals("WorkingDirectory", StringComparison.OrdinalIgnoreCase))
        {
            job.WorkingDirectory = value;
            return;
        }

        if (key.Equals("Environment", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(value);
                if (parsed is null)
                {
                    return;
                }

                job.Environment ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in parsed)
                {
                    job.Environment[kvp.Key] = kvp.Value;
                }
            }
            catch (JsonException)
            {
                // Ignore invalid JSON stored in parameters
            }

            return;
        }

        if (key.Equals("ShowWindow", StringComparison.OrdinalIgnoreCase))
        {
            if (value is null)
            {
                job.ShowWindow = false;
                return;
            }

            if (bool.TryParse(value, out var boolValue))
            {
                job.ShowWindow = boolValue;
                return;
            }

            if (int.TryParse(value, out var intValue))
            {
                job.ShowWindow = intValue != 0;
            }

            return;
        }
    }

    private static JobDefinitionDto MapToDto(SqlDataReader reader)
    {
        var envJson = reader.IsDBNull(reader.GetOrdinal("Environment"))
            ? null
            : reader.GetString(reader.GetOrdinal("Environment"));

        var environment = !string.IsNullOrEmpty(envJson)
            ? JsonConvert.DeserializeObject<Dictionary<string, string>>(envJson)
            : null;

        return new JobDefinitionDto
        {
            JobId = reader.GetInt32(reader.GetOrdinal("JobId")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            OperationCode = reader.GetString(reader.GetOrdinal("OperationCode")),
            Enabled = reader.GetBoolean(reader.GetOrdinal("Enabled")),
            Command = reader.IsDBNull(reader.GetOrdinal("Command")) ? null : reader.GetString(reader.GetOrdinal("Command")),
            Arguments = reader.IsDBNull(reader.GetOrdinal("Arguments")) ? null : reader.GetString(reader.GetOrdinal("Arguments")),
            WorkingDirectory = reader.IsDBNull(reader.GetOrdinal("WorkingDirectory")) ? null : reader.GetString(reader.GetOrdinal("WorkingDirectory")),
            ShowWindow = reader.GetBoolean(reader.GetOrdinal("ShowWindow")),
            Environment = environment,
            ScheduleType = reader.IsDBNull(reader.GetOrdinal("ScheduleType")) ? "MINUTES" : reader.GetString(reader.GetOrdinal("ScheduleType")),
            IntervalMinutes = reader.IsDBNull(reader.GetOrdinal("IntervalMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("IntervalMinutes")),
            RunAtTime = reader.IsDBNull(reader.GetOrdinal("RunAtTime")) ? null : reader.GetTimeSpan(reader.GetOrdinal("RunAtTime")).ToString(),
            DaysOfWeekMask = reader.IsDBNull(reader.GetOrdinal("DaysOfWeekMask")) ? null : reader.GetByte(reader.GetOrdinal("DaysOfWeekMask"))
        };
    }
}
