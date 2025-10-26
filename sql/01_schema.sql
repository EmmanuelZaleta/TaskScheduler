/*
  Ejecutar en tu base (ej. NCDR_YCC_Moldeo) antes de correr el servicio.
*/
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID('dbo.JobRuns','U') IS NULL
BEGIN
  CREATE TABLE dbo.JobRuns(
    JobRunId     bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
    JobName      sysname NOT NULL,
    CorrelationId uniqueidentifier NOT NULL DEFAULT NEWSEQUENTIALID(),
    StartedUtc   datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    FinishedUtc  datetime2 NULL,
    Status       varchar(20) NOT NULL,
    Message      nvarchar(max) NULL
  );
END

IF OBJECT_ID('dbo.JobParam','U') IS NULL
BEGIN
  CREATE TABLE dbo.JobParam(
    ParamName  sysname NOT NULL PRIMARY KEY,
    ParamValue nvarchar(max) NULL
  );
END

IF OBJECT_ID('dbo.ExcludedHU','U') IS NULL
BEGIN
  CREATE TABLE dbo.ExcludedHU(
    HU         nvarchar(50) NOT NULL PRIMARY KEY,
    Reason     nvarchar(200) NULL,
    CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
  );
END

IF OBJECT_ID('dbo.Job','U') IS NULL
BEGIN
  CREATE TABLE dbo.Job(
    JobId            int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name             nvarchar(200) NOT NULL UNIQUE,
    OperationCode    nvarchar(100) NOT NULL,
    Enabled          bit NOT NULL DEFAULT 1,
    Command          nvarchar(500) NULL,
    Arguments        nvarchar(1000) NULL,
    WorkingDirectory nvarchar(500) NULL,
    ShowWindow       bit NOT NULL DEFAULT 0,
    Environment      nvarchar(max) NULL
  );
END

IF OBJECT_ID('dbo.JobSchedule','U') IS NULL
BEGIN
  CREATE TABLE dbo.JobSchedule(
    JobId            int NOT NULL PRIMARY KEY,
    ScheduleType     varchar(20) NOT NULL DEFAULT 'MINUTES',
    IntervalMinutes  int NULL,
    RunAtTime        time NULL,
    DaysOfWeekMask   tinyint NULL,
    FOREIGN KEY (JobId) REFERENCES dbo.Job(JobId) ON DELETE CASCADE
  );
END
