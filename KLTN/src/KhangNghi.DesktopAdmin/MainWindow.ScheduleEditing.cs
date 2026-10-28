using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KhangNghi.Core.Models;

namespace KhangNghi.DesktopAdmin
{
    public partial class MainWindow
    {
        private sealed record ScheduleDraft(bool Enabled, int Hour, int Minute, string Frequency);
        private ScheduleDraft? _savedScheduleDraft;
        private bool _scheduleEditorReady;
        private bool _applyingScheduleValues;
        private bool _scheduleOperationBusy;
        private bool _scheduleNavigationBusy;
        private bool _scheduleCloseApproved;

        protected enum ScheduleLeaveChoice { Stay, Save, Discard }

        private void InitializeScheduleEditing()
        {
            ChkEnableSchedule.Checked += ScheduleEditor_ValueChanged;
            ChkEnableSchedule.Unchecked += ScheduleEditor_ValueChanged;
            TxtScheduleHour.SelectionChanged += ScheduleEditor_ValueChanged;
            TxtScheduleMinute.SelectionChanged += ScheduleEditor_ValueChanged;
            CmbFrequency.SelectionChanged += ScheduleEditor_ValueChanged;
            Closing += MainWindow_ScheduleClosing;
            SetScheduleOperationBusy(false);
            UpdateScheduleDraftIndicator();
        }

        private ScheduleDraft ReadScheduleDraft()
        {
            string content = (CmbFrequency.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string frequency = content.Contains("Daily", StringComparison.OrdinalIgnoreCase) ? "Daily"
                : content.Contains("Weekly", StringComparison.OrdinalIgnoreCase) ? "Weekly" : "";
            return new ScheduleDraft(ChkEnableSchedule.IsChecked == true,
                TxtScheduleHour.SelectedIndex, TxtScheduleMinute.SelectedIndex, frequency);
        }

        private bool HasUnsavedScheduleChanges => _scheduleEditorReady &&
            _savedScheduleDraft != null && ReadScheduleDraft() != _savedScheduleDraft;

        private void ScheduleEditor_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (!_applyingScheduleValues) UpdateScheduleDraftIndicator();
        }

        private void UpdateScheduleDraftIndicator()
        {
            bool dirty = HasUnsavedScheduleChanges;
            TxtScheduleDraftState.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
            TxtScheduleDraftState.Text = dirty
                ? "Có thay đổi chưa lưu — nhấn Lưu lịch để áp dụng." : "";
            BtnSaveSchedule.IsEnabled = _scheduleEditorReady && !_scheduleOperationBusy && dirty;
        }

        private void SetScheduleOperationBusy(bool busy)
        {
            _scheduleOperationBusy = busy;
            bool enabled = _scheduleEditorReady && !busy;
            ChkEnableSchedule.IsEnabled = enabled;
            CmbFrequency.IsEnabled = enabled;
            TxtScheduleHour.IsEnabled = enabled;
            TxtScheduleMinute.IsEnabled = enabled;
            BtnTriggerScheduleJob.IsEnabled = enabled;
            UpdateScheduleDraftIndicator();
        }

        private void ApplyScheduleDraft(ScheduleDraft draft)
        {
            _applyingScheduleValues = true;
            try
            {
                ChkEnableSchedule.IsChecked = draft.Enabled;
                TxtScheduleHour.SelectedIndex = draft.Hour is >= 0 and <= 23 ? draft.Hour : -1;
                TxtScheduleMinute.SelectedIndex = draft.Minute is >= 0 and <= 59 ? draft.Minute : -1;
                CmbFrequency.SelectedIndex = -1;
                foreach (ComboBoxItem item in CmbFrequency.Items)
                {
                    if (item.Content?.ToString()?.Contains(draft.Frequency, StringComparison.OrdinalIgnoreCase) == true &&
                        !string.IsNullOrWhiteSpace(draft.Frequency))
                    {
                        CmbFrequency.SelectedItem = item;
                        break;
                    }
                }
            }
            finally { _applyingScheduleValues = false; }
            UpdateScheduleDraftIndicator();
        }

