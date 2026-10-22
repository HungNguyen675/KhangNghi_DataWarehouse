-- Tạo Database mới cho Website OLTP
CREATE DATABASE KhangNghi_OLTP;
GO
USE KhangNghi_OLTP;
GO

-- 1. Bảng Khách Hàng (Tách từ Customer ID, Name, Segment)
CREATE TABLE Customers (
    CustomerID NVARCHAR(50) PRIMARY KEY,
    CustomerName NVARCHAR(255) NOT NULL,
    Segment NVARCHAR(100)
);

-- 2. Bảng Địa chỉ / Vị trí (Tách từ Location ID, City, State...)
CREATE TABLE Locations (
    LocationID NVARCHAR(50) PRIMARY KEY,
    PostalCode NVARCHAR(50),
    City NVARCHAR(100),
    State NVARCHAR(100),
    Country NVARCHAR(100),
    Region NVARCHAR(100),
    Market NVARCHAR(100)
);

-- 3. Bảng Nhân viên bán hàng
CREATE TABLE Employees (
    EmployeeID INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeName NVARCHAR(255) NOT NULL
);

-- 4. Bảng Nhà cung cấp / Hãng sản xuất
CREATE TABLE Manufacturers (
    ManufacturerID INT IDENTITY(1,1) PRIMARY KEY,
    ManufacturerName NVARCHAR(255) NOT NULL
);

-- 5. Bảng Sản phẩm (Tách từ Product ID, Name, Category...)
CREATE TABLE Products (
    ProductID NVARCHAR(50) PRIMARY KEY,
    ProductName NVARCHAR(255) NOT NULL,
    Category NVARCHAR(100),
    SubCategory NVARCHAR(100),
    ManufacturerID INT FOREIGN KEY REFERENCES Manufacturers(ManufacturerID),
    ServiceType NVARCHAR(255) -- Dịch vụ bảo hành đi kèm
);

-- 6. Bảng Chương trình Khuyến mãi
CREATE TABLE Promotions (
    PromotionID INT IDENTITY(1,1) PRIMARY KEY,
    PromotionName NVARCHAR(255)
);

-- 7. Bảng Đơn Hàng (Chứa các thông tin chung của 1 hóa đơn)
CREATE TABLE Orders (
    OrderID NVARCHAR(50) PRIMARY KEY,
    OrderDate DATE NOT NULL,
    OrderPriority NVARCHAR(50),
    SalesChannel NVARCHAR(100),
    PaymentMethod NVARCHAR(100),
    CustomerID NVARCHAR(50) FOREIGN KEY REFERENCES Customers(CustomerID),
    LocationID NVARCHAR(50) FOREIGN KEY REFERENCES Locations(LocationID),
    EmployeeID INT FOREIGN KEY REFERENCES Employees(EmployeeID),
    PromotionID INT FOREIGN KEY REFERENCES Promotions(PromotionID)
);

-- 8. Bảng Chi tiết Đơn Hàng (1 Đơn hàng có thể mua nhiều Sản phẩm)
CREATE TABLE OrderDetails (
    OrderID NVARCHAR(50) FOREIGN KEY REFERENCES Orders(OrderID),
    ProductID NVARCHAR(50) FOREIGN KEY REFERENCES Products(ProductID),
    Quantity INT,
    Sales FLOAT,    -- Tổng tiền của món đó
    Discount FLOAT, -- Giảm giá
    Profit FLOAT,   -- Lợi nhuận
    PRIMARY KEY (OrderID, ProductID) -- Khóa chính kép
);

-- 9. Bảng Giao Hàng (Tách riêng phần Logistic)
CREATE TABLE Shipments (
    ShipmentID INT IDENTITY(1,1) PRIMARY KEY,
    OrderID NVARCHAR(50) FOREIGN KEY REFERENCES Orders(OrderID),
    ShipDate DATE,
    ShipMode NVARCHAR(100),
    ShippingCost FLOAT,
    Warehouse NVARCHAR(100)
);


USE KhangNghi_OLTP;
GO

-- 10. Bảng Người quản lý khu vực (Tách từ sheet People)
CREATE TABLE RegionManagers (
    Region NVARCHAR(100) PRIMARY KEY, -- Khóa chính là Tên Vùng (Region)
    ManagerName NVARCHAR(255) NOT NULL
);

-- 11. Bảng Hoàn Trả Hàng (Tách từ sheet Returns)
CREATE TABLE Returns (
    ReturnID INT IDENTITY(1,1) PRIMARY KEY,
    OrderID NVARCHAR(50) FOREIGN KEY REFERENCES Orders(OrderID), -- Nối với bảng Orders
    ReturnDate DATE,
    ReturnReason NVARCHAR(MAX) -- Bổ sung thêm cột Lý do trả hàng cho chuẩn thiết kế web
);

SELECT * FROM OrderDetails;


USE KhangNghi_OLTP;
GO

