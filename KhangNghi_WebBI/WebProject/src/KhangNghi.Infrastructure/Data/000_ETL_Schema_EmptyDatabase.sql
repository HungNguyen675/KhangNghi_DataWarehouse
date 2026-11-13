-- ETL-aligned bootstrap for an EMPTY TEST database selected by the operator.
-- Stops if any target table exists. No DROP, DELETE, reseed, or ETL execution.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped=0)
    THROW 51000, 'Bootstrap requires an empty database. Review existing schema with 002 instead.', 1;
IF OBJECT_ID(N'dbo.Dim_Time',N'U') IS NOT NULL THROW 51000, 'Target table Dim_Time already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Dim_Location',N'U') IS NOT NULL THROW 51000, 'Target table Dim_Location already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Dim_Customer',N'U') IS NOT NULL THROW 51000, 'Target table Dim_Customer already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Dim_Product',N'U') IS NOT NULL THROW 51000, 'Target table Dim_Product already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Dim_Employee',N'U') IS NOT NULL THROW 51000, 'Target table Dim_Employee already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Dim_Manufacturer',N'U') IS NOT NULL THROW 51000, 'Target table Dim_Manufacturer already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Dim_Shipper',N'U') IS NOT NULL THROW 51000, 'Target table Dim_Shipper already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Dim_Supplier',N'U') IS NOT NULL THROW 51000, 'Target table Dim_Supplier already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Dim_Promotion',N'U') IS NOT NULL THROW 51000, 'Target table Dim_Promotion already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Dim_SalesChannel',N'U') IS NOT NULL THROW 51000, 'Target table Dim_SalesChannel already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Dim_Warehouse',N'U') IS NOT NULL THROW 51000, 'Target table Dim_Warehouse already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Dim_PaymentMethod',N'U') IS NOT NULL THROW 51000, 'Target table Dim_PaymentMethod already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Dim_Service',N'U') IS NOT NULL THROW 51000, 'Target table Dim_Service already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Fact_Sales',N'U') IS NOT NULL THROW 51000, 'Target table Fact_Sales already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Fact_Purchase',N'U') IS NOT NULL THROW 51000, 'Target table Fact_Purchase already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Fact_Shipping',N'U') IS NOT NULL THROW 51000, 'Target table Fact_Shipping already exists; review schema differences manually.', 1;
IF OBJECT_ID(N'dbo.Fact_Inventory',N'U') IS NOT NULL THROW 51000, 'Target table Fact_Inventory already exists; review schema differences manually.', 1;
CREATE TABLE dbo.Dim_Time (
    TimeKey INT IDENTITY(1,1) PRIMARY KEY, FullDate DATE, 
    [Day] INT, [Month] INT, [Quarter] INT, [Year] INT,
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Dim_Location (
    LocationKey INT IDENTITY(1,1) PRIMARY KEY, LocationID NVARCHAR(50),               
    Country NVARCHAR(100), City NVARCHAR(100), [State] NVARCHAR(100), Region NVARCHAR(50),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Dim_Customer (
    CustomerKey INT IDENTITY(1,1) PRIMARY KEY, CustomerID NVARCHAR(50),               
    CustomerName NVARCHAR(255), Segment NVARCHAR(50),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Dim_Product (
    ProductKey INT IDENTITY(1,1) PRIMARY KEY, ProductID NVARCHAR(50),                
    ProductName NVARCHAR(255), Category NVARCHAR(100), SubCategory NVARCHAR(100),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Dim_Employee (
    EmployeeKey INT IDENTITY(1,1) PRIMARY KEY, EmployeeID NVARCHAR(50),               
    EmployeeName NVARCHAR(255), Title NVARCHAR(100),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Dim_Manufacturer (
    ManufacturerKey INT IDENTITY(1,1) PRIMARY KEY, ManufacturerID NVARCHAR(50),           
    ManufacturerName NVARCHAR(255), Country NVARCHAR(100),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Dim_Shipper (
    ShipperKey INT IDENTITY(1,1) PRIMARY KEY, ShipperID NVARCHAR(50),                
    ShipperName NVARCHAR(255), ShippingMethod NVARCHAR(50), ServiceLevel NVARCHAR(50),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Dim_Supplier (
    SupplierKey INT IDENTITY(1,1) PRIMARY KEY,
    SupplierID NVARCHAR(50) NOT NULL,
    SupplierName NVARCHAR(255),
    SourceSystem NVARCHAR(100),
    CreatedDate DATETIME DEFAULT GETDATE(),
    UpdatedDate DATETIME DEFAULT GETDATE()
);
CREATE TABLE dbo.Dim_Promotion (
    PromotionKey INT IDENTITY(1,1) PRIMARY KEY, PromotionID NVARCHAR(50),
    PromotionName NVARCHAR(255), DiscountType NVARCHAR(50), StartDate DATE, EndDate DATE,
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Dim_SalesChannel (
    ChannelKey INT IDENTITY(1,1) PRIMARY KEY, ChannelID NVARCHAR(50),
    ChannelName NVARCHAR(100), -- (e.g., Website, Đại lý, Bán lẻ)
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Dim_Warehouse (
    WarehouseKey INT IDENTITY(1,1) PRIMARY KEY, WarehouseID NVARCHAR(50),
    WarehouseName NVARCHAR(255), Capacity INT, ManagerName NVARCHAR(100),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Dim_PaymentMethod (
    PaymentKey INT IDENTITY(1,1) PRIMARY KEY, PaymentID NVARCHAR(50),
    MethodType NVARCHAR(100), TermDays INT, -- (e.g., Tiền mặt, Chuyển khoản, Công nợ 30 ngày)
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Dim_Service (
    ServiceKey INT IDENTITY(1,1) PRIMARY KEY, ServiceID NVARCHAR(50),
    ServiceName NVARCHAR(255), WarrantyPeriod INT, -- (e.g., Sửa chữa, Bảo trì)
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Fact_Sales (
    OrderID NVARCHAR(50), 
    TimeKey INT FOREIGN KEY REFERENCES Dim_Time(TimeKey),
    LocationKey INT FOREIGN KEY REFERENCES Dim_Location(LocationKey),
    CustomerKey INT FOREIGN KEY REFERENCES Dim_Customer(CustomerKey),
    ProductKey INT FOREIGN KEY REFERENCES Dim_Product(ProductKey),
    EmployeeKey INT FOREIGN KEY REFERENCES Dim_Employee(EmployeeKey),
    ManufacturerKey INT FOREIGN KEY REFERENCES Dim_Manufacturer(ManufacturerKey),
    PromotionKey INT FOREIGN KEY REFERENCES Dim_Promotion(PromotionKey),
    ChannelKey INT FOREIGN KEY REFERENCES Dim_SalesChannel(ChannelKey),
    PaymentKey INT FOREIGN KEY REFERENCES Dim_PaymentMethod(PaymentKey),
    ServiceKey INT FOREIGN KEY REFERENCES Dim_Service(ServiceKey),
    
    Quantity INT, 
    SalesAmount FLOAT, 
    DiscountAmount FLOAT, 
    ProfitAmount FLOAT,
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Fact_Purchase (
    PurchaseOrderID NVARCHAR(50),
    TimeKey INT FOREIGN KEY REFERENCES Dim_Time(TimeKey),
    ProductKey INT FOREIGN KEY REFERENCES Dim_Product(ProductKey),
    SupplierKey INT FOREIGN KEY REFERENCES Dim_Supplier(SupplierKey),
    LocationKey INT FOREIGN KEY REFERENCES Dim_Location(LocationKey),
    WarehouseKey INT FOREIGN KEY REFERENCES Dim_Warehouse(WarehouseKey),
    PaymentKey INT FOREIGN KEY REFERENCES Dim_PaymentMethod(PaymentKey),
    
    PurchaseQuantity INT,
    UnitCost FLOAT,
    TotalCost FLOAT,
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Fact_Shipping (
    OrderID NVARCHAR(50), 
    TimeKey INT FOREIGN KEY REFERENCES Dim_Time(TimeKey),
    LocationKey INT FOREIGN KEY REFERENCES Dim_Location(LocationKey),
    ShipperKey INT FOREIGN KEY REFERENCES Dim_Shipper(ShipperKey),
    WarehouseKey INT FOREIGN KEY REFERENCES Dim_Warehouse(WarehouseKey),
    
    ShippingCost FLOAT, 
    DeliveryDays INT, 
    LateDays INT,
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
CREATE TABLE dbo.Fact_Inventory (
    TimeKey INT FOREIGN KEY REFERENCES Dim_Time(TimeKey),
    ProductKey INT FOREIGN KEY REFERENCES Dim_Product(ProductKey),
    LocationKey INT FOREIGN KEY REFERENCES Dim_Location(LocationKey),
    WarehouseKey INT FOREIGN KEY REFERENCES Dim_Warehouse(WarehouseKey),
    
    QuantityIn INT, 
    QuantityOut INT, 
    QuantityOnHand INT,
    ReorderLevel INT,
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);
COMMIT;
