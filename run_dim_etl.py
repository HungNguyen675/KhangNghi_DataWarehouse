import os, subprocess, sys
sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')

dtexec_path = r'C:\Program Files (x86)\Microsoft SQL Server\170\DTS\Binn\DTExec.exe'
pkg_dir = r'd:\Khóa Luận Tốt Nghiệp Cô Vân Anh\KhangNghi_DataWarehouse_Git\KhangNghi_ETL'

dim_pkgs = [
    'Dim_Employee.dtsx',
    'Dim_Location.dtsx',
    'Dim_Manufacturer.dtsx',
    'Dim_PaymentMethod.dtsx',
    'Dim_Product.dtsx',
    'Dim_Promotion.dtsx',
    'Dim_SalesChannel.dtsx',
    'Dim_Service.dtsx',
    'Dim_Shipper.dtsx',
    'Dim_Supplier.dtsx',
    'Dim_Warehouse.dtsx'
]

print("=== BẮT ĐẦU NẠP CÁC BẢNG DIMENSIONS BẰNG SSIS (32-bit) ===")
for pkg in dim_pkgs:
    pkg_file = os.path.join(pkg_dir, pkg)
    cmd = [dtexec_path, '/F', pkg_file]
    res = subprocess.run(cmd, capture_output=True, text=True)
    if 'DTSER_SUCCESS (0)' in res.stdout:
        print(f"✔ [SUCCESS] {pkg}")
    else:
        print(f"❌ [FAILED] {pkg}")
        for line in res.stdout.splitlines():
            if 'Error:' in line or 'Description:' in line:
                print('   ', line.strip())

print("=== HOÀN TẤT NẠP DIMENSIONS ===")
