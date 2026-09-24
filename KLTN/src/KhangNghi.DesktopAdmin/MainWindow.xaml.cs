using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KhangNghi.Core.DTOs;
using KhangNghi.Core.Models;
using Microsoft.Win32;

namespace KhangNghi.DesktopAdmin
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
        private SysUser? _currentUser;
        private string _jwtToken = string.Empty;
        private System.Timers.Timer _schedulerTimer;
        private DateTime _lastRunDate = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();

            _httpClient = new HttpClient 
            { 
                BaseAddress = new Uri("http://localhost:5000"), 
                Timeout = TimeSpan.FromMinutes(5) 
            };
            
            // Auto-Scheduler Timer
            _schedulerTimer = new System.Timers.Timer(60000); // 1 minute
            _schedulerTimer.Elapsed += SchedulerTimer_Elapsed;
            _schedulerTimer.Start();

            // Tự động tìm đường dẫn Data.xlsx
            var candidates = new[]
            {
                @"d:\VS_source\KLTN\Data.xlsx",
                @"d:\download\Data.xlsx",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data.xlsx")
            };
            var found = candidates.FirstOrDefault(File.Exists);
            if (found != null) TxtExcelSourcePath.Text = found;
        }

        private async void SchedulerTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                if (ChkEnableSchedule.IsChecked != true || _currentUser == null) return;
                
                string hourStr = TxtScheduleHour.Text.Trim();
                string minStr = TxtScheduleMinute.Text.Trim();
                
                if (int.TryParse(hourStr, out int h) && int.TryParse(minStr, out int m))
                {
                    var now = DateTime.Now;
                    if (now.Hour == h && now.Minute == m && _lastRunDate.Date != now.Date)
                    {
                        _lastRunDate = now;
                        TxtConsoleLog.Text += $"\n[{now:HH:mm:ss}] 🕒 Kích hoạt Auto-Scheduler tự động chạy ETL...\n";
                        await RunEtlProcessAsync(TxtExcelSourcePath.Text.Trim(), "AutoScheduler");
                    }
                }
            });
        }

        // ==================== XÁC THỰC & ĐĂNG NHẬP ====================
        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtLoginUsername.Text.Trim();
            string password = TxtLoginPassword.Password.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                TxtLoginError.Text = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu!";
                return;
            }

            try
            {
                var req = new { Username = username, Password = password };
                var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", req);
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

                if (result == null || !result.Success)
                {
                    TxtLoginError.Text = result?.Message ?? "Đăng nhập thất bại!";
                    return;
                }

                if (result.Role != "Admin")
                {
                    TxtLoginError.Text = "Truy cập bị từ chối: Ứng dụng Desktop chỉ dành riêng cho Quản trị viên (Admin)!";
                    return;
                }

                _jwtToken = result.Token;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

                _currentUser = new SysUser { Username = result.Username, FullName = result.FullName, Role = result.Role };
                LoginOverlay.Visibility = Visibility.Collapsed;
                MainAppGrid.Visibility = Visibility.Visible;

                await LoadOverviewDataAsync();
                await LoadUsersAsync();
                await LoadEtlHistoryAsync();
            }
            catch (Exception ex)
            {
                TxtLoginError.Text = $"Lỗi kết nối API: {ex.Message}";
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _currentUser = null;
            _jwtToken = string.Empty;
            _httpClient.DefaultRequestHeaders.Authorization = null;
            MainAppGrid.Visibility = Visibility.Collapsed;
            LoginOverlay.Visibility = Visibility.Visible;
            TxtLoginPassword.Clear();
            TxtLoginError.Text = string.Empty;
        }

        // ==================== ĐIỀU HƯỚNG TAB ====================
        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string targetPanelName)
            {
                var navButtons = new[] { NavOverview, NavUsers, NavDatabase, NavEtl, NavSchedule };
                foreach (var b in navButtons)
                {
                    b.Background = Brushes.Transparent;
                    b.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
                }

                btn.Background = (SolidColorBrush)FindResource("PrimaryBrush");
                btn.Foreground = Brushes.White;

                PanelOverview.Visibility = Visibility.Collapsed;
                PanelUsers.Visibility = Visibility.Collapsed;
                PanelDatabase.Visibility = Visibility.Collapsed;
                PanelEtl.Visibility = Visibility.Collapsed;
                PanelSchedule.Visibility = Visibility.Collapsed;

                var panel = FindName(targetPanelName) as Grid;
                if (panel != null) panel.Visibility = Visibility.Visible;

                if (targetPanelName == "PanelOverview") _ = LoadOverviewDataAsync();
                if (targetPanelName == "PanelUsers") _ = LoadUsersAsync();
                if (targetPanelName == "PanelEtl") _ = LoadEtlHistoryAsync();
            }
        }

        // ==================== PANEL 1: TỔNG QUAN ====================
        private async Task LoadOverviewDataAsync()
        {
            try
            {
                var tables = await _httpClient.GetFromJsonAsync<List<DwhTableStatusDto>>("/api/v1/etl/tables-status");
                if (tables != null)
                {
                    GridDwhTables.ItemsSource = tables;

                    TxtFactOrderCount.Text = (tables.FirstOrDefault(t => t.TableName == "Fact_Sales")?.RowCount ?? 0).ToString("N0");
                    TxtFactShippingCount.Text = (tables.FirstOrDefault(t => t.TableName == "Fact_Shipping")?.RowCount ?? 0).ToString("N0");
                    TxtFactPurchaseCount.Text = (tables.FirstOrDefault(t => t.TableName == "Fact_Purchase")?.RowCount ?? 0).ToString("N0");
                    TxtFactInventoryCount.Text = (tables.FirstOrDefault(t => t.TableName == "Fact_Inventory")?.RowCount ?? 0).ToString("N0");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải thống kê: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== PANEL 2: QUẢN TRỊ NGƯỜI DÙNG ====================
        private async Task LoadUsersAsync()
        {
            try
            {
                var users = await _httpClient.GetFromJsonAsync<List<SysUser>>("/api/v1/auth/users");
                GridUsers.ItemsSource = users;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách người dùng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GridUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridUsers.SelectedItem is SysUser selected)
            {
                TxtUserUsername.Text = selected.Username;
                TxtUserFullName.Text = selected.FullName;
                foreach (ComboBoxItem item in CmbUserRole.Items)
                {
                    if (item.Content.ToString() == selected.Role)
                    {
                        CmbUserRole.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private async void BtnAddUser_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUserUsername.Text.Trim();
            string fullName = TxtUserFullName.Text.Trim();
            string role = ((ComboBoxItem)CmbUserRole.SelectedItem)?.Content?.ToString() ?? "Director";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(fullName))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ tên đăng nhập và họ tên!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var req = new CreateUserRequest { Username = username, FullName = fullName, Role = role, Email = $"{username}@khangnghi.com.vn", Password = "123456" };
                var res = await _httpClient.PostAsJsonAsync("/api/v1/auth/users", req);
                if (res.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Đã tạo người dùng '{username}' thành công! Mật khẩu mặc định: 123456", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadUsersAsync();
                }
                else
                {
                    string err = await res.Content.ReadAsStringAsync();
                    MessageBox.Show(err, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi thêm người dùng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnUpdateUser_Click(object sender, RoutedEventArgs e)
        {
            if (GridUsers.SelectedItem is SysUser selected)
            {
                try
                {
                    var req = new UpdateUserRequest 
                    { 
                        UserID = selected.UserID,
                        FullName = TxtUserFullName.Text.Trim(), 
                        Role = ((ComboBoxItem)CmbUserRole.SelectedItem)?.Content?.ToString() ?? selected.Role,
                        Email = selected.Email,
                        IsActive = selected.IsActive
                    };
                    
                    var res = await _httpClient.PutAsJsonAsync($"/api/v1/auth/users/{selected.UserID}", req);
                    if (res.IsSuccessStatusCode)
                    {
                        MessageBox.Show("Cập nhật thông tin người dùng thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadUsersAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi cập nhật: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (GridUsers.SelectedItem is SysUser selected)
            {
                if (selected.Username == "admin")
                {
                    MessageBox.Show("Không được phép xóa tài khoản quản trị tối cao (admin)!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MessageBox.Show($"Bạn có chắc chắn muốn xóa người dùng '{selected.Username}'?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _httpClient.DeleteAsync($"/api/v1/auth/users/{selected.UserID}");
                    await LoadUsersAsync();
                }
            }
        }

        // ==================== PANEL 3: KẾT NỐI CSDL ====================
        private void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            TxtConnectionResult.Text = "Tính năng Test Connection CSDL đã bị vô hiệu hóa vì ứng dụng hiện sử dụng REST API thay vì kết nối trực tiếp.";
            TxtConnectionResult.Foreground = (SolidColorBrush)FindResource("DangerBrush");
        }

        private void BtnSaveConnection_Click(object sender, RoutedEventArgs e)
        {
            string url = TxtConnectionString.Text.Trim();
            try {
                _httpClient.BaseAddress = new Uri(url);
                MessageBox.Show("Đã lưu URL API Server mới!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) {
                MessageBox.Show($"URL không hợp lệ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== PANEL 4: KÍCH HOẠT ETL ====================
        private void BtnBrowseExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
                Title = "Chọn tệp dữ liệu Excel"
            };

            if (dlg.ShowDialog() == true) TxtExcelSourcePath.Text = dlg.FileName;
        }

        private async void BtnRunEtl_Click(object sender, RoutedEventArgs e)
        {
            await RunEtlProcessAsync(TxtExcelSourcePath.Text.Trim(), "Admin Desktop");
        }

        private async Task RunEtlProcessAsync(string filePath, string triggeredBy)
        {
            BtnRunEtl.IsEnabled = false;
            EtlProgressBar.IsIndeterminate = true;
            TxtEtlStatus.Text = "Đang gửi yêu cầu ETL đến Server API...";
            if (triggeredBy != "AutoScheduler") TxtConsoleLog.Text = string.Empty;

            try
            {
                string encodedPath = Uri.EscapeDataString(filePath);
                var res = await _httpClient.PostAsync($"/api/v1/etl/trigger?filePath={encodedPath}&triggeredBy={triggeredBy}", null);
                
                EtlProgressBar.IsIndeterminate = false;
                EtlProgressBar.Value = 100;
                BtnRunEtl.IsEnabled = true;

                if (res.IsSuccessStatusCode)
                {
                    var result = await res.Content.ReadFromJsonAsync<EtlResultDto>();
                    if (result != null && result.Success)
                    {
                        TxtEtlStatus.Text = $"✅ {result.Message}";
                        TxtConsoleLog.Text += $"\n[{DateTime.Now:HH:mm:ss}] API trả về: {result.Message}\nĐã xử lý: {result.RowsProcessed} dòng.\nThời gian: {result.Duration.TotalSeconds:0.##}s";
                        if (triggeredBy != "AutoScheduler") MessageBox.Show(result.Message, "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        TxtEtlStatus.Text = $"❌ {result?.Message}";
                        TxtConsoleLog.Text += $"\n[{DateTime.Now:HH:mm:ss}] Lỗi từ API: {result?.Message}";
                    }
                }
                else
                {
                    TxtEtlStatus.Text = "❌ Lỗi khi gọi API.";
                    TxtConsoleLog.Text += $"\n[{DateTime.Now:HH:mm:ss}] Error: HTTP {(int)res.StatusCode}";
                }

                await LoadEtlHistoryAsync();
                await LoadOverviewDataAsync();
            }
            catch (Exception ex)
            {
                EtlProgressBar.IsIndeterminate = false;
                BtnRunEtl.IsEnabled = true;
                TxtEtlStatus.Text = $"❌ Lỗi: {ex.Message}";
                TxtConsoleLog.Text += $"\n[{DateTime.Now:HH:mm:ss}] Network Exception: {ex.Message}";
            }
        }

        private async Task LoadEtlHistoryAsync()
        {
            try
            {
                var logs = await _httpClient.GetFromJsonAsync<List<SysEtlLog>>("/api/v1/etl/logs?limit=20");
                GridEtlHistory.ItemsSource = logs;
            }
            catch { }
        }

        // ==================== PANEL 5: LẬP LỊCH ====================
        private void BtnSaveSchedule_Click(object sender, RoutedEventArgs e)
        {
            bool isEnabled = ChkEnableSchedule.IsChecked ?? false;
            string freq = ((ComboBoxItem)CmbFrequency.SelectedItem)?.Content?.ToString() ?? "Mỗi đêm (Daily)";
            string hour = TxtScheduleHour.Text.Trim();
            string minute = TxtScheduleMinute.Text.Trim();

            if (isEnabled)
            {
                TxtScheduleInfo.Text = $"✅ Đã kích hoạt lập lịch: Chu kỳ [{freq}] chạy vào lúc {hour.PadLeft(2, '0')}:{minute.PadLeft(2, '0')}.";
                TxtScheduleInfo.Foreground = (SolidColorBrush)FindResource("SuccessBrush");
            }
            else
            {
                TxtScheduleInfo.Text = "⚠️ Đã tắt tính năng tự động cập nhật định kỳ.";
                TxtScheduleInfo.Foreground = (SolidColorBrush)FindResource("DangerBrush");
            }

            MessageBox.Show("Cấu hình lập lịch tự động đã được lưu thành công trên ứng dụng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
