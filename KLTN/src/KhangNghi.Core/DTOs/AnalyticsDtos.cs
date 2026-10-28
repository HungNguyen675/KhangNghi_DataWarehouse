using System;
using System.Collections.Generic;

namespace KhangNghi.Core.DTOs
{
    // === AUTHENTICATION DTOs ===
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
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

    // === FORECAST DTOs (ModelKLTN Hybrid Optimal) ===
    public class ForecastRequest
    {
        public string Granularity { get; set; } = "month"; // "day", "week", "month"
        public int HorizonMonths { get; set; } = 6; // 3, 6, 12, 24 (hoặc số đơn vị kỳ hạn)
        public string? StartDate { get; set; } // YYYY-MM-DD
        public string? EndDate { get; set; }   // YYYY-MM-DD
        public string? Category { get; set; }
    }

    public class ForecastPointDto
    {
        public string Period { get; set; } = string.Empty; // e.g. "T10/2026"
        public string DateKey { get; set; } = string.Empty; // YYYY-MM-DD
        public string SubLabel { get; set; } = string.Empty; // Chi tiết Thứ hoặc khoảng ngày
        public double ActualValue { get; set; }
        public double SamePeriodActualValue { get; set; } // Doanh thu thực tế cùng kỳ năm trước (2025)
        public double ForecastValue { get; set; }
        public double LowerBound { get; set; }
        public double UpperBound { get; set; }
        public bool IsFuture { get; set; }
    }

    public class ForecastResponse
    {
        public string ModelName { get; set; } = "Hybrid Optimal (Prophet + XGBoost + CatBoost)";
        public string TargetMetric { get; set; } = "Doanh thu (Sales)";
        public string Granularity { get; set; } = "month";
        public string ForecastStart { get; set; } = "2026-01-01";
        public string ForecastEnd { get; set; } = "2027-12-31";
        public string SelectedStart { get; set; } = string.Empty;
        public string SelectedEnd { get; set; } = string.Empty;
        public double TotalActualRevenue { get; set; }
        public double TotalForecastRevenue { get; set; }
        public double AveragePeriodRevenue { get; set; }
        public string PeakPeriodLabel { get; set; } = string.Empty;
        public double PeakPeriodRevenue { get; set; }
        public double MeanAbsoluteError { get; set; }
        public double RootMeanSquaredError { get; set; }
        public List<ForecastPointDto> Points { get; set; } = new();
    }

    public class MonthDrilldownResponse
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel { get; set; } = string.Empty;
        public bool IsFuture { get; set; }
        public double TotalActual { get; set; }
        public double TotalSamePeriodActual { get; set; }
        public double TotalForecast { get; set; }
        public double AverageWeeklyForecast { get; set; }
        public double AverageDailyForecast { get; set; }
        public List<ForecastPointDto> Weeks { get; set; } = new();
        public List<ForecastPointDto> Days { get; set; } = new();
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
        public long? FactRowCount { get; set; }
        public bool HasRowProcessingMetrics { get; set; } = false;
        public int RowsProcessed { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    // === SQL AGENT JOB HISTORY DTO ===
    public class JobHistoryDto
    {
        public int InstanceId { get; set; }
        public string JobName { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public DateTime RunDateTime { get; set; }
        public string RunStatus { get; set; } = string.Empty; // Succeeded, Failed, Retried
        public TimeSpan RunDuration { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    // === PURCHASE ANALYTICS DTOs ===
    public class PurchaseOrderSummaryDto
    {
        public string PurchaseOrderID { get; set; } = string.Empty;
        public string OrderDate { get; set; } = string.Empty;
        public int SupplierKey { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int WarehouseKey { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public int LineItemCount { get; set; }
        public int TotalQuantity { get; set; }
        public double TotalCost { get; set; }
        public double AvgUnitCost { get; set; }
    }

    public class PurchaseOrderDetailDto
    {
        public string PurchaseOrderID { get; set; } = string.Empty;
        public string OrderDate { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string ProductID { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public int PurchaseQuantity { get; set; }
        public double UnitCost { get; set; }
        public double TotalCost { get; set; }
    }

    public class PurchaseCategorySpendDto
    {
        public string Category { get; set; } = string.Empty;
        public double TotalCost { get; set; }
        public int TotalQuantity { get; set; }
    }

    // === SSAS OLAP & MDX DTOs ===
    public class SsasSystemStatusDto
    {
        public bool IsServerConnected { get; set; }
        public string ServerInstance { get; set; } = string.Empty;
        public string CatalogName { get; set; } = string.Empty;
        public bool CatalogExists { get; set; }
        public string CubeName { get; set; } = "KhangNghi_Cube";
        public string CubeStatus { get; set; } = string.Empty; // "OPERATIONAL", "METADATA_MISMATCH", "UNAVAILABLE"
        public string? ErrorMessage { get; set; }
        public string? ActionRequired { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }

    public class MdxCatalogItemDto
    {
        public string Id { get; set; } = string.Empty;
        public int OrderNumber { get; set; }
        public string Section { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string BusinessPurpose { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public string MdxQuery { get; set; } = string.Empty;
        public string VerificationStatus { get; set; } = string.Empty; // "VERIFIED" vs "DESIGN_TEMPLATE"
        public string SourceFactTable { get; set; } = string.Empty;
    }
}
