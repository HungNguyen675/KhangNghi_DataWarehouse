using System;
using System.Globalization;
using System.Windows.Data;

namespace KhangNghi.DesktopAdmin;

public sealed class RoleDisplayConverter : IValueConverter
{
    public static string GetDisplayName(string? role) => role switch
    {
        "Admin" => "Quản trị viên hệ thống",
        "Director" => "Giám đốc",
        "SalesStaff" => "Trưởng phòng kinh doanh",
        "InventoryStaff" => "Trưởng phòng quản lý kho",
        _ => role ?? ""
    };

    public object Convert(
        object value, Type targetType,
        object parameter, CultureInfo culture)
        => GetDisplayName(value?.ToString());

    public object ConvertBack(
        object value, Type targetType,
        object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}