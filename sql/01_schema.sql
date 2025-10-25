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
