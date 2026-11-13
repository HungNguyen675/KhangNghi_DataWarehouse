using System;
using System.Linq;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using KhangNghi.Core.Models;

namespace KhangNghi.DesktopAdmin;

public partial class MainWindow
{
    private bool _userOperationBusy;

    private bool CanManageUsers()
    {
        if (_currentUser?.Role == "Admin") return true;
        MessageBox.Show("Chỉ quản trị viên được quản lý tài khoản.");
        return false;
    }

    private void ClearUserEditor()
    {
        GridUsers.SelectedItem = null;
        TxtUserUsername.IsReadOnly = false;
        TxtUserUsername.Clear();
        TxtUserFullName.Clear();
        TxtUserEmail.Clear();
        TxtUserPassword.Clear();
        TxtUserConfirmPassword.Clear();
        CmbUserRole.SelectedIndex = 2;
        ChkUserActive.IsChecked = true;
    }

    private void BtnNewUser_Click(object sender, RoutedEventArgs e)
    {
        if (!_userOperationBusy && CanManageUsers()) ClearUserEditor();
    }

    private void GridUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridUsers.SelectedItem is not SysUser user) return;
        TxtUserUsername.Text = user.Username;
        TxtUserUsername.IsReadOnly = true;
        TxtUserFullName.Text = user.FullName;
        TxtUserEmail.Text = user.Email;
        TxtUserPassword.Clear();
        TxtUserConfirmPassword.Clear();
        ChkUserActive.IsChecked = user.IsActive;
        CmbUserRole.SelectedIndex = -1;
        foreach (ComboBoxItem item in CmbUserRole.Items)
            if (item.Tag?.ToString() == user.Role)
                CmbUserRole.SelectedItem = item;
    }

    private SysUser ReadUserEditor(bool creating)
    {
        string username = TxtUserUsername.Text.Trim();
        string fullName = TxtUserFullName.Text.Trim();
        string email = TxtUserEmail.Text.Trim();
        string role = (CmbUserRole.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        string password = TxtUserPassword.Password;
        if (username.Length is < 3 or > 100 || username.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("Tên đăng nhập dài 3–100 ký tự và không chứa khoảng trắng.");
        if (fullName.Length is < 1 or > 255)
            throw new InvalidOperationException("Vui lòng nhập họ tên, tối đa 255 ký tự.");
        if (email.Length > 255 || !MailAddress.TryCreate(email, out var address)
            || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Vui lòng nhập địa chỉ email hợp lệ.");
        if (role is not ("Admin" or "Director" or "SalesStaff" or "InventoryStaff"))
            throw new InvalidOperationException("Vui lòng chọn vai trò.");
        if (creating || password.Length > 0 || TxtUserConfirmPassword.Password.Length > 0)
        {
            // BCrypt chỉ xử lý tối đa 72 byte mật khẩu: không cho cắt ngầm.
            if (password.Length < 8 || System.Text.Encoding.UTF8.GetByteCount(password) > 72)
                throw new InvalidOperationException("Mật khẩu cần ít nhất 8 ký tự và tối đa 72 byte UTF-8.");
            if (password != TxtUserConfirmPassword.Password)
                throw new InvalidOperationException("Hai ô mật khẩu chưa khớp.");
        }
        return new SysUser
        {
            Username = username, FullName = fullName, Email = email, Role = role,
            IsActive = ChkUserActive.IsChecked == true
        };
    }

    private async void BtnAddUser_Click(object sender, RoutedEventArgs e)
    {
        if (_userOperationBusy || !CanManageUsers()) return;
        if (GridUsers.SelectedItem != null)
        {
            MessageBox.Show("Nhấn 'Tạo mới' để nhập tài khoản mới.");
            return;
        }
        _userOperationBusy = true;
        bool saved = false;
        try
        {
            var user = ReadUserEditor(true);
            string password = TxtUserPassword.Password;
            var users = await _userRepository.GetAllAsync();
            if (users.Any(x => string.Equals(x.Username, user.Username, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Tên đăng nhập đã được sử dụng.");
            if (users.Any(x => string.Equals(x.Email, user.Email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Email đã được sử dụng.");
            await _userRepository.CreateUserAsync(user, password);
            saved = true;
            ClearUserEditor();
            await LoadUsersAsync();
            MessageBox.Show("Đã tạo tài khoản với email và mật khẩu bạn nhập. Không có email nào được gửi tự động.", "Thành công");
        }
        catch (Exception ex)
        {
            MessageBox.Show((saved ? "Tài khoản đã được tạo, nhưng giao diện chưa hoàn tất cập nhật: " : "Không tạo được tài khoản: ") + ex.Message);
        }
        finally { _userOperationBusy = false; }
    }

    private async void BtnUpdateUser_Click(object sender, RoutedEventArgs e)
    {
        if (_userOperationBusy || !CanManageUsers()) return;
        if (GridUsers.SelectedItem is not SysUser selected)
        {
            MessageBox.Show("Chọn tài khoản cần cập nhật trong bảng.");
            return;
        }
        _userOperationBusy = true;
        try
        {
            var edited = ReadUserEditor(false);
            string? password = TxtUserPassword.Password.Length == 0 ? null : TxtUserPassword.Password;
            edited.UserID = selected.UserID;
            edited.Username = selected.Username;
            if ((selected.UserID == _currentUser!.UserID || selected.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
                && (edited.Role != "Admin" || !edited.IsActive))
                throw new InvalidOperationException("Không thể tự khóa/hạ quyền tài khoản đang đăng nhập hoặc tài khoản admin gốc.");
            var users = (await _userRepository.GetAllAsync()).ToList();
            if (users.Any(x => x.UserID != selected.UserID && string.Equals(x.Email, edited.Email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Email đã được tài khoản khác sử dụng.");
            if (selected.Role == "Admin" && selected.IsActive && (edited.Role != "Admin" || !edited.IsActive)
                && !users.Any(x => x.UserID != selected.UserID && x.Role == "Admin" && x.IsActive))
                throw new InvalidOperationException("Phải giữ ít nhất một quản trị viên hoạt động.");
            if (password != null && MessageBox.Show("Xác nhận đặt mật khẩu mới cho tài khoản này?", "Đổi mật khẩu",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (!await _userRepository.UpdateUserAsync(edited, password))
                throw new InvalidOperationException("Không tìm thấy tài khoản để cập nhật.");
            ClearUserEditor();
            await LoadUsersAsync();
            MessageBox.Show("Đã cập nhật tài khoản.");
        }
        catch (Exception ex) { MessageBox.Show("Không cập nhật được: " + ex.Message); }
        finally { _userOperationBusy = false; }
    }

    private async void BtnDeleteUser_Click(object sender, RoutedEventArgs e)
    {
        if (_userOperationBusy || !CanManageUsers()) return;
        if (GridUsers.SelectedItem is not SysUser user)
        {
            MessageBox.Show("Chọn tài khoản cần xóa.");
            return;
        }
        _userOperationBusy = true;
        try
        {
            if (user.UserID == _currentUser!.UserID || user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Không được xóa tài khoản đang đăng nhập hoặc tài khoản admin gốc.");
            var users = await _userRepository.GetAllAsync();
            if (user.Role == "Admin" && user.IsActive && !users.Any(x => x.UserID != user.UserID && x.Role == "Admin" && x.IsActive))
                throw new InvalidOperationException("Phải giữ ít nhất một quản trị viên hoạt động.");
            if (MessageBox.Show($"Xóa tài khoản '{user.Username}'? Bạn cũng có thể bỏ chọn 'Đang hoạt động' rồi lưu để khóa tài khoản.",
                    "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (!await _userRepository.DeleteUserAsync(user.UserID))
                throw new InvalidOperationException("Tài khoản không còn tồn tại.");
            ClearUserEditor();
            await LoadUsersAsync();
        }
        catch (Exception ex) { MessageBox.Show("Không xóa được: " + ex.Message); }
        finally { _userOperationBusy = false; }
    }
}
