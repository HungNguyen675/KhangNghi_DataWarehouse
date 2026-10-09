import pandas as pd
import pyodbc

excel_path = r'd:\Khóa Luận Tốt Nghiệp Cô Vân Anh\KhangNghi_DataWarehouse_Git\KhangNghi_ETL\KhangNghiData_Sample.xlsx'
print("Reading Excel...")
df = pd.read_excel(excel_path, sheet_name='Orders')
print(f"Total rows in Excel: {len(df)}")
print("Columns:", list(df.columns))

conn_str = (
    r"DRIVER={ODBC Driver 17 for SQL Server};"
    r"SERVER=.\MSSQLSERVER2025;"
    r"DATABASE=NapDuLieu_Bai01;"
    r"Trusted_Connection=yes;"
)
conn = pyodbc.connect(conn_str)
cursor = conn.cursor()

# Create table Stg_KhangNghiData if not exists
create_sql = """
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Stg_KhangNghiData')
BEGIN
CREATE TABLE [dbo].[Stg_KhangNghiData] (
    [Row ID] float NULL,
    [Order ID] nvarchar(255) NULL,
    [Order Date] datetime NULL,
    [Ship Date] datetime NULL,
    [Ship Mode] nvarchar(255) NULL,
    [Customer ID] nvarchar(255) NULL,
    [Customer Name] nvarchar(255) NULL,
    [Segment] nvarchar(255) NULL,
    [Postal Code] float NULL,
    [City] nvarchar(255) NULL,
    [State] nvarchar(255) NULL,
    [Country] nvarchar(255) NULL,
    [Region] nvarchar(255) NULL,
    [Market] nvarchar(255) NULL,
    [Product ID] nvarchar(255) NULL,
    [Category] nvarchar(255) NULL,
    [Sub-Category] nvarchar(255) NULL,
    [Product Name] nvarchar(255) NULL,
    [Sales] float NULL,
    [Quantity] float NULL,
    [Discount] float NULL,
    [Profit] float NULL,
    [Shipping Cost] float NULL,
    [Order Priority] nvarchar(255) NULL,
    [Warehouse] nvarchar(255) NULL,
    [Payment Method] nvarchar(255) NULL,
    [Sales Channel] nvarchar(255) NULL,
    [Manufacturer] nvarchar(255) NULL,
    [Service] nvarchar(255) NULL,
    [Employee Name] nvarchar(255) NULL,
    [Promotion Name] nvarchar(255) NULL,
    [Location ID] nvarchar(255) NULL
);
END
"""
cursor.execute(create_sql)
conn.commit()

# Check row count
cursor.execute("SELECT COUNT(*) FROM dbo.Stg_KhangNghiData")
cnt = cursor.fetchone()[0]
print(f"Current rows in Stg_KhangNghiData: {cnt}")

if cnt == 0:
    print("Populating Stg_KhangNghiData from Excel...")
    df.columns = [c.strip() for c in df.columns]
    
    # Fast insert using executemany
    records = []
    for _, r in df.iterrows():
        records.append((
            r.get('Row ID'), r.get('Order ID'), r.get('Order Date'), r.get('Ship Date'), r.get('Ship Mode'),
            r.get('Customer ID'), r.get('Customer Name'), r.get('Segment'), r.get('Postal Code'),
            r.get('City'), r.get('State'), r.get('Country'), r.get('Region'), r.get('Market'),
            r.get('Product ID'), r.get('Category'), r.get('Sub-Category'), r.get('Product Name'),
            r.get('Sales'), r.get('Quantity'), r.get('Discount'), r.get('Profit'), r.get('Shipping Cost'),
            r.get('Order Priority'), r.get('Warehouse'), r.get('Payment Method'), r.get('Sales Channel'),
            r.get('Manufacturer'), r.get('Service'), r.get('Employee Name'), r.get('Promotion Name'),
            r.get('Location ID')
        ))
    
    cursor.executemany("""
        INSERT INTO [dbo].[Stg_KhangNghiData] VALUES (
            ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?
        )
    """, records)
    conn.commit()
    cursor.execute("SELECT COUNT(*) FROM dbo.Stg_KhangNghiData")
    print(f"Inserted rows in Stg_KhangNghiData: {cursor.fetchone()[0]}")

conn.close()
