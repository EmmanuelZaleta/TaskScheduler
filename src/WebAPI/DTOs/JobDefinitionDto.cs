namespace YCC.SapAutomation.WebAPI.DTOs;

public sealed class JobDefinitionDto
{
    public int JobId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OperationCode { get; set; } = string.Empty;
    public string? Command { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public bool ShowWindow { get; set; }
    public Dictionary<string, string>? Environment { get; set; }
    public string ScheduleType { get; set; } = "MINUTES";
    public int? IntervalMinutes { get; set; }
    public string? RunAtTime { get; set; }
    public byte? DaysOfWeekMask { get; set; }
    public bool Enabled { get; set; }
}

public sealed class CreateJobDto
{
    public string Name { get; set; } = string.Empty;
    public string OperationCode { get; set; } = string.Empty;
    public string? Command { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public bool ShowWindow { get; set; }
    public Dictionary<string, string>? Environment { get; set; }
    public string ScheduleType { get; set; } = "MINUTES";
    public int? IntervalMinutes { get; set; }
    public string? RunAtTime { get; set; }
    public byte? DaysOfWeekMask { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class UpdateJobDto
{
    public string? Name { get; set; }
    public string? OperationCode { get; set; }
    public string? Command { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public bool? ShowWindow { get; set; }
    public Dictionary<string, string>? Environment { get; set; }
    public string? ScheduleType { get; set; }
    public int? IntervalMinutes { get; set; }
    public string? RunAtTime { get; set; }
    public byte? DaysOfWeekMask { get; set; }
    public bool? Enabled { get; set; }
}

public sealed class JobRunDto
{
    public long JobRunId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public sealed class FileUploadResultDto
{
    public string FileName { get; set; } = string.Empty;
    public string ExtractedPath { get; set; } = string.Empty;
    public List<string> ExtractedFiles { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
