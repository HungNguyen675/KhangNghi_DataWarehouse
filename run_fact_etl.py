import os, subprocess, sys
sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')

dtexec_path = r'C:\Program Files (x86)\Microsoft SQL Server\170\DTS\Binn\DTExec.exe'
pkg_dir = r'd:\Khóa Luận Tốt Nghiệp Cô Vân Anh\KhangNghi_DataWarehouse_Git\KhangNghi_ETL'

fact_pkgs = [
    'Fact_Sales.dtsx',
    'Fact_Purchase.dtsx',
    'Fact_Inventory.dtsx',
    'Fact_Shipping.dtsx'
]

print("=== BẮT ĐẦU NẠP CÁC BẢNG FACT BẰNG SSIS (32-bit) ===")
for pkg in fact_pkgs:
    pkg_file = os.path.join(pkg_dir, pkg)
    cmd = [dtexec_path, '/F', pkg_file]
    print(f"--> Đang chạy {pkg}...")
    res = subprocess.run(cmd, capture_output=True, text=True)
    if 'DTSER_SUCCESS (0)' in res.stdout:
        print(f"✔ [SUCCESS] {pkg}")
    else:
        print(f"❌ [FAILED] {pkg}")
        for line in res.stdout.splitlines():
            if 'Error:' in line or 'Description:' in line:
                print('   ', line.strip())

print("=== HOÀN TẤT NẠP FACT ===")
