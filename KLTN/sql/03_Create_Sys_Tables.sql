-- ============================================================
-- SCRIPT KHỞI TẠO BẢNG QUẢN TRỊ & THEO DÕI ĐIỀU PHỐI ETL DWMS
-- Database: DWH_KhangNghi
-- ============================================================

USE DWH_KhangNghi;
GO

-- 1. BẢNG QUẢN TRỊ NGƯỜI DÙNG & PHÂN QUYỀN RBAC (SYS_USER)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sys_User')
BEGIN
    CREATE TABLE dbo.Sys_User (
        UserID INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(255) NOT NULL,
        FullName NVARCHAR(255) NOT NULL,
        Email NVARCHAR(255) NULL,
        Role NVARCHAR(50) NOT NULL DEFAULT 'Director', -- 'Admin', 'Director', 'SalesStaff', 'InventoryStaff'
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
END
GO

-- 2. BẢNG THEO DÕI TỪNG ĐỢT CHẠY ETL (SYS_ETLEXECUTION)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sys_EtlExecution')
BEGIN
    CREATE TABLE dbo.Sys_EtlExecution (
        ExecutionId INT IDENTITY(1,1) PRIMARY KEY,
        RunNumber INT NOT NULL,
        LoadType NVARCHAR(50) NOT NULL DEFAULT 'Incremental', -- 'Initial', 'Incremental'
        TriggeredBy NVARCHAR(100) NOT NULL DEFAULT 'Manual',  -- 'Manual', 'SQLAgent', 'AutoScheduler'
        StartTime DATETIME NOT NULL DEFAULT GETDATE(),
        EndTime DATETIME NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Running',       -- 'Running', 'Success', 'Failed'
        RetryAttempt INT NOT NULL DEFAULT 0,
        TotalRowsProcessed INT NOT NULL DEFAULT 0,
        ErrorMessage NVARCHAR(MAX) NULL
    );
END
GO

-- 3. BẢNG THEO DÕI CHI TIẾT TỪNG BƯỚC NẠP (SYS_ETLSTEPLOG)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sys_EtlStepLog')
BEGIN
    CREATE TABLE dbo.Sys_EtlStepLog (
        StepLogId INT IDENTITY(1,1) PRIMARY KEY,
        ExecutionId INT NOT NULL FOREIGN KEY REFERENCES dbo.Sys_EtlExecution(ExecutionId) ON DELETE CASCADE,
        StepNumber INT NOT NULL,
        StepName NVARCHAR(255) NOT NULL,
        PackageName NVARCHAR(255) NOT NULL,
        TargetTable NVARCHAR(100) NULL,
        StartTime DATETIME NOT NULL DEFAULT GETDATE(),
        EndTime DATETIME NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Running',       -- 'Running', 'Success', 'Failed'
        RowsProcessed INT NOT NULL DEFAULT 0,
        ErrorMessage NVARCHAR(MAX) NULL
    );
END
GO

-- 4. BẢNG CẤU HÌNH LỊCH CHẠY SQL SERVER AGENT (SYS_SCHEDULE)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sys_Schedule')
BEGIN
    CREATE TABLE dbo.Sys_Schedule (
        ScheduleID INT IDENTITY(1,1) PRIMARY KEY,
        JobName NVARCHAR(128) NOT NULL DEFAULT 'KhangNghi_ETL_Daily_Job',
        FrequencyType NVARCHAR(50) NOT NULL DEFAULT 'Daily',
        Hour INT NOT NULL DEFAULT 2,
        Minute INT NOT NULL DEFAULT 0,
        IsEnabled BIT NOT NULL DEFAULT 1,
        LastRunTime DATETIME NULL,
        NextRunTime DATETIME NULL,
        LastRunStatus NVARCHAR(50) NULL
    );
END
GO

-- Seed cấu hình lịch mặc định nếu chưa có
IF NOT EXISTS (SELECT 1 FROM dbo.Sys_Schedule WHERE JobName = 'KhangNghi_ETL_Daily_Job')
BEGIN
    INSERT INTO dbo.Sys_Schedule (JobName, FrequencyType, Hour, Minute, IsEnabled)
    VALUES ('KhangNghi_ETL_Daily_Job', 'Daily', 2, 0, 1);
END
GO
