-- Tạo Database mới cho hệ thống Thu Mua / Nhập Hàng
CREATE DATABASE KhangNghi_Purchasing_OLTP;
GO
USE KhangNghi_Purchasing_OLTP;
GO

-- 1. Bảng Nhà cung cấp
CREATE TABLE Suppliers (
    SupplierID NVARCHAR(50) PRIMARY KEY,
    SupplierName NVARCHAR(255) NOT NULL,
    ContactPhone NVARCHAR(50),
    Address NVARCHAR(255)
);

-- 2. Bảng Kho bãi
CREATE TABLE Warehouses (
    WarehouseID NVARCHAR(50) PRIMARY KEY,
    WarehouseName NVARCHAR(255) NOT NULL,
    Capacity INT,
    ManagerName NVARCHAR(100)
);

-- 3. Bảng Sản phẩm (Master data dùng chung)
CREATE TABLE Products (
    ProductID NVARCHAR(50) PRIMARY KEY,
    ProductName NVARCHAR(255) NOT NULL,
    Category NVARCHAR(100),
    ReorderLevel INT -- Mức tồn kho tối thiểu để hệ thống cảnh báo nhập thêm hàng
);

-- 4. Bảng Nhân viên (Phụ trách thu mua/nhập kho)
CREATE TABLE Employees (
    EmployeeID INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeName NVARCHAR(255) NOT NULL,
    Department NVARCHAR(100)
);

-- 5. Bảng Phiếu Nhập Hàng (Purchase Orders - Thông tin chung của phiếu)
CREATE TABLE Purchases (
    PurchaseID NVARCHAR(50) PRIMARY KEY,
    PurchaseDate DATE NOT NULL,
    SupplierID NVARCHAR(50) FOREIGN KEY REFERENCES Suppliers(SupplierID),
    WarehouseID NVARCHAR(50) FOREIGN KEY REFERENCES Warehouses(WarehouseID),
    EmployeeID INT FOREIGN KEY REFERENCES Employees(EmployeeID),
    PaymentStatus NVARCHAR(50), -- Tình trạng thanh toán cho nhà cung cấp
    Notes NVARCHAR(MAX)
);

-- 6. Bảng Chi tiết Phiếu Nhập Hàng (Các món hàng trong 1 phiếu)
CREATE TABLE PurchaseDetails (
    PurchaseID NVARCHAR(50) FOREIGN KEY REFERENCES Purchases(PurchaseID),
    ProductID NVARCHAR(50) FOREIGN KEY REFERENCES Products(ProductID),
    QuantityIn INT NOT NULL,     -- Số lượng nhập vào
    UnitCost FLOAT NOT NULL,     -- Giá vốn nhập vào 1 đơn vị
    TotalCost FLOAT,             -- Thành tiền (QuantityIn * UnitCost)
    PRIMARY KEY (PurchaseID, ProductID)
);

USE KhangNghi_Purchasing_OLTP;
GO

-- 7. Bảng Quản lý Ngành hàng (Tương đương bảng People bên Sales)
CREATE TABLE CategoryManagers (
    Category NVARCHAR(100) PRIMARY KEY, -- Khóa chính là Tên Ngành hàng
    ManagerName NVARCHAR(255) NOT NULL,
    ContactEmail NVARCHAR(100)
);

-- 8. Bảng Trả hàng Nhà cung cấp (Tương đương bảng Returns bên Sales)
CREATE TABLE PurchaseReturns (
    ReturnID INT IDENTITY(1,1) PRIMARY KEY,
    PurchaseID NVARCHAR(50) FOREIGN KEY REFERENCES Purchases(PurchaseID),
    ProductID NVARCHAR(50) FOREIGN KEY REFERENCES Products(ProductID),
    ReturnQuantity INT NOT NULL,   -- Số lượng hàng bị lỗi phải trả lại
    ReturnReason NVARCHAR(255),    -- Lý do trả (VD: Hàng móp méo, Sai quy cách, Giao nhầm...)
    ReturnDate DATE
);


USE KhangNghi_Purchasing_OLTP;
GO

-- 1. Bơm Nhà cung cấp (SQL tự động chép từ DB Bán hàng sang)
INSERT INTO Suppliers (SupplierID, SupplierName)
SELECT DISTINCT 'SUP-' + CAST(ManufacturerID AS NVARCHAR), ManufacturerName
FROM KhangNghi_OLTP.dbo.Manufacturers;

-- 2. Bơm Kho bãi (Lấy danh sách các Kho từ Excel Bán hàng)
INSERT INTO Warehouses (WarehouseID, WarehouseName, Capacity)
SELECT DISTINCT Warehouse, 'Kho ' + Warehouse, 50000
FROM KhangNghi_OLTP.dbo.Stg_RawExcelData
WHERE Warehouse IS NOT NULL;

-- 3. Bơm Sản phẩm (Chép Mã và Tên SP từ DB Bán hàng sang)
INSERT INTO Products (ProductID, ProductName, Category, ReorderLevel)
SELECT ProductID, ProductName, Category, 50
FROM KhangNghi_OLTP.dbo.Products;

-- 4. Bơm Nhân viên mua hàng (Sinh tự động 1 vài người mẫn cán)
INSERT INTO Employees (EmployeeName, Department)
VALUES ('Nguyen Van Thu Mua', 'Procurement'), ('Tran Thi Kho', 'Inventory');

-- 5. Tạo các Phiếu Nhập Hàng ảo (Tạo 1 Hợp đồng nhập khổng lồ đầu năm 2012 cho mỗi Nhà cung cấp)
INSERT INTO Purchases (PurchaseID, PurchaseDate, SupplierID, WarehouseID, EmployeeID, PaymentStatus)
SELECT DISTINCT 
    'PO-INIT-' + SupplierID, 
    '2012-01-01', 
    SupplierID, 
    (SELECT TOP 1 WarehouseID FROM Warehouses), 
    1, 
    'Paid'
FROM Suppliers;

-- 6. Bơm Chi tiết các Phiếu Nhập (Dùng thuật toán Random để ra số lượng nhập từ 100-500 chiếc, Giá ngẫu nhiên)
INSERT INTO PurchaseDetails (PurchaseID, ProductID, QuantityIn, UnitCost, TotalCost)
SELECT 
    'PO-INIT-' + s.SupplierID,
    p.ProductID,
    ABS(CHECKSUM(NEWID())) % 400 + 100, -- Random từ 100 đến 500 chiếc
    ABS(CHECKSUM(NEWID())) % 90 + 10,   -- Random giá nhập từ 10$ đến 100$
    0
FROM KhangNghi_OLTP.dbo.Products p
JOIN KhangNghi_OLTP.dbo.Manufacturers m ON p.ManufacturerID = m.ManufacturerID
JOIN Suppliers s ON s.SupplierName = m.ManufacturerName;

-- Cập nhật lại cột Thành tiền = Số lượng * Giá vốn
UPDATE PurchaseDetails
SET TotalCost = QuantityIn * UnitCost;


USE KhangNghi_Purchasing_OLTP;
GO
SELECT '1. Suppliers (Nhà cung cấp)' AS TableName, COUNT(*) AS TotalRows FROM Suppliers UNION ALL
SELECT '2. Products (Sản phẩm)', COUNT(*) FROM Products UNION ALL
SELECT '3. Warehouses (Kho bãi)', COUNT(*) FROM Warehouses UNION ALL
SELECT '4. Purchases (Phiếu nhập tổng)', COUNT(*) FROM Purchases UNION ALL
SELECT '5. PurchaseDetails (Chi tiết nhập)', COUNT(*) FROM PurchaseDetails;


USE KhangNghi_Purchasing_OLTP;
GO

-- 1A. Tạo bảng Phòng Ban (Departments)
CREATE TABLE Departments (
    DepartmentID INT IDENTITY(1,1) PRIMARY KEY,
    DepartmentName NVARCHAR(100) NOT NULL
);
GO
-- Thêm sẵn vài dữ liệu mẫu
INSERT INTO Departments (DepartmentName) VALUES (N'Phòng Thu Mua'), (N'Phòng Kế Toán'), (N'Ban Giám Đốc');
GO
-- Thêm cột DepartmentID vào bảng Employees và tạo Khóa ngoại
ALTER TABLE Employees ADD DepartmentID INT;
GO
ALTER TABLE Employees ADD CONSTRAINT FK_Employees_Departments FOREIGN KEY (DepartmentID) REFERENCES Departments(DepartmentID);
GO

-- 1B. Tạo bảng Cities (Thành phố) và Wards (Xã/Phường)
CREATE TABLE Cities (
    CityID INT IDENTITY(1,1) PRIMARY KEY,
    CityName NVARCHAR(100) NOT NULL
);
GO
CREATE TABLE Wards (
    WardID INT IDENTITY(1,1) PRIMARY KEY,
    WardName NVARCHAR(100) NOT NULL,
    CityID INT,
    CONSTRAINT FK_Wards_Cities FOREIGN KEY (CityID) REFERENCES Cities(CityID)
);
GO
-- Thêm cột WardID vào bảng Suppliers và nối Khóa ngoại
ALTER TABLE Suppliers ADD WardID INT;
GO
ALTER TABLE Suppliers ADD CONSTRAINT FK_Suppliers_Wards FOREIGN KEY (WardID) REFERENCES Wards(WardID);
GO

USE KhangNghi_Purchasing_OLTP;
GO
ALTER TABLE Products WITH NOCHECK
ADD CONSTRAINT FK_Products_CategoryManagers 
FOREIGN KEY (Category) REFERENCES CategoryManagers(Category);
GO