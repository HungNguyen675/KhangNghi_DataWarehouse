/*
Run manually in SSMS on HungNguyen after stopping Desktop/Web and ETL.
Copies application tables only. Does not delete or modify source rows.
Destination must have none of the four tables; do not run initializers first.
Passwords remain hashed; no account credentials are printed or reset.
Keep source tables until both applications have been switched and verified.
*/
USE [KhangNghi_AppManagement];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_ID(N'DWH_KhangNghi') IS NULL
    THROW 51000, 'Source database DWH_KhangNghi does not exist.', 1;

IF (SELECT collation_name FROM sys.databases WHERE name = DB_NAME()) <>
   (SELECT collation_name FROM sys.databases WHERE name = N'DWH_KhangNghi')
    THROW 51001, 'Database collations differ. Review before copying.', 1;

IF OBJECT_ID(N'dbo.Sys_User') IS NOT NULL
 OR OBJECT_ID(N'dbo.Sys_EtlExecution') IS NOT NULL
 OR OBJECT_ID(N'dbo.Sys_EtlStepLog') IS NOT NULL
 OR OBJECT_ID(N'dbo.Sys_Schedule') IS NOT NULL
 OR OBJECT_ID(N'dbo.Seq_EtlRunNumber') IS NOT NULL
    THROW 51002, 'Destination objects already exist. Stop and inspect; do not overwrite.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Hold the source tables stable while copying and comparing.
    DECLARE @SourceRows bigint;
    SELECT @SourceRows = COUNT_BIG(*) FROM DWH_KhangNghi.dbo.Sys_User WITH (TABLOCKX, HOLDLOCK);
    SELECT @SourceRows = COUNT_BIG(*) FROM DWH_KhangNghi.dbo.Sys_EtlExecution WITH (TABLOCKX, HOLDLOCK);
    SELECT @SourceRows = COUNT_BIG(*) FROM DWH_KhangNghi.dbo.Sys_EtlStepLog WITH (TABLOCKX, HOLDLOCK);
    SELECT @SourceRows = COUNT_BIG(*) FROM DWH_KhangNghi.dbo.Sys_Schedule WITH (TABLOCKX, HOLDLOCK);

    CREATE TABLE dbo.Sys_User (
        UserID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sys_User PRIMARY KEY,
        Username nvarchar(100) NOT NULL CONSTRAINT UQ_Sys_User_Username UNIQUE,
        PasswordHash nvarchar(255) NOT NULL,
        FullName nvarchar(255) NOT NULL,
        Email nvarchar(255) NULL,
        Role nvarchar(50) NOT NULL CONSTRAINT DF_Sys_User_Role DEFAULT (N'Director'),
        IsActive bit NOT NULL CONSTRAINT DF_Sys_User_IsActive DEFAULT (1),
        CreatedAt datetime NULL CONSTRAINT DF_Sys_User_CreatedAt DEFAULT (GETDATE())
    );

    CREATE TABLE dbo.Sys_EtlExecution (
        ExecutionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sys_EtlExecution PRIMARY KEY,
        RunNumber int NOT NULL,
        LoadType nvarchar(50) NOT NULL CONSTRAINT DF_Sys_EtlExecution_LoadType DEFAULT (N'Incremental'),
        TriggeredBy nvarchar(100) NOT NULL CONSTRAINT DF_Sys_EtlExecution_TriggeredBy DEFAULT (N'Manual'),
        StartTime datetime NOT NULL CONSTRAINT DF_Sys_EtlExecution_StartTime DEFAULT (GETDATE()),
        EndTime datetime NULL,
        Status nvarchar(50) NOT NULL CONSTRAINT DF_Sys_EtlExecution_Status DEFAULT (N'Running'),
        RetryAttempt int NOT NULL CONSTRAINT DF_Sys_EtlExecution_RetryAttempt DEFAULT (0),
        TotalRowsProcessed int NOT NULL CONSTRAINT DF_Sys_EtlExecution_TotalRows DEFAULT (0),
        ErrorMessage nvarchar(max) NULL,
        FactRowCount bigint NULL
    );

    CREATE TABLE dbo.Sys_EtlStepLog (
        StepLogId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sys_EtlStepLog PRIMARY KEY,
        ExecutionId int NOT NULL,
        StepNumber int NOT NULL,
        StepName nvarchar(255) NOT NULL,
        PackageName nvarchar(255) NOT NULL,
        TargetTable nvarchar(255) NULL,
        StartTime datetime NOT NULL CONSTRAINT DF_Sys_EtlStepLog_StartTime DEFAULT (GETDATE()),
        EndTime datetime NULL,
        Status nvarchar(50) NOT NULL CONSTRAINT DF_Sys_EtlStepLog_Status DEFAULT (N'Running'),
        RowsProcessed int NOT NULL CONSTRAINT DF_Sys_EtlStepLog_Rows DEFAULT (0),
        ErrorMessage nvarchar(max) NULL,
        CONSTRAINT FK_Sys_EtlStepLog_Execution FOREIGN KEY (ExecutionId)
            REFERENCES dbo.Sys_EtlExecution(ExecutionId)
    );

    CREATE TABLE dbo.Sys_Schedule (
        ScheduleID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sys_Schedule PRIMARY KEY,
        JobName nvarchar(128) NOT NULL CONSTRAINT DF_Sys_Schedule_JobName DEFAULT (N'KhangNghi_ETL_Daily_Job'),
        FrequencyType nvarchar(50) NOT NULL CONSTRAINT DF_Sys_Schedule_Frequency DEFAULT (N'Daily'),
        Hour int NOT NULL CONSTRAINT DF_Sys_Schedule_Hour DEFAULT (2),
        Minute int NOT NULL CONSTRAINT DF_Sys_Schedule_Minute DEFAULT (0),
        IsEnabled bit NOT NULL CONSTRAINT DF_Sys_Schedule_IsEnabled DEFAULT (1),
        LastRunTime datetime NULL,
        NextRunTime datetime NULL,
        LastRunStatus nvarchar(50) NULL,
        ScheduleName nvarchar(100) NULL
    );

    -- Explicit columns preserve values and tolerate source column ordering.
    SET IDENTITY_INSERT dbo.Sys_User ON;
    INSERT dbo.Sys_User (UserID,Username,PasswordHash,FullName,Email,Role,IsActive,CreatedAt)
    SELECT UserID,Username,PasswordHash,FullName,Email,Role,IsActive,CreatedAt
    FROM DWH_KhangNghi.dbo.Sys_User;
    SET IDENTITY_INSERT dbo.Sys_User OFF;

    SET IDENTITY_INSERT dbo.Sys_EtlExecution ON;
    INSERT dbo.Sys_EtlExecution (ExecutionId,RunNumber,LoadType,TriggeredBy,StartTime,EndTime,Status,RetryAttempt,TotalRowsProcessed,ErrorMessage,FactRowCount)
    SELECT ExecutionId,RunNumber,LoadType,TriggeredBy,StartTime,EndTime,Status,RetryAttempt,TotalRowsProcessed,ErrorMessage,FactRowCount
    FROM DWH_KhangNghi.dbo.Sys_EtlExecution;
    SET IDENTITY_INSERT dbo.Sys_EtlExecution OFF;

    SET IDENTITY_INSERT dbo.Sys_EtlStepLog ON;
    INSERT dbo.Sys_EtlStepLog (StepLogId,ExecutionId,StepNumber,StepName,PackageName,TargetTable,StartTime,EndTime,Status,RowsProcessed,ErrorMessage)
    SELECT StepLogId,ExecutionId,StepNumber,StepName,PackageName,TargetTable,StartTime,EndTime,Status,RowsProcessed,ErrorMessage
    FROM DWH_KhangNghi.dbo.Sys_EtlStepLog;
    SET IDENTITY_INSERT dbo.Sys_EtlStepLog OFF;

    SET IDENTITY_INSERT dbo.Sys_Schedule ON;
    INSERT dbo.Sys_Schedule (ScheduleID,JobName,FrequencyType,Hour,Minute,IsEnabled,LastRunTime,NextRunTime,LastRunStatus,ScheduleName)
    SELECT ScheduleID,JobName,FrequencyType,Hour,Minute,IsEnabled,LastRunTime,NextRunTime,LastRunStatus,ScheduleName
    FROM DWH_KhangNghi.dbo.Sys_Schedule;
    SET IDENTITY_INSERT dbo.Sys_Schedule OFF;

    -- Compare all copied columns in both directions without displaying them.
    DECLARE @Tables table (TableName sysname, ColumnsSql nvarchar(max));
    INSERT @Tables VALUES
    (N'Sys_User', N'UserID,Username,PasswordHash,FullName,Email,Role,IsActive,CreatedAt'),
    (N'Sys_EtlExecution', N'ExecutionId,RunNumber,LoadType,TriggeredBy,StartTime,EndTime,Status,RetryAttempt,TotalRowsProcessed,ErrorMessage,FactRowCount'),
    (N'Sys_EtlStepLog', N'StepLogId,ExecutionId,StepNumber,StepName,PackageName,TargetTable,StartTime,EndTime,Status,RowsProcessed,ErrorMessage'),
    (N'Sys_Schedule', N'ScheduleID,JobName,FrequencyType,Hour,Minute,IsEnabled,LastRunTime,NextRunTime,LastRunStatus,ScheduleName');

    DECLARE @TableName sysname, @Columns nvarchar(max), @Sql nvarchar(max), @IdentityLast bigint;
    DECLARE TableCursor CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, ColumnsSql FROM @Tables;
    OPEN TableCursor;
    FETCH NEXT FROM TableCursor INTO @TableName, @Columns;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'IF EXISTS (SELECT ' + @Columns + N' FROM DWH_KhangNghi.dbo.' + QUOTENAME(@TableName)
            + N' EXCEPT SELECT ' + @Columns + N' FROM dbo.' + QUOTENAME(@TableName) + N')'
            + N' OR EXISTS (SELECT ' + @Columns + N' FROM dbo.' + QUOTENAME(@TableName)
            + N' EXCEPT SELECT ' + @Columns + N' FROM DWH_KhangNghi.dbo.' + QUOTENAME(@TableName) + N')'
            + N' THROW 51003, ''Source and destination data differ.'', 1;';
        EXEC sys.sp_executesql @Sql;

        SELECT @IdentityLast = CONVERT(bigint, c.last_value)
        FROM DWH_KhangNghi.sys.identity_columns c
        JOIN DWH_KhangNghi.sys.tables t ON t.object_id = c.object_id
        JOIN DWH_KhangNghi.sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = N'dbo' AND t.name = @TableName;
        IF @IdentityLast IS NOT NULL
        BEGIN
            SET @Sql = N'DBCC CHECKIDENT (N''dbo.' + @TableName + N''', RESEED, '
                + CONVERT(nvarchar(30), @IdentityLast) + N') WITH NO_INFOMSGS;';
            EXEC sys.sp_executesql @Sql;
        END;
        FETCH NEXT FROM TableCursor INTO @TableName, @Columns;
    END;
    CLOSE TableCursor;
    DEALLOCATE TableCursor;

    DECLARE @SequenceNext bigint = (SELECT ISNULL(MAX(CONVERT(bigint, RunNumber)), 0) + 1 FROM dbo.Sys_EtlExecution);
    DECLARE @SourceSequenceNext bigint = (
        SELECT CONVERT(bigint, current_value) + CONVERT(bigint, increment)
        FROM DWH_KhangNghi.sys.sequences
        WHERE name = N'Seq_EtlRunNumber' AND schema_id = SCHEMA_ID(N'dbo')
    );
    IF @SourceSequenceNext > @SequenceNext SET @SequenceNext = @SourceSequenceNext;
    SET @Sql = N'CREATE SEQUENCE dbo.Seq_EtlRunNumber AS int START WITH '
        + CONVERT(nvarchar(30), @SequenceNext) + N' INCREMENT BY 1;';
    EXEC sys.sp_executesql @Sql;

    COMMIT TRANSACTION;
    PRINT 'PASS: application data copied and compared. DWH source tables retained. Application connections unchanged.';

    SELECT N'Sys_User' AS TableName, COUNT_BIG(*) AS CopiedRows FROM dbo.Sys_User
    UNION ALL SELECT N'Sys_EtlExecution', COUNT_BIG(*) FROM dbo.Sys_EtlExecution
    UNION ALL SELECT N'Sys_EtlStepLog', COUNT_BIG(*) FROM dbo.Sys_EtlStepLog
    UNION ALL SELECT N'Sys_Schedule', COUNT_BIG(*) FROM dbo.Sys_Schedule;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
