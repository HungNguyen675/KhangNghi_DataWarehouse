import openpyxl
import os

sample_dir = r"d:\Khóa Luận Tốt Nghiệp Cô Vân Anh\KhangNghi_DataWarehouse_Git\KLTN\sample_data"
os.makedirs(sample_dir, exist_ok=True)

# 1. Orders_Sample.xlsx
wb_orders = openpyxl.Workbook()
ws_orders = wb_orders.active
ws_orders.title = "Orders"

ws_orders.append(["OrderCode", "OrderDate", "CustomerCode", "ProductCode", "Quantity", "UnitPrice", "Discount", "Region"])
ws_orders.append(["DH801", "2026-09-24", "KH001", "SP001", 2, 35000000, 0, "MIEN_BAC"])  # VALID
ws_orders.append(["DH802", "2026-09-24", "KH002", "SP003", 1, 9500000, 500000, "MIEN_NAM"]) # VALID
ws_orders.append(["DH803", "2026-09-24", "KH999", "SP003", 1, 9500000, 0, "MIEN_BAC"])  # ERROR: Missing Customer KH999
ws_orders.append(["DH804", "2026-09-24", "KH003", "SP999", 5, 2000000, 0, "MIEN_BAC"])  # ERROR: Missing Product SP999
ws_orders.append(["DH001", "2026-09-24", "KH001", "SP001", 1, 35000000, 0, "MIEN_BAC"]) # DUPLICATE: DH001 in DB

orders_path = os.path.join(sample_dir, "Orders_Sample.xlsx")
wb_orders.save(orders_path)

# 2. Purchases_Sample.xlsx
wb_purchases = openpyxl.Workbook()
ws_purchases = wb_purchases.active
ws_purchases.title = "Purchases"

ws_purchases.append(["PurchaseCode", "PurchaseDate", "SupplierCode", "ProductCode", "Quantity", "UnitCost", "Region"])
ws_purchases.append(["PM801", "2026-09-24", "NCC001", "SP001", 10, 31000000, "MIEN_BAC"]) # VALID
ws_purchases.append(["PM802", "2026-09-24", "NCC002", "SP004", 5, 2000000, "MIEN_NAM"])  # VALID
ws_purchases.append(["PM803", "2026-09-24", "NCC999", "SP002", 3, 2500000, "MIEN_BAC"])  # ERROR: Missing Supplier NCC999
ws_purchases.append(["PM001", "2026-09-24", "NCC001", "SP001", 5, 32000000, "MIEN_BAC"]) # DUPLICATE: PM001 in DB

purchases_path = os.path.join(sample_dir, "Purchases_Sample.xlsx")
wb_purchases.save(purchases_path)

print("Sample Excel files generated successfully!")
