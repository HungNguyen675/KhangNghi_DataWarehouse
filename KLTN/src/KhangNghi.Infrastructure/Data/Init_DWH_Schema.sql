-- ============================================================
-- SCRIPT KHỞI TẠO MÔ HÌNH CHÒM SAO KHO DỮ LIỆU CÔNG TY KHANG NGHỊ
-- Database: DWH_KhangNghi
-- Mô hình gồm 4 Fact và 7 Dimension phục vụ trọn vẹn 40 câu hỏi phân tích
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'DWH_KhangNghi')
BEGIN
    CREATE DATABASE DWH_KhangNghi;
END
GO

USE DWH_KhangNghi;
GO

-- 1. BẢNG CHIỀU NHÀ CUNG CẤP (DIM_SUPPLIER)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Dim_Supplier')
BEGIN
    CREATE TABLE dbo.Dim_Supplier (
        SupplierKey INT IDENTITY(1,1) PRIMARY KEY,
        SupplierID NVARCHAR(50) NOT NULL UNIQUE,
        SupplierName NVARCHAR(255) NOT NULL,
        Country NVARCHAR(100) NULL,
        City NVARCHAR(100) NULL,
        Phone NVARCHAR(50) NULL
    );
END
GO

-- 2. BẢNG CHIỀU NHÀ SẢN XUẤT (DIM_MANUFACTURER)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Dim_Manufacturer')
BEGIN
    CREATE TABLE dbo.Dim_Manufacturer (
        ManufacturerKey INT IDENTITY(1,1) PRIMARY KEY,
        ManufacturerID NVARCHAR(50) NOT NULL UNIQUE,
        ManufacturerName NVARCHAR(255) NOT NULL,
        Country NVARCHAR(100) NULL
    );
END
GO

-- 3. BẢNG CHIỀU ĐƠN VỊ VẬN CHUYỂN (DIM_SHIPPER)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Dim_Shipper')
BEGIN
    CREATE TABLE dbo.Dim_Shipper (
        ShipperKey INT IDENTITY(1,1) PRIMARY KEY,
        ShipperID NVARCHAR(50) NOT NULL UNIQUE,
        ShipperName NVARCHAR(255) NOT NULL,
        ShippingMethod NVARCHAR(100) NULL,
        ServiceLevel NVARCHAR(100) NULL
    );
END
GO

-- 4. BẢNG CHIỀU THỜI GIAN (DIM_TIME)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Dim_Time')
BEGIN
    CREATE TABLE dbo.Dim_Time (
        TimeID INT PRIMARY KEY, -- Format YYYYMMDD
        FullDate DATE NOT NULL,
        Day INT NOT NULL,
        Month INT NOT NULL,
        Quarter INT NOT NULL,
        Year INT NOT NULL,
        DayOfWeek NVARCHAR(50) NOT NULL
    );
END
GO

-- 5. BẢNG CHIỀU ĐỊA ĐIỂM (DIM_LOCATION)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Dim_Location')
BEGIN
    CREATE TABLE dbo.Dim_Location (
        LocationID INT IDENTITY(1,1) PRIMARY KEY,
        LocationCode NVARCHAR(50) NULL,
        Country NVARCHAR(100) NOT NULL,
        State NVARCHAR(100) NULL,
        City NVARCHAR(100) NOT NULL,
        Region NVARCHAR(50) NOT NULL,
        PostalCode NVARCHAR(50) NULL
    );
END
GO

-- 6. BẢNG CHIỀU KHÁCH HÀNG (DIM_CUSTOMER)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Dim_Customer')
BEGIN
    CREATE TABLE dbo.Dim_Customer (
        CustomerID NVARCHAR(50) PRIMARY KEY,
        CustomerName NVARCHAR(255) NOT NULL,
        Segment NVARCHAR(50) NOT NULL
    );
END
GO

