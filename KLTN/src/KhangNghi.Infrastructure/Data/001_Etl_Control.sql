-- Review and execute manually on a TEST copy of the intended DWH first.
-- Uses the current database; does not alter any dimension/fact or run ETL.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock @Resource=N'KhangNghi.DWMS.ETL',
    @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=0;
IF @lockResult < 0 THROW 51000, 'ETL is active or migration lock cannot be acquired.', 1;
IF OBJECT_ID(N'dbo.Sys_EtlExecution', N'U') IS NULL
CREATE TABLE dbo.Sys_EtlExecution (
    ExecutionId int IDENTITY PRIMARY KEY,
    RunNumber int NOT NULL UNIQUE,
    LoadType nvarchar(50) NOT NULL,
    TriggeredBy nvarchar(100) NOT NULL,
    StartTime datetime2 NOT NULL,
    EndTime datetime2 NULL,
    Status nvarchar(50) NOT NULL,
    RetryAttempt int NOT NULL DEFAULT 0,
    TotalRowsProcessed int NOT NULL DEFAULT 0,
    ErrorMessage nvarchar(max) NULL
);
IF OBJECT_ID(N'dbo.Seq_EtlRunNumber', N'SO') IS NULL
BEGIN
    DECLARE @next bigint = (SELECT ISNULL(MAX(CONVERT(bigint,RunNumber)),0)+1 FROM dbo.Sys_EtlExecution);
    DECLARE @sql nvarchar(max) = N'CREATE SEQUENCE dbo.Seq_EtlRunNumber AS int START WITH ' + CONVERT(nvarchar(20),@next) + N' INCREMENT BY 1;';
    EXEC sys.sp_executesql @sql;
END;
IF OBJECT_ID(N'dbo.Sys_EtlStepLog', N'U') IS NULL
CREATE TABLE dbo.Sys_EtlStepLog (
    StepLogId int IDENTITY PRIMARY KEY,
    ExecutionId int NOT NULL REFERENCES dbo.Sys_EtlExecution(ExecutionId),
    StepNumber int NOT NULL,
    StepName nvarchar(255) NOT NULL,
    PackageName nvarchar(255) NOT NULL,
    TargetTable nvarchar(255) NULL,
    StartTime datetime2 NOT NULL,
    EndTime datetime2 NULL,
    Status nvarchar(50) NOT NULL,
    RowsProcessed int NOT NULL DEFAULT 0,
    ErrorMessage nvarchar(max) NULL,
    UNIQUE(ExecutionId, StepNumber)
);
IF OBJECT_ID(N'dbo.Sys_Schedule', N'U') IS NULL
CREATE TABLE dbo.Sys_Schedule (
    ScheduleID int IDENTITY PRIMARY KEY,
    ScheduleName nvarchar(100) NOT NULL,
    JobName nvarchar(128) NULL,
    FrequencyType nvarchar(50) NOT NULL,
    Hour int NOT NULL DEFAULT 2,
    Minute int NOT NULL DEFAULT 0,
    IsEnabled bit NOT NULL DEFAULT 0,
    LastRunTime datetime2 NULL,
    NextRunTime datetime2 NULL,
    LastRunStatus nvarchar(50) NULL
);
IF COL_LENGTH(N'dbo.Sys_Schedule', N'JobName') IS NULL
    ALTER TABLE dbo.Sys_Schedule ADD JobName nvarchar(128) NULL;
IF COL_LENGTH(N'dbo.Sys_Schedule', N'LastRunStatus') IS NULL
    ALTER TABLE dbo.Sys_Schedule ADD LastRunStatus nvarchar(50) NULL;
IF COL_LENGTH(N'dbo.Sys_EtlExecution', N'FactRowCount') IS NULL
    ALTER TABLE dbo.Sys_EtlExecution ADD FactRowCount bigint NULL;
COMMIT;
