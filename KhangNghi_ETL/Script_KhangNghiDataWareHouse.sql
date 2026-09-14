
USE DWH_KhangNghi;
GO

---------------------------------------------------------
-- PHẦN 1: DỌN DẸP SẠCH SẼ (DROP)
---------------------------------------------------------
-- 1. Xóa Facts trước
DROP TABLE IF EXISTS Fact_Shipping;
DROP TABLE IF EXISTS Fact_Purchase;
DROP TABLE IF EXISTS Fact_Sales;
DROP TABLE IF EXISTS Fact_Inventory;

-- 2. Xóa Dims sau (8 bảng cũ + 5 bảng mới)
DROP TABLE IF EXISTS Dim_Time;
DROP TABLE IF EXISTS Dim_Location;
DROP TABLE IF EXISTS Dim_Customer;
DROP TABLE IF EXISTS Dim_Product;
DROP TABLE IF EXISTS Dim_Employee;
DROP TABLE IF EXISTS Dim_Manufacturer;
DROP TABLE IF EXISTS Dim_Shipper;
DROP TABLE IF EXISTS Dim_Supplier;

DROP TABLE IF EXISTS Dim_Promotion;
DROP TABLE IF EXISTS Dim_SalesChannel;
DROP TABLE IF EXISTS Dim_Warehouse;
DROP TABLE IF EXISTS Dim_PaymentMethod;
DROP TABLE IF EXISTS Dim_Service;

---------------------------------------------------------
-- PHẦN 2: TẠO 13 BẢNG DIMENSION (ĐẦY ĐỦ KHÓA & AUDIT)
---------------------------------------------------------

-- [8 BẢNG DIM CŨ]
CREATE TABLE Dim_Time (
    TimeKey INT IDENTITY(1,1) PRIMARY KEY, FullDate DATE, 
    [Day] INT, [Month] INT, [Quarter] INT, [Year] INT,
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

CREATE TABLE Dim_Location (
    LocationKey INT IDENTITY(1,1) PRIMARY KEY, LocationID NVARCHAR(50),               
    Country NVARCHAR(100), City NVARCHAR(100), [State] NVARCHAR(100), Region NVARCHAR(50),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

CREATE TABLE Dim_Customer (
    CustomerKey INT IDENTITY(1,1) PRIMARY KEY, CustomerID NVARCHAR(50),               
    CustomerName NVARCHAR(255), Segment NVARCHAR(50),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

CREATE TABLE Dim_Product (
    ProductKey INT IDENTITY(1,1) PRIMARY KEY, ProductID NVARCHAR(50),                
    ProductName NVARCHAR(255), Category NVARCHAR(100), SubCategory NVARCHAR(100),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

CREATE TABLE Dim_Employee (
    EmployeeKey INT IDENTITY(1,1) PRIMARY KEY, EmployeeID NVARCHAR(50),               
    EmployeeName NVARCHAR(255), Title NVARCHAR(100),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

CREATE TABLE Dim_Manufacturer (
    ManufacturerKey INT IDENTITY(1,1) PRIMARY KEY, ManufacturerID NVARCHAR(50),           
    ManufacturerName NVARCHAR(255), Country NVARCHAR(100),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

CREATE TABLE Dim_Shipper (
    ShipperKey INT IDENTITY(1,1) PRIMARY KEY, ShipperID NVARCHAR(50),                
    ShipperName NVARCHAR(255), ShippingMethod NVARCHAR(50), ServiceLevel NVARCHAR(50),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

CREATE TABLE Dim_Supplier (
    SupplierKey INT IDENTITY(1,1) PRIMARY KEY, SupplierID NVARCHAR(50),                
    SupplierName NVARCHAR(255), Country NVARCHAR(100), Phone NVARCHAR(50),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

-- [5 BẢNG DIM MỚI THÊM VÀO]
CREATE TABLE Dim_Promotion (
    PromotionKey INT IDENTITY(1,1) PRIMARY KEY, PromotionID NVARCHAR(50),
    PromotionName NVARCHAR(255), DiscountType NVARCHAR(50), StartDate DATE, EndDate DATE,
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

CREATE TABLE Dim_SalesChannel (
    ChannelKey INT IDENTITY(1,1) PRIMARY KEY, ChannelID NVARCHAR(50),
    ChannelName NVARCHAR(100), -- (e.g., Website, Đại lý, Bán lẻ)
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

CREATE TABLE Dim_Warehouse (
    WarehouseKey INT IDENTITY(1,1) PRIMARY KEY, WarehouseID NVARCHAR(50),
    WarehouseName NVARCHAR(255), Capacity INT, ManagerName NVARCHAR(100),
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

CREATE TABLE Dim_PaymentMethod (
    PaymentKey INT IDENTITY(1,1) PRIMARY KEY, PaymentID NVARCHAR(50),
    MethodType NVARCHAR(100), TermDays INT, -- (e.g., Tiền mặt, Chuyển khoản, Công nợ 30 ngày)
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

CREATE TABLE Dim_Service (
    ServiceKey INT IDENTITY(1,1) PRIMARY KEY, ServiceID NVARCHAR(50),
    ServiceName NVARCHAR(255), WarrantyPeriod INT, -- (e.g., Sửa chữa, Bảo trì)
    SourceSystem NVARCHAR(50), CreatedDate DATETIME, UpdatedDate DATETIME
);

---------------------------------------------------------
-- PHẦN 3: TẠO 4 BẢNG FACT (ĐÃ MÓC NỐI 13 BẢNG DIM)
---------------------------------------------------------

-- 1. SỰ KIỆN BÁN HÀNG (Kết nối Khuyến mãi, Kênh bán, Thanh toán, Dịch vụ)
CREATE TABLE Fact_Sales (
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

-- 2. SỰ KIỆN NHẬP HÀNG (Kết nối Thanh toán, Kho bãi)
CREATE TABLE Fact_Purchase (
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

-- 3. SỰ KIỆN VẬN CHUYỂN (Kết nối Kho bãi xuất phát)
CREATE TABLE Fact_Shipping (
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

-- 4. SỰ KIỆN TỒN KHO (Kết nối Kho bãi)
CREATE TABLE Fact_Inventory (
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