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

-- Staging para UPSERT (llenada con SqlBulkCopy)
IF OBJECT_ID('dbo.TQMBulk_In','U') IS NULL
BEGIN
  CREATE TABLE dbo.TQMBulk_In(
    HU        nvarchar(50) NOT NULL,
    StationNo nvarchar(20) NOT NULL,
    DateReg   date NOT NULL,
    TimeReg   time(0) NOT NULL,
    Material  nvarchar(20) NULL,
    MfgLnNum  nvarchar(20) NULL
  );
END

-- Tabla destino incremental
IF OBJECT_ID('dbo.TQMBulk_Tran','U') IS NULL
BEGIN
  CREATE TABLE dbo.TQMBulk_Tran(
    HU        nvarchar(50) NOT NULL,
    StationNo nvarchar(20) NOT NULL,
    DateReg   date NOT NULL,
    TimeReg   time(0) NOT NULL,
    Material  nvarchar(20) NULL,
    MfgLnNum  nvarchar(20) NULL,
    CONSTRAINT PK_TQMBulk_Tran PRIMARY KEY CLUSTERED (HU, StationNo, DateReg, TimeReg)
  );
END

-- Indices utiles
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TQMBulk_Tran_Station' AND object_id=OBJECT_ID('dbo.TQMBulk_Tran'))
  CREATE NONCLUSTERED INDEX IX_TQMBulk_Tran_Station ON dbo.TQMBulk_Tran(StationNo, DateReg, TimeReg) INCLUDE(Material, MfgLnNum);

/*
Notas:
- Usa PK compuesta para idempotencia por HU+Station+Fecha+Hora (como en tus SPs).
- Agrega mas columnas segun tu realidad (Ver, STD_Pack, etc.) y expandelas en el MERGE y el modelo.
*/