-- 1. Bơm Bảng Khách hàng
INSERT INTO Customers (CustomerID, CustomerName, Segment)
SELECT DISTINCT Customer_ID, Customer_Name, Segment
FROM Stg_RawExcelData
WHERE Customer_ID IS NOT NULL;

-- 2. Bơm Bảng Vị trí / Địa lý
INSERT INTO Locations (LocationID, PostalCode, City, State, Country, Region, Market)
SELECT DISTINCT Location_ID, CAST(Postal_Code AS NVARCHAR(50)), City, State, Country, Region, Market
FROM Stg_RawExcelData
WHERE Location_ID IS NOT NULL;

-- 3. Bơm Bảng Nhà sản xuất (SQL tự sinh ID)
INSERT INTO Manufacturers (ManufacturerName)
SELECT DISTINCT Manufacturer
FROM Stg_RawExcelData
WHERE Manufacturer IS NOT NULL;

-- 4. Bơm Bảng Nhân viên bán hàng
INSERT INTO Employees (EmployeeName)
SELECT DISTINCT Employee_Name
FROM Stg_RawExcelData
WHERE Employee_Name IS NOT NULL;

-- 5. Bơm Bảng Khuyến mãi
INSERT INTO Promotions (PromotionName)
SELECT DISTINCT Promotion_Name
FROM Stg_RawExcelData
WHERE Promotion_Name IS NOT NULL;

-- 6. Bơm Bảng Sản phẩm
INSERT INTO Products (ProductID, ProductName, Category, SubCategory, ManufacturerID, ServiceType)
SELECT DISTINCT 
    s.Product_ID, s.Product_Name, s.Category, s.Sub_Category, 
    m.ManufacturerID, s.Service
FROM Stg_RawExcelData s
JOIN Manufacturers m ON s.Manufacturer = m.ManufacturerName
WHERE s.Product_ID IS NOT NULL;


USE KhangNghi_OLTP;
GO

-- CHỮA CHÁY 1: Bảng Vị trí (Ép lấy 1 dòng duy nhất cho mỗi Location_ID)
WITH CTE_Locations AS (
    SELECT Location_ID, Postal_Code, City, State, Country, Region, Market,
           ROW_NUMBER() OVER(PARTITION BY Location_ID ORDER BY (SELECT NULL)) as rn
    FROM Stg_RawExcelData
    WHERE Location_ID IS NOT NULL
)
INSERT INTO Locations (LocationID, PostalCode, City, State, Country, Region, Market)
SELECT Location_ID, CAST(Postal_Code AS NVARCHAR(50)), City, State, Country, Region, Market
FROM CTE_Locations
WHERE rn = 1;

-- CHỮA CHÁY 2: Bảng Sản phẩm (Ép lấy 1 dòng duy nhất cho mỗi Product_ID)
-- CHỮA CHÁY 2: Bảng Sản phẩm (Đã sửa lỗi gõ nhầm s.Service)
WITH CTE_Products AS (
    SELECT s.Product_ID, s.Product_Name, s.Category, s.Sub_Category, m.ManufacturerID, s.Service,
           ROW_NUMBER() OVER(PARTITION BY s.Product_ID ORDER BY (SELECT NULL)) as rn
    FROM Stg_RawExcelData s
    JOIN Manufacturers m ON s.Manufacturer = m.ManufacturerName
    WHERE s.Product_ID IS NOT NULL
)
INSERT INTO Products (ProductID, ProductName, Category, SubCategory, ManufacturerID, ServiceType)
SELECT Product_ID, Product_Name, Category, Sub_Category, ManufacturerID, Service
FROM CTE_Products
WHERE rn = 1;


USE KhangNghi_OLTP;
GO

-- 7. Bơm Bảng Đơn Hàng (Orders) - Lọc lấy 1 hóa đơn duy nhất
WITH CTE_Orders AS (
    SELECT Order_ID, Order_Date, Order_Priority, Sales_Channel, Payment_Method, 
           Customer_ID, Location_ID, e.EmployeeID, p.PromotionID,
           ROW_NUMBER() OVER(PARTITION BY Order_ID ORDER BY (SELECT NULL)) as rn
    FROM Stg_RawExcelData s
    LEFT JOIN Employees e ON s.Employee_Name = e.EmployeeName
    LEFT JOIN Promotions p ON s.Promotion_Name = p.PromotionName
    WHERE Order_ID IS NOT NULL
)
INSERT INTO Orders (OrderID, OrderDate, OrderPriority, SalesChannel, PaymentMethod, CustomerID, LocationID, EmployeeID, PromotionID)
SELECT Order_ID, CONVERT(DATE, Order_Date, 103), Order_Priority, Sales_Channel, Payment_Method, 
       Customer_ID, Location_ID, EmployeeID, PromotionID
FROM CTE_Orders
WHERE rn = 1;

