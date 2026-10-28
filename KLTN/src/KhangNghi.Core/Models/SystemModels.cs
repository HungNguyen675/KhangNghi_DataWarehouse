using System;

namespace KhangNghi.Core.Models
{
    public class SysUser
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Role { get; set; } = "Director"; // Admin, Director, SalesStaff, InventoryStaff
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class SysEtlExecution
    {
        public int ExecutionId { get; set; }
        public int RunNumber { get; set; }
        public string LoadType { get; set; } = "Incremental"; // 'Initial', 'Incremental'
        public string TriggeredBy { get; set; } = "Manual";   // 'Manual', 'SQLAgent', 'AutoScheduler'
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = "Running";       // 'Running', 'Success', 'Failed'
        public int RetryAttempt { get; set; } = 0;
        public int TotalRowsProcessed { get; set; } = 0; // Unknown unless SSIS supplies run metrics.
        public long? FactRowCount { get; set; } // Snapshot after successful SSIS + SSAS.
        public bool HasRowProcessingMetrics { get; set; } = false;
        public string? ErrorMessage { get; set; }
    }

    public class SysEtlStepLog
    {
        public int StepLogId { get; set; }
        public int ExecutionId { get; set; }
        public int StepNumber { get; set; }
        public string StepName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string? TargetTable { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = "Running";       // 'Running', 'Success', 'Failed'
        public int RowsProcessed { get; set; } = 0;
        public string? ErrorMessage { get; set; }
    }

    public class SysEtlLog
    {
        public int LogID { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = "Running";
        public int RowsInserted { get; set; }
        public long? FactRowCount { get; set; }
        public bool HasRowProcessingMetrics { get; set; } = false;
        public string? ErrorMessage { get; set; }
        public string? TriggeredBy { get; set; }
    }

    public class SysSchedule
    {
        public int ScheduleID { get; set; }
        public string JobName { get; set; } = "KhangNghi_ETL_Daily_Job";
        public string FrequencyType { get; set; } = "Daily"; // Daily, Hourly, Weekly
        public int Hour { get; set; } = 2;
        public int Minute { get; set; } = 0;
        public bool IsEnabled { get; set; } = false;
        public DateTime? LastRunTime { get; set; }
        public DateTime? NextRunTime { get; set; }
        public string? LastRunStatus { get; set; }
    }
}