-- 7. BẢNG CHIỀU SẢN PHẨM (DIM_PRODUCT)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Dim_Product')
BEGIN
    CREATE TABLE dbo.Dim_Product (
        ProductKey INT IDENTITY(1,1) PRIMARY KEY,
        ProductID NVARCHAR(50) NOT NULL UNIQUE,
        ProductName NVARCHAR(255) NOT NULL,
        Category NVARCHAR(100) NOT NULL,
        SubCategory NVARCHAR(100) NOT NULL,
        SupplierKey INT NULL FOREIGN KEY REFERENCES dbo.Dim_Supplier(SupplierKey),
        ManufacturerKey INT NULL FOREIGN KEY REFERENCES dbo.Dim_Manufacturer(ManufacturerKey)
    );
END
GO

-- 8. FACT BÁN HÀNG (FACT_ORDER)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Fact_Order')
BEGIN
    CREATE TABLE dbo.Fact_Order (
        OrderFactKey INT IDENTITY(1,1) PRIMARY KEY,
        OrderID NVARCHAR(50) NOT NULL,
        TimeID INT NOT NULL FOREIGN KEY REFERENCES dbo.Dim_Time(TimeID),
        LocationID INT NOT NULL FOREIGN KEY REFERENCES dbo.Dim_Location(LocationID),
        CustomerID NVARCHAR(50) NOT NULL FOREIGN KEY REFERENCES dbo.Dim_Customer(CustomerID),
        ProductKey INT NOT NULL FOREIGN KEY REFERENCES dbo.Dim_Product(ProductKey),
        ShipperKey INT NULL FOREIGN KEY REFERENCES dbo.Dim_Shipper(ShipperKey),
        Sales FLOAT NOT NULL,
        Quantity INT NOT NULL,
        Discount FLOAT NOT NULL,
        Profit FLOAT NOT NULL
    );
END
GO

-- 9. FACT MUA HÀNG (FACT_PURCHASE)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Fact_Purchase')
BEGIN
    CREATE TABLE dbo.Fact_Purchase (
        PurchaseFactKey INT IDENTITY(1,1) PRIMARY KEY,
        PurchaseOrderID NVARCHAR(100) NOT NULL,
        TimeID INT NOT NULL FOREIGN KEY REFERENCES dbo.Dim_Time(TimeID),
        ProductKey INT NOT NULL FOREIGN KEY REFERENCES dbo.Dim_Product(ProductKey),
        SupplierKey INT NOT NULL FOREIGN KEY REFERENCES dbo.Dim_Supplier(SupplierKey),
        LocationID INT NULL FOREIGN KEY REFERENCES dbo.Dim_Location(LocationID),
        PurchaseQuantity INT NOT NULL DEFAULT 0,
        UnitCost DECIMAL(18,2) NOT NULL DEFAULT 0,
        PurchaseAmount DECIMAL(18,2) NOT NULL DEFAULT 0
    );
END
GO

-- 10. FACT TỒN KHO (FACT_INVENTORY)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Fact_Inventory')
BEGIN
    CREATE TABLE dbo.Fact_Inventory (
        TimeID INT NOT NULL FOREIGN KEY REFERENCES dbo.Dim_Time(TimeID),
        ProductKey INT NOT NULL FOREIGN KEY REFERENCES dbo.Dim_Product(ProductKey),
        LocationID INT NOT NULL FOREIGN KEY REFERENCES dbo.Dim_Location(LocationID),
        QuantityIn INT NULL DEFAULT 0,
        QuantityOut INT NULL DEFAULT 0,
        QuantityOnHand INT NULL DEFAULT 0,
        ReorderLevel INT NULL DEFAULT 0,
        CONSTRAINT PK_Fact_Inventory PRIMARY KEY (TimeID, ProductKey, LocationID)
    );
END
GO

