using System;
using System.Collections.Generic;

namespace KhangNghi.Core.DTOs
{
    // === AUTH DTOs ===
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class UserDto
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Role { get; set; } = "Director";
    }

    public class UpdateUserRequest
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Role { get; set; } = "Director";
        public bool IsActive { get; set; } = true;
        public string? NewPassword { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    // === DASHBOARD KPI SUMMARY ===
    public class DashboardSummaryDto
    {
        public double TotalRevenue { get; set; }
        public double TotalProfit { get; set; }
        public double ProfitMargin { get; set; }
        public int TotalOrders { get; set; }
        public int TotalQuantitySold { get; set; }
        public double TotalShippingCost { get; set; }
        public int LowStockAlertCount { get; set; }
        public int TotalCustomers { get; set; }
    }

    // === COMMON CHART DATA ITEM ===
    public class ChartItemDto
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public double? SecondaryValue { get; set; }
        public string? Category { get; set; }
    }

    // === INVENTORY ALERT DTO ===
    public class InventoryAlertDto
    {
        public string ProductID { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public int QuantityOnHand { get; set; }
        public int ReorderLevel { get; set; }
        public int Deficit { get; set; } // ReorderLevel - QuantityOnHand
        public string StatusColor { get; set; } = "Green"; // "Red", "Yellow", "Green"
        public string Recommendation { get; set; } = "Đủ tồn kho";
    }

    // === FORECAST DTOs (ML.NET) ===
    public class ForecastRequest
    {
        public int HorizonMonths { get; set; } = 6; // 3, 6, 12
        public string? Category { get; set; }
    }

    public class ForecastPointDto
    {
        public string Period { get; set; } = string.Empty; // e.g. "T10/2026"
        public double ActualValue { get; set; }
        public double ForecastValue { get; set; }
        public double LowerBound { get; set; }
        public double UpperBound { get; set; }
        public bool IsFuture { get; set; }
    }

    public class ForecastResponse
    {
        public string ModelName { get; set; } = "ML.NET SSA (Singular Spectrum Analysis)";
        public string TargetMetric { get; set; } = "Doanh thu (Sales)";
        public double MeanAbsoluteError { get; set; }
        public double RootMeanSquaredError { get; set; }
        public List<ForecastPointDto> Points { get; set; } = new();
    }

    // === DWH TABLE STATUS DTO ===
    public class DwhTableStatusDto
    {
        public string TableName { get; set; } = string.Empty;
        public string TableType { get; set; } = "Dimension"; // Dimension or Fact
        public long RowCount { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    // === ETL TRIGGER RESULT ===
    public class EtlResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int RowsProcessed { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