        private void DisplayScheduleSummary(SysSchedule schedule)
        {
            string enabled = schedule.IsEnabled ? "Đang bật" : "Đang tắt";
            string last = schedule.LastRunTime.HasValue
                ? $"Lần chạy gần nhất: {schedule.LastRunTime:dd/MM/yyyy HH:mm:ss} ({schedule.LastRunStatus ?? "Không rõ"})"
                : "Chưa có lịch sử chạy gần đây";
            TxtScheduleInfo.Text = $"Lịch đã lưu: {enabled} · [{schedule.FrequencyType}] {schedule.Hour:D2}:{schedule.Minute:D2}. {last}";
        }

        private async Task ReloadScheduleEditorAsync()
        {
            // Do not replace edits with a late/duplicate refresh.
            if (_scheduleOperationBusy || HasUnsavedScheduleChanges) return;
            SetScheduleOperationBusy(true);
            try
            {
                bool running = await _sqlAgentService.IsAgentRunningAsync();
                var schedule = await _sqlAgentService.GetScheduleAsync();
                if ((schedule.FrequencyType != "Daily" && schedule.FrequencyType != "Weekly") ||
                    schedule.Hour is < 0 or > 23 || schedule.Minute is < 0 or > 59)
                    throw new InvalidOperationException("Lịch đã lưu có tần suất hoặc giờ/phút chưa được giao diện hỗ trợ.");
                SetAgentStatus(running);
                _scheduleEditorReady = false;
                var draft = new ScheduleDraft(schedule.IsEnabled, schedule.Hour, schedule.Minute, schedule.FrequencyType);
                ApplyScheduleDraft(draft);
                _savedScheduleDraft = draft;
                _scheduleEditorReady = true;
                DisplayScheduleSummary(schedule);
                await RefreshScheduleHistoryOnlyAsync();
            }
            catch (Exception ex)
            {
                _scheduleEditorReady = false;
                TxtScheduleInfo.Text = $"Không đọc được lịch SQL Agent: {ex.Message}. Hãy vào lại tab để thử tải lại.";
            }
            finally { SetScheduleOperationBusy(false); }
        }

        private void SetAgentStatus(bool running)
        {
            TxtAgentServiceStatus.Text = running
                ? "Dịch vụ SQL Server Agent: Đang hoạt động (Running)"
                : "Dịch vụ SQL Server Agent: Đang dừng";
            TxtAgentServiceStatus.Foreground = (SolidColorBrush)FindResource(running ? "SuccessBrush" : "DangerBrush");
        }

        private async Task RefreshScheduleHistoryOnlyAsync()
        {
            try { GridAgentHistory.ItemsSource = await _sqlAgentService.GetJobHistoryAsync(20); }
            catch (Exception ex)
            {
                // A history failure is not a failed save and must not replace the editor.
                TxtScheduleInfo.Text += $" Không tải được lịch sử: {ex.Message}";
            }
        }