-- 11. FACT VẬN CHUYỂN (FACT_SHIPPING)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Fact_Shipping')
BEGIN
    CREATE TABLE dbo.Fact_Shipping (
        OrderID NVARCHAR(255) NOT NULL,
        DeliveryTimeID INT NOT NULL FOREIGN KEY REFERENCES dbo.Dim_Time(TimeID),
        LocationID INT NOT NULL FOREIGN KEY REFERENCES dbo.Dim_Location(LocationID),
        ShipperKey INT NULL FOREIGN KEY REFERENCES dbo.Dim_Shipper(ShipperKey),
        ShippingCost FLOAT NULL DEFAULT 0,
        DeliveryDays INT NULL DEFAULT 0,
        LateDays INT NULL DEFAULT 0,
        CONSTRAINT PK_Fact_Shipping PRIMARY KEY (OrderID, DeliveryTimeID)
    );
END
GO

-- 12. BẢNG HỆ THỐNG: QUẢN TRỊ NGƯỜI DÙNG & PHÂN QUYỀN (SYS_USER)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Sys_User')
BEGIN
    CREATE TABLE dbo.Sys_User (
        UserID INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(100) UNIQUE NOT NULL,
        PasswordHash NVARCHAR(255) NOT NULL,
        FullName NVARCHAR(255) NOT NULL,
        Email NVARCHAR(255) NULL,
        Role NVARCHAR(50) NOT NULL, -- 'Admin', 'Director', 'InventoryManager', 'ShippingStaff'
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
END
GO

-- 13. BẢNG HỆ THỐNG: LỊCH SỬ THỰC THI ETL (SYS_ETLLOG)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Sys_EtlLog')
BEGIN
    CREATE TABLE dbo.Sys_EtlLog (
        LogID INT IDENTITY(1,1) PRIMARY KEY,
        StartTime DATETIME NOT NULL,
        EndTime DATETIME NULL,
        Status NVARCHAR(50) NOT NULL, -- 'Running', 'Success', 'Failed'
        RowsInserted INT DEFAULT 0,
        ErrorMessage NVARCHAR(MAX) NULL,
        TriggeredBy NVARCHAR(100) NULL
    );
END
GO

-- 14. BẢNG HỆ THỐNG: CẤU HÌNH LẬP LỊCH TỰ ĐỘNG (SYS_SCHEDULE)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Sys_Schedule')
BEGIN
    CREATE TABLE dbo.Sys_Schedule (
        ScheduleID INT IDENTITY(1,1) PRIMARY KEY,
        ScheduleName NVARCHAR(100) NOT NULL,
        FrequencyType NVARCHAR(50) NOT NULL, -- 'Daily', 'Hourly', 'Weekly'
        CronExpression NVARCHAR(100) NULL,
        Hour INT DEFAULT 0,
        Minute INT DEFAULT 0,
        IsEnabled BIT DEFAULT 1,
        LastRunTime DATETIME NULL,
        NextRunTime DATETIME NULL
    );
END
GO

-- TẠO CHỈ MỤC TỐI ƯU HÓA TRUY VẤN (INDEXES)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FactOrder_Time' AND object_id = OBJECT_ID('Fact_Order'))
    CREATE INDEX IX_FactOrder_Time ON dbo.Fact_Order(TimeID);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FactOrder_Product' AND object_id = OBJECT_ID('Fact_Order'))
    CREATE INDEX IX_FactOrder_Product ON dbo.Fact_Order(ProductKey);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FactOrder_Location' AND object_id = OBJECT_ID('Fact_Order'))
    CREATE INDEX IX_FactOrder_Location ON dbo.Fact_Order(LocationID);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FactOrder_Customer' AND object_id = OBJECT_ID('Fact_Order'))
    CREATE INDEX IX_FactOrder_Customer ON dbo.Fact_Order(CustomerID);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FactInventory_Reorder' AND object_id = OBJECT_ID('Fact_Inventory'))
    CREATE INDEX IX_FactInventory_Reorder ON dbo.Fact_Inventory(QuantityOnHand, ReorderLevel);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FactShipping_Late' AND object_id = OBJECT_ID('Fact_Shipping'))
    CREATE INDEX IX_FactShipping_Late ON dbo.Fact_Shipping(LateDays);
GO
