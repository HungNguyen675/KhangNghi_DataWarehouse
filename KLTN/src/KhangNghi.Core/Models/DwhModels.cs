using System;

namespace KhangNghi.Core.Models
{
    public class DimTime
    {
        public int TimeID { get; set; }
        public DateTime FullDate { get; set; }
        public int Day { get; set; }
        public int Month { get; set; }
        public int Quarter { get; set; }
        public int Year { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;
    }

    public class DimLocation
    {
        public int LocationID { get; set; }
        public string? LocationCode { get; set; }
        public string Country { get; set; } = string.Empty;
        public string? State { get; set; }
        public string City { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string? PostalCode { get; set; }
    }

    public class DimCustomer
    {
        public string CustomerID { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Segment { get; set; } = string.Empty;
    }

    public class DimProduct
    {
        public int ProductKey { get; set; }
        public string ProductID { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public int? SupplierKey { get; set; }
        public int? ManufacturerKey { get; set; }
    }

    public class DimSupplier
    {
        public int SupplierKey { get; set; }
        public string SupplierID { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Phone { get; set; }
    }

    public class DimManufacturer
    {
        public int ManufacturerKey { get; set; }
        public string ManufacturerID { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;
        public string? Country { get; set; }
    }

    public class DimShipper
    {
        public int ShipperKey { get; set; }
        public string ShipperID { get; set; } = string.Empty;
        public string ShipperName { get; set; } = string.Empty;
        public string? ShippingMethod { get; set; }
        public string? ServiceLevel { get; set; }
    }

    public class FactOrder
    {
        public int OrderFactKey { get; set; }
        public string OrderID { get; set; } = string.Empty;
        public int TimeID { get; set; }
        public int LocationID { get; set; }
        public string CustomerID { get; set; } = string.Empty;
        public int ProductKey { get; set; }
        public int? ShipperKey { get; set; }
        public double Sales { get; set; }
        public int Quantity { get; set; }
        public double Discount { get; set; }
        public double Profit { get; set; }
    }

    public class FactPurchase
    {
        public int PurchaseFactKey { get; set; }
        public string PurchaseOrderID { get; set; } = string.Empty;
        public int TimeID { get; set; }
        public int ProductKey { get; set; }
        public int SupplierKey { get; set; }
        public int? LocationID { get; set; }
        public int PurchaseQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal PurchaseAmount { get; set; }
    }

    public class FactInventory
    {
        public int TimeID { get; set; }
        public int ProductKey { get; set; }
        public int LocationID { get; set; }
        public int QuantityIn { get; set; }
        public int QuantityOut { get; set; }
        public int QuantityOnHand { get; set; }
        public int ReorderLevel { get; set; }
    }

    public class FactShipping
    {
        public string OrderID { get; set; } = string.Empty;
        public int DeliveryTimeID { get; set; }
        public int LocationID { get; set; }
        public int? ShipperKey { get; set; }
        public double ShippingCost { get; set; }
        public int DeliveryDays { get; set; }
        public int LateDays { get; set; }
    }
}