-- 8. Bơm Bảng Chi Tiết Đơn Hàng (OrderDetails) - Gom tổng sản phẩm
INSERT INTO OrderDetails (OrderID, ProductID, Quantity, Sales, Discount, Profit)
SELECT Order_ID, Product_ID, 
       SUM(CAST(Quantity AS INT)), 
       SUM(CAST(Sales AS FLOAT)), 
       MAX(CAST(Discount AS FLOAT)), 
       SUM(CAST(Profit AS FLOAT))
FROM Stg_RawExcelData
WHERE Order_ID IS NOT NULL AND Product_ID IS NOT NULL
GROUP BY Order_ID, Product_ID;

-- 9. Bơm Bảng Giao Hàng (Shipments)
WITH CTE_Shipments AS (
    SELECT Order_ID, Ship_Date, Ship_Mode, Shipping_Cost, Warehouse,
           ROW_NUMBER() OVER(PARTITION BY Order_ID ORDER BY (SELECT NULL)) as rn
    FROM Stg_RawExcelData
    WHERE Order_ID IS NOT NULL
)
INSERT INTO Shipments (OrderID, ShipDate, ShipMode, ShippingCost, Warehouse)
SELECT Order_ID, CONVERT(DATE, Ship_Date, 103), Ship_Mode, CAST(Shipping_Cost AS FLOAT), Warehouse
FROM CTE_Shipments
WHERE rn = 1;


USE KhangNghi_OLTP;
GO
SELECT '1. Customers (Khách hàng)' AS TableName, COUNT(*) AS TotalRows FROM Customers UNION ALL
SELECT '2. Products (Sản phẩm)', COUNT(*) FROM Products UNION ALL
SELECT '3. Locations (Vị trí địa lý)', COUNT(*) FROM Locations UNION ALL
SELECT '4. Orders (Hóa đơn chung)', COUNT(*) FROM Orders UNION ALL
SELECT '5. OrderDetails (Chi tiết mua hàng)', COUNT(*) FROM OrderDetails UNION ALL
SELECT '6. Shipments (Giao vận)', COUNT(*) FROM Shipments;



USE KhangNghi_OLTP;
GO
-- Tạo 1 mã đơn hàng mới toanh của năm 2026
DECLARE @NewOrderID NVARCHAR(255) = 'DON-HANG-VIP-2026';

-- Bơm 1 hóa đơn mới vào bảng Orders (Ngày mua là ngày hôm nay GETDATE())
INSERT INTO Orders (OrderID, OrderDate, OrderPriority, SalesChannel, PaymentMethod, CustomerID, LocationID, EmployeeID, PromotionID)
SELECT TOP 1 @NewOrderID, GETDATE(), OrderPriority, SalesChannel, PaymentMethod, CustomerID, LocationID, EmployeeID, PromotionID
FROM Orders;

-- Bơm chi tiết đơn hàng đó vào bảng OrderDetails (Khách mua 100 cái, giá 9999 đô)
INSERT INTO OrderDetails (OrderID, ProductID, Quantity, Sales, Discount, Profit)
SELECT TOP 1 @NewOrderID, ProductID, 100, 9999.00, 0, 5000.00
FROM OrderDetails;

PRINT N'✅ BƯỚC 1: ĐÃ BƠM THÀNH CÔNG 1 ĐƠN HÀNG MỚI VÀO HỆ THỐNG OLTP!';

USE KhangNghi_OLTP;
GO

-- 2A. Tạo bảng Loại Khách Hàng (CustomerTypes)
CREATE TABLE CustomerTypes (
    CustomerTypeID INT IDENTITY(1,1) PRIMARY KEY,
    CustomerTypeName NVARCHAR(100) NOT NULL
);
GO
-- Thêm sẵn vài loại mẫu
INSERT INTO CustomerTypes (CustomerTypeName) VALUES (N'Khách hàng cá nhân'), (N'Khách hàng doanh nghiệp'), (N'Khách VIP');
GO
-- Nối Khóa ngoại vào bảng Customers
ALTER TABLE Customers ADD CustomerTypeID INT;
GO
ALTER TABLE Customers ADD CONSTRAINT FK_Customers_CustomerTypes FOREIGN KEY (CustomerTypeID) REFERENCES CustomerTypes(CustomerTypeID);
GO

-- 2B. Tạo bảng Loại Sản Phẩm (ProductTypes)
CREATE TABLE ProductTypes (
    ProductTypeID INT IDENTITY(1,1) PRIMARY KEY,
    ProductTypeName NVARCHAR(100) NOT NULL
);
GO
-- Nối Khóa ngoại vào bảng Products
ALTER TABLE Products ADD ProductTypeID INT;
GO
ALTER TABLE Products ADD CONSTRAINT FK_Products_ProductTypes FOREIGN KEY (ProductTypeID) REFERENCES ProductTypes(ProductTypeID);
GO

-- 2C. Sửa bảng Orders (Hóa Đơn) - Thêm số nhà, tên đường
ALTER TABLE Orders ADD DeliveryStreetAddress NVARCHAR(255);
GO