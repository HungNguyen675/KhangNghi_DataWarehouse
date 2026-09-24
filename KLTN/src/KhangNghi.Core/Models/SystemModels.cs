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
        public string Role { get; set; } = "Director"; // Admin, Director/GiamDoc, QuanLyKinhDoanh, QuanLyMuaHang, InventoryManager/QuanLyKho, ShippingStaff/QuanLyVanChuyen
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class SysEtlLog
    {
        public int LogID { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = "Running"; // Running, Success, Failed
        public int RowsInserted { get; set; }
        public string? ErrorMessage { get; set; }
        public string? TriggeredBy { get; set; }
    }

    public class SysSchedule
    {
        public int ScheduleID { get; set; }
        public string ScheduleName { get; set; } = "Default Daily ETL";
        public string FrequencyType { get; set; } = "Daily"; // Daily, Hourly, Weekly
        public string? CronExpression { get; set; }
        public int Hour { get; set; } = 0;
        public int Minute { get; set; } = 0;
        public bool IsEnabled { get; set; } = true;
        public DateTime? LastRunTime { get; set; }
        public DateTime? NextRunTime { get; set; }
    }
}