        private async Task<bool> SaveScheduleDraftAsync(bool showSuccess)
        {
            if (_scheduleOperationBusy || !_scheduleEditorReady) return false;
            var draft = ReadScheduleDraft();
            if (draft.Hour is < 0 or > 23 || draft.Minute is < 0 or > 59 ||
                (draft.Frequency != "Daily" && draft.Frequency != "Weekly"))
            {
                ShowScheduleMessage("Vui lòng chọn tần suất, giờ và phút hợp lệ.", "Thiếu cấu hình", MessageBoxImage.Warning);
                return false;
            }
            if (!HasUnsavedScheduleChanges) return true;
            SetScheduleOperationBusy(true);
            try
            {
                bool saved = await _sqlAgentService.UpdateScheduleAsync(draft.Enabled, draft.Hour, draft.Minute, draft.Frequency);
                if (!saved) throw new InvalidOperationException("Dịch vụ chưa xác nhận lưu lịch thành công.");
                _savedScheduleDraft = draft;
                // Keep editable fields intact. Do not reload the form after saving.
                TxtScheduleInfo.Text = $"Lịch đã lưu: {(draft.Enabled ? "Đang bật" : "Đang tắt")} · [{draft.Frequency}] {draft.Hour:D2}:{draft.Minute:D2}.";
                UpdateScheduleDraftIndicator();
                await RefreshScheduleHistoryOnlyAsync();
                if (showSuccess) ShowScheduleMessage("Đã lưu cấu hình lịch SQL Server Agent.", "Lưu lịch thành công", MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                ShowScheduleMessage($"Không xác nhận được việc lưu lịch: {ex.Message}\n\nCác lựa chọn của bạn vẫn được giữ để kiểm tra hoặc thử lại.", "Lỗi lưu lịch", MessageBoxImage.Error);
                return false;
            }
            finally { SetScheduleOperationBusy(false); }
        }

        protected virtual void ShowScheduleMessage(string text, string title, MessageBoxImage icon)
        {
            MessageBox.Show(this, text, title, MessageBoxButton.OK, icon);
        }

        protected virtual ScheduleLeaveChoice PromptUnsavedScheduleChanges()
        {
            var choice = ScheduleLeaveChoice.Stay;
            var dialog = new Window
            {
                Owner = this, Title = "Có thay đổi chưa lưu", Width = 490,
                SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false, Background = Brushes.White, FontFamily = new FontFamily("Segoe UI"), FontSize = 13
            };
            var content = new StackPanel { Margin = new Thickness(24) };
            content.Children.Add(new TextBlock
            {
                Text = "Bạn có muốn lưu thay đổi lịch ETL trước khi rời đi?",
                TextWrapping = TextWrapping.Wrap, FontSize = 16, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            });
            content.Children.Add(new TextBlock
            {
                Text = "Lưu: áp dụng vào SQL Agent. Bỏ thay đổi: giữ lịch đã lưu. Ở lại: tiếp tục chỉnh sửa.",
                TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("TextMutedBrush")
            });
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            var save = new Button { Content = "Lưu", MinWidth = 80, Margin = new Thickness(0, 0, 8, 0), Style = (Style)FindResource("PrimaryActionButtonStyle") };
            var discard = new Button { Content = "Bỏ thay đổi", MinWidth = 100, Margin = new Thickness(0, 0, 8, 0), Style = (Style)FindResource("GalleryButtonStyle") };
            var stay = new Button { Content = "Ở lại", MinWidth = 80, IsCancel = true, IsDefault = true, Style = (Style)FindResource("GalleryButtonStyle") };
            save.Click += (_, _) => { choice = ScheduleLeaveChoice.Save; dialog.DialogResult = true; };
            discard.Click += (_, _) => { choice = ScheduleLeaveChoice.Discard; dialog.DialogResult = true; };
            stay.Click += (_, _) => { dialog.DialogResult = false; };
            buttons.Children.Add(save); buttons.Children.Add(discard); buttons.Children.Add(stay);
            content.Children.Add(buttons); dialog.Content = content; dialog.ShowDialog();
            return choice;
        }

        private async Task<bool> ConfirmScheduleLeaveAsync()
        {
            if (_scheduleOperationBusy)
            {
                ShowScheduleMessage("Đang tải hoặc lưu lịch. Vui lòng chờ thao tác kết thúc.", "Đang xử lý", MessageBoxImage.Information);
                return false;
            }
            if (!HasUnsavedScheduleChanges) return true;
            switch (PromptUnsavedScheduleChanges())
            {
                case ScheduleLeaveChoice.Save: return await SaveScheduleDraftAsync(false);
                case ScheduleLeaveChoice.Discard:
                    if (_savedScheduleDraft != null) ApplyScheduleDraft(_savedScheduleDraft);
                    return true;
                default: return false;
            }
        }

        private async void MainWindow_ScheduleClosing(object? sender, CancelEventArgs e)
        {
            if (_scheduleCloseApproved || (!HasUnsavedScheduleChanges && !_scheduleOperationBusy)) return;
            e.Cancel = true;
            if (_scheduleNavigationBusy) return;
            _scheduleNavigationBusy = true;
            try
            {
                if (!await ConfirmScheduleLeaveAsync()) return;
                _scheduleCloseApproved = true;
                // Defer the second Close until the current Closing event has returned.
                _ = Dispatcher.BeginInvoke(new Action(Close));
            }
            finally { _scheduleNavigationBusy = false; }
        }
    }
}
