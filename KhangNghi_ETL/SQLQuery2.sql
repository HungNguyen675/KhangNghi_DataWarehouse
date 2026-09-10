CREATE DATABASE DWH_KhangNghi;

-- 1. TẠO CÁC BẢNG DIMENSION (VỆ TINH)
CREATE TABLE Dim_ThoiGian (
    TimeID INT PRIMARY KEY,
    Ngay DATE,
    Thang INT,
    Quy INT,
    Nam INT
);

CREATE TABLE Dim_DiaDiem (
    LocationID INT PRIMARY KEY,
    QuocGia NVARCHAR(100),
    TinhThanh NVARCHAR(100),
    VungMien NVARCHAR(50)
);

CREATE TABLE Dim_KhachHang (
    CustomerID NVARCHAR(50) PRIMARY KEY,
    TenKhachHang NVARCHAR(255),
    PhanKhuc NVARCHAR(50)
);

CREATE TABLE Dim_SanPham (
    ProductID NVARCHAR(50) PRIMARY KEY,
    TenSanPham NVARCHAR(255),
    DanhMuc NVARCHAR(100),
    NhomSanPham NVARCHAR(100)
);

CREATE TABLE Dim_NhanVien (
    EmployeeID NVARCHAR(50) PRIMARY KEY,
    TenNhanVien NVARCHAR(255),
    ChucVu NVARCHAR(100),
    ChiNhanh NVARCHAR(100)
);

-- 2. TẠO CÁC BẢNG FACT (TRUNG TÂM CỦA CHÒM SAO)

-- Fact Bán Hàng
CREATE TABLE Fact_BanHang (
    OrderID NVARCHAR(50),
    TimeID INT FOREIGN KEY REFERENCES Dim_ThoiGian(TimeID),
    LocationID INT FOREIGN KEY REFERENCES Dim_DiaDiem(LocationID),
    CustomerID NVARCHAR(50) FOREIGN KEY REFERENCES Dim_KhachHang(CustomerID),
    ProductID NVARCHAR(50) FOREIGN KEY REFERENCES Dim_SanPham(ProductID),
    EmployeeID NVARCHAR(50) FOREIGN KEY REFERENCES Dim_NhanVien(EmployeeID),
    DoanhThu FLOAT,
    SoLuong INT,
    TienChietKhau FLOAT,
    LoiNhuan FLOAT
);

-- Fact Vận Chuyển
CREATE TABLE Fact_VanChuyen (
    OrderID NVARCHAR(50),
    TimeID_NgayGiao INT FOREIGN KEY REFERENCES Dim_ThoiGian(TimeID),
    LocationID INT FOREIGN KEY REFERENCES Dim_DiaDiem(LocationID),
    PhiVanChuyen FLOAT,
    SoNgayGiaoHang INT,
    SoNgayTreHen INT
);

-- Fact Tồn Kho (Phục vụ cảnh báo tồn kho tuần 7)
CREATE TABLE Fact_TonKho (
    TimeID INT FOREIGN KEY REFERENCES Dim_ThoiGian(TimeID),
    ProductID NVARCHAR(50) FOREIGN KEY REFERENCES Dim_SanPham(ProductID),
    LocationID INT FOREIGN KEY REFERENCES Dim_DiaDiem(LocationID),
    SoLuongNhap INT,
    SoLuongXuat INT,
    SoLuongTon INT,
    MucCanhBao INT
);

SELECT * FROM Dim_KhachHang;

DELETE FROM Dim_KhachHang;