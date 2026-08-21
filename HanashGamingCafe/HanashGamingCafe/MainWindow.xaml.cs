using Postgrest.Attributes;
using Postgrest.Models;
using Supabase.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace HanashGamingCafe
{
    public partial class MainWindow : Window
    {
        public class SessionOrderItem
        {
            public string Name { get; set; }
            public int Qty { get; set; }
            public decimal Total { get; set; }
        }

        public ObservableCollection<Station> StationsList { get; set; } = new ObservableCollection<Station>();

        private Dictionary<object, List<SessionOrderItem>> _sessionExtraOrders = new Dictionary<object, List<SessionOrderItem>>();

        public SessionModel _selectedSession = null;
        private string _currentDeviceCategoryAdding = "بلايستيشن";

        private DispatcherTimer _liveTimer;
        private List<SessionModel> _activeSessionsList = new List<SessionModel>();

        public MainWindow()
        {
            InitializeComponent();
            ApiServer.StartServer();
            SetupLiveTimer();
            Loaded += MainWindow_Loaded;
            this.KeyDown += MainWindow_KeyDown;
        }

        private async void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F12)
            {
                await GenerateMockOrdersAsync(5);
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadActiveSessionsAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _liveTimer?.Stop();
            ApiServer.StopServer();
        }

        private void SetupLiveTimer()
        {
            _liveTimer = new DispatcherTimer();
            _liveTimer.Interval = TimeSpan.FromSeconds(1);
            _liveTimer.Tick += LiveTimer_Tick;
            _liveTimer.Start();
        }

        private void LiveTimer_Tick(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                foreach (UIElement child in PanelActiveSessions.Children)
                {
                    if (child is FrameworkElement element && element.Tag is SessionModel session)
                    {
                        TextBlock timerTextBlock = FindTimerTextBlock(element);

                        if (IsRoundBasedSession(session))
                        {
                            if (timerTextBlock != null)
                            {
                                int currentRounds = session.RoundsCount > 0 ? session.RoundsCount : 1;
                                timerTextBlock.Text = $"🎮 {currentRounds} جولة";
                            }
                        }
                        else
                        {
                            if (session.StartTime != DateTime.MinValue)
                            {
                                DateTime startTimeLocal = session.StartTime.Kind == DateTimeKind.Utc
                                    ? session.StartTime.ToLocalTime()
                                    : session.StartTime;

                                DateTime referenceTime;
                                if (session.Status == "pending_payment" && session.EndTime.HasValue)
                                {
                                    referenceTime = DateTime.SpecifyKind(session.EndTime.Value, DateTimeKind.Utc).ToLocalTime();
                                }
                                else
                                {
                                    referenceTime = DateTime.Now;
                                }

                                TimeSpan elapsed = referenceTime - startTimeLocal;
                                if (elapsed.TotalSeconds < 0) elapsed = TimeSpan.Zero;

                                if (timerTextBlock != null)
                                {
                                    string frozenMark = session.Status == "pending_payment" ? " (متوقف)" : "";
                                    timerTextBlock.Text = $"⏱️ {(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}{frozenMark}";
                                }
                            }
                        }
                    }
                }

                if (_selectedSession != null)
                {
                    RecalculateSelectedSessionBill();
                }
            });
        }

        private TextBlock FindTimerTextBlock(DependencyObject parent)
        {
            if (parent == null) return null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is TextBlock textBlock)
                {
                    if (textBlock.Name == "TxtLiveTimer" ||
                        textBlock.Text.Contains(":") ||
                        textBlock.Text.Contains("⏱️") ||
                        textBlock.Text.Contains("جولة") ||
                        textBlock.Text.Contains("جولات") ||
                        textBlock.Text.Contains("🎮") ||
                        textBlock.Text.Contains("--:--"))
                    {
                        return textBlock;
                    }
                }

                var result = FindTimerTextBlock(child);
                if (result != null) return result;
            }
            return null;
        }

        private void UpdateActiveSessionCardsUI()
        {
            PanelActiveSessions.Children.Clear();

            foreach (var session in _activeSessionsList)
            {
                bool isPending = session.Status == "pending_payment";

                DateTime referenceTimeForCard;
                if (isPending && session.EndTime.HasValue)
                {
                    referenceTimeForCard = DateTime.SpecifyKind(session.EndTime.Value, DateTimeKind.Utc).ToLocalTime();
                }
                else
                {
                    referenceTimeForCard = DateTime.Now;
                }
                TimeSpan elapsed = referenceTimeForCard - session.StartTime;
                if (elapsed.TotalSeconds < 0) elapsed = TimeSpan.Zero;

                var border = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isPending ? "#3D2E00" : "#252538")),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Margin = new Thickness(5),
                    Cursor = Cursors.Hand,
                    Tag = session
                };

                if (_selectedSession != null && _selectedSession.Id.Equals(session.Id))
                {
                    border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC00"));
                    border.BorderThickness = new Thickness(2);
                }

                var stack = new StackPanel();

                var title = new TextBlock
                {
                    Text = $"{session.DeviceType} - {session.DeviceName}",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC00")),
                    FontWeight = FontWeights.Bold,
                    FontSize = 14
                };
                stack.Children.Add(title);

                if (isPending)
                {
                    var pendingLabel = new TextBlock
                    {
                        Text = "⏳ بانتظار الحساب",
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA800")),
                        FontWeight = FontWeights.Bold,
                        FontSize = 12,
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    stack.Children.Add(pendingLabel);
                }

                TextBlock detailText;
                if (IsTimeBasedSession(session))
                {
                    detailText = new TextBlock
                    {
                        Text = $"⏱️ {elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}",
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF88")),
                        FontWeight = FontWeights.Bold,
                        FontSize = 15,
                        Margin = new Thickness(0, 6, 0, 0)
                    };
                }
                else if (IsRoundBasedSession(session))
                {
                    detailText = new TextBlock
                    {
                        Text = $"🎯 جولات: {session.RoundsCount}",
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")),
                        FontWeight = FontWeights.Bold,
                        FontSize = 15,
                        Margin = new Thickness(0, 6, 0, 0)
                    };
                }
                else
                {
                    detailText = new TextBlock
                    {
                        Text = $"☕ طلبات ومشروبات",
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E67E22")),
                        FontWeight = FontWeights.Bold,
                        FontSize = 14,
                        Margin = new Thickness(0, 6, 0, 0)
                    };
                }

                var startText = new TextBlock
                {
                    Text = $"فتح: {session.StartTime:hh:mm tt}",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0A0B0")),
                    FontSize = 11,
                    Margin = new Thickness(0, 3, 0, 0)
                };

                stack.Children.Add(detailText);
                stack.Children.Add(startText);
                border.Child = stack;

                border.MouseDown += (s, e) => SelectSessionForCheckout(session);

                PanelActiveSessions.Children.Add(border);
            }
        }

        public bool IsRoundBasedSession(SessionModel session)
        {
            if (session == null || string.IsNullOrEmpty(session.DeviceType))
                return false;

            string cat = session.DeviceType.Trim().ToLower();
            return cat == "bi" || cat == "billiard" || cat == "ps_rounds";
        }

        public bool IsTimeBasedSession(SessionModel session)
        {
            return !IsRoundBasedSession(session);
        }

        private void BtnOpenSalesHistory_Click(object sender, RoutedEventArgs e)
        {
            var salesWin = new SalesHistoryWindow();
            salesWin.Owner = this;
            salesWin.ShowDialog();
        }

        private async void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string tag = btn.Tag.ToString();
                ResetNavButtons();

                ViewCashier.Visibility = Visibility.Collapsed;
                ViewPlayStation.Visibility = Visibility.Collapsed;
                ViewBilliards.Visibility = Visibility.Collapsed;
                ViewSeating.Visibility = Visibility.Collapsed;

                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));

                switch (tag)
                {
                    case "Cashier":
                        ViewCashier.Visibility = Visibility.Visible;
                        await LoadActiveSessionsAsync();
                        break;
                    case "PlayStation":
                        ViewPlayStation.Visibility = Visibility.Visible;
                        await LoadCategoryDevicesAsync("ps", PanelPlayStationDevices);
                        break;
                    case "Billiards":
                        ViewBilliards.Visibility = Visibility.Visible;
                        await LoadCategoryDevicesAsync("bi", PanelBilliardsDevices);
                        break;
                    case "Seating":
                        ViewSeating.Visibility = Visibility.Visible;
                        await LoadCategoryDevicesAsync("tab", PanelSeatingDevices);
                        break;
                    case "Inventory":
                        var invWindow = new InventoryWindow();
                        invWindow.Owner = this;
                        invWindow.ShowDialog();
                        break;
                }
            }
        }

        private void ResetNavButtons()
        {
            var defaultColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1C29"));
            BtnNavCashier.Background = defaultColor;
            BtnNavPlayStation.Background = defaultColor;
            BtnNavBilliards.Background = defaultColor;
            BtnNavSeating.Background = defaultColor;
            BtnNavInventory.Background = defaultColor;
        }

        private async Task LoadCategoryDevicesAsync(string category, WrapPanel panelTarget)
        {
            try
            {
                panelTarget.Children.Clear();
                await SupabaseService.InitializeAsync();

                var response = await SupabaseService.Instance
                    .From<DeviceModel>()
                    .Where(d => d.Type == category)
                    .Get();

                foreach (var device in response.Models)
                {
                    var card = CreateDeviceCard(device);
                    panelTarget.Children.Add(card);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء جلب الأجهزة: {ex.Message}");
            }
        }

        private Border CreateDeviceCard(DeviceModel device)
        {
            bool isBusy = device.Status == "busy";
            string bgHex = isBusy ? "#E74C3C" : "#2ECC71";

            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(8),
                Cursor = Cursors.Hand,
                Tag = device
            };

            var stack = new StackPanel();

            var title = new TextBlock
            {
                Text = device.Name,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var statusText = new TextBlock
            {
                Text = isBusy ? "🔴 مشغول (انقر للمحاسبة)" : "🟢 متاح (انقر للفتح)",
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            string rateUnit = "طلبات فقط";
            if (device.Type == "ps") rateUnit = $"{device.HourlyRate:N0} د.ع / ساعة أو جولة";
            else if (device.Type == "bi") rateUnit = $"{device.HourlyRate:N0} د.ع / جولة";

            var rateText = new TextBlock
            {
                Text = rateUnit,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1C40F")),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stack.Children.Add(title);
            stack.Children.Add(statusText);
            stack.Children.Add(rateText);
            border.Child = stack;

            border.MouseDown += async (s, e) => await OnDeviceCardClickedAsync(device);

            return border;
        }

        private static readonly System.Threading.SemaphoreSlim _sessionLock = new System.Threading.SemaphoreSlim(1, 1);

        private async Task OnDeviceCardClickedAsync(DeviceModel device)
        {
            await _sessionLock.WaitAsync();

            try
            {
                if (device == null) return;

                if (device.Status == "available" || device.Status == "free")
                {
                    string selectedDeviceType = device.Type;

                    if (device.Type == "ps")
                    {
                        var choiceResult = MessageBox.Show(
                            $"اختر نظام اللعب للجهاز ({device.Name}):\n\n" +
                            "[ نعم ]  : نظام الوقت (ساعات / دقائق) ⏱️\n" +
                            "[ لا ]    : نظام الجولات (عدد الجولات) 🎮",
                            "تحديد نوع الجلسة",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Question);

                        if (choiceResult == MessageBoxResult.Yes)
                        {
                            selectedDeviceType = "ps";
                        }
                        else if (choiceResult == MessageBoxResult.No)
                        {
                            selectedDeviceType = "ps_rounds";
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        var result = MessageBox.Show($"هل تريد فتح ({device.Name}) الآن؟", "فتح طاولة / جهاز", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result != MessageBoxResult.Yes) return;
                    }

                    await SupabaseService.InitializeAsync();

                    var activeCheck = await SupabaseService.Instance
                        .From<SessionModel>()
                        .Where(s => s.ItemId == device.Id && s.IsActive == true)
                        .Get();

                    if (activeCheck.Models != null && activeCheck.Models.Count > 0)
                    {
                        MessageBox.Show($"الجلسة مفتوحة بالفعل لـ ({device.Name})!", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        NavButton_Click(BtnNavCashier, new RoutedEventArgs());
                        return;
                    }

                    decimal applicableRate = (selectedDeviceType == "ps_rounds") ? device.RoundRate : device.HourlyRate;

                    var newSession = new SessionModel
                    {
                        ItemId = device.Id,
                        DeviceName = device.Name,
                        DeviceType = selectedDeviceType,
                        StartTime = DateTime.UtcNow,
                        HourlyRate = applicableRate,
                        RoundsCount = 1,
                        IsActive = true,
                        Status = "active"
                    };

                    await SupabaseService.Instance.From<SessionModel>().Insert(newSession);

                    device.Status = "busy";
                    await SupabaseService.Instance.From<DeviceModel>().Where(d => d.Id == device.Id).Update(device);

                    MessageBox.Show($"تم فتح {device.Name} بنجاح! 🚀", "تم الفتح", MessageBoxButton.OK, MessageBoxImage.Information);

                    NavButton_Click(BtnNavCashier, new RoutedEventArgs());
                }
                else
                {
                    NavButton_Click(BtnNavCashier, new RoutedEventArgs());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء فتح الجلسة:\n{ex.Message}\n\nالتفاصيل:\n{ex.InnerException?.Message}",
                                "خطأ في الفتح", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private void BtnAddNewDevice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string rawCategory = btn.Tag.ToString();

                switch (rawCategory)
                {
                    case "بلايستيشن":
                        _currentDeviceCategoryAdding = "ps";
                        break;
                    case "بلياردو":
                        _currentDeviceCategoryAdding = "bi";
                        break;
                    case "طاولة":
                    default:
                        _currentDeviceCategoryAdding = "tab";
                        break;
                }
            }
        }

        private async Task LoadActiveSessionsAsync()
        {
            try
            {
                await SupabaseService.InitializeAsync();

                var response = await SupabaseService.Instance
                    .From<SessionModel>()
                    .Filter("status", Postgrest.Constants.Operator.In, new List<object> { "active", "pending_payment" })
                    .Get();

                _activeSessionsList = response.Models;

                foreach (var session in _activeSessionsList)
                {
                    if (session.StartTime != DateTime.MinValue)
                    {
                        session.StartTime = session.StartTime.ToLocalTime();
                    }

                    if (!_sessionExtraOrders.ContainsKey(session.Id))
                    {
                        _sessionExtraOrders[session.Id] = new List<SessionOrderItem>();
                    }
                }

                UpdateActiveSessionCardsUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading sessions: {ex.Message}");
            }
        }

        private async void SelectSessionForCheckout(SessionModel session)
        {
            if (session == null) return;

            _selectedSession = session;
            TxtSelectedSessionTitle.Text = $"حساب: {session.DeviceType} ({session.DeviceName})";

            if (!_sessionExtraOrders.ContainsKey(session.Id))
            {
                _sessionExtraOrders[session.Id] = new List<SessionOrderItem>();
            }

            if (IsRoundBasedSession(session))
            {
                RoundsControlBorder.Visibility = Visibility.Visible;
                TxtRoundsCount.Text = session.RoundsCount.ToString();
            }
            else
            {
                RoundsControlBorder.Visibility = Visibility.Collapsed;
            }

            await SyncSessionOrdersFromSupabaseAsync(session.Id);

            RecalculateSelectedSessionBill();
            UpdateActiveSessionCardsUI();
        }

        private async void BtnIncrementRound_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSession != null && IsRoundBasedSession(_selectedSession))
            {
                _selectedSession.RoundsCount++;
                TxtRoundsCount.Text = _selectedSession.RoundsCount.ToString();

                await SupabaseService.Instance.From<SessionModel>().Where(s => s.Id == _selectedSession.Id).Update(_selectedSession);
                RecalculateSelectedSessionBill();
                UpdateActiveSessionCardsUI();
            }
        }

        private async void BtnDecrementRound_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSession != null && IsRoundBasedSession(_selectedSession) && _selectedSession.RoundsCount > 1)
            {
                _selectedSession.RoundsCount--;
                TxtRoundsCount.Text = _selectedSession.RoundsCount.ToString();

                await SupabaseService.Instance.From<SessionModel>().Where(s => s.Id == _selectedSession.Id).Update(_selectedSession);
                RecalculateSelectedSessionBill();
                UpdateActiveSessionCardsUI();
            }
        }

        public void RecalculateSelectedSessionBill()
        {
            if (_selectedSession == null) return;

            decimal totalSessionPrice = 0;
            decimal hourlyRate = _selectedSession.HourlyRate;

            if (IsRoundBasedSession(_selectedSession))
            {
                int rounds = _selectedSession.RoundsCount > 0 ? _selectedSession.RoundsCount : 1;
                totalSessionPrice = hourlyRate * rounds;
            }
            else
            {
                DateTime startUtc = ConvertToUtc(_selectedSession.StartTime);
                DateTime endUtc;

                if (_selectedSession.Status == "pending_payment")
                {
                    if (_selectedSession.EndTime.HasValue && _selectedSession.EndTime.Value != DateTime.MinValue)
                    {
                        endUtc = ConvertToUtc(_selectedSession.EndTime.Value);
                    }
                    else
                    {
                        endUtc = DateTime.UtcNow;
                        _selectedSession.EndTime = endUtc;

                        Task.Run(async () =>
                        {
                            try
                            {
                                await SupabaseService.Instance
                                    .From<SessionModel>()
                                    .Where(s => s.Id == _selectedSession.Id)
                                    .Set(s => s.EndTime, endUtc)
                                    .Update();
                            }
                            catch { }
                        });
                    }
                }
                else
                {
                    endUtc = DateTime.UtcNow;
                }

                TimeSpan elapsed = endUtc - startUtc;
                double totalMinutes = elapsed.TotalMinutes;

                if (totalMinutes < 0) totalMinutes = 0;

                decimal minuteRate = hourlyRate / 60m;
                decimal rawCost = (decimal)totalMinutes * minuteRate;

                totalSessionPrice = Math.Round(rawCost / 250m) * 250m;
            }

            decimal extrasTotal = 0;
            if (_sessionExtraOrders.ContainsKey(_selectedSession.Id))
            {
                extrasTotal = _sessionExtraOrders[_selectedSession.Id].Sum(x => x.Total);
            }

            if (TxtTotalAmount != null)
            {
                TxtTotalAmount.Text = (totalSessionPrice + extrasTotal).ToString("N0") + " د.ع";
            }
        }

        private DateTime ConvertToUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc)
                return dt;

            if (dt.Kind == DateTimeKind.Local)
                return dt.ToUniversalTime();

            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        private async void BtnAddService_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSession == null)
            {
                MessageBox.Show("يرجى تحديد جلسة أولاً من القائمة!", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var menuWin = new MenuWindow();
            menuWin.Owner = this;

            if (menuWin.ShowDialog() == true && menuWin.SelectedProduct != null)
            {
                var product = menuWin.SelectedProduct;

                try
                {
                    await SupabaseService.InitializeAsync();

                    var existingOrderResponse = await SupabaseService.Instance
                        .From<HanashGamingCafe.SessionOrderDatabaseModel>()
                        .Where(x => x.SessionId == _selectedSession.Id && x.ItemName == product.Name)
                        .Get();

                    var existingDbItem = existingOrderResponse.Models.FirstOrDefault();

                    if (existingDbItem != null)
                    {
                        double newQty = existingDbItem.Qty + 1;
                        double newTotalPrice = newQty * existingDbItem.Price;

                        await SupabaseService.Instance
                            .From<HanashGamingCafe.SessionOrderDatabaseModel>()
                            .Where(x => x.Id == existingDbItem.Id)
                            .Set(x => x.Qty, newQty)
                            .Set(x => x.TotalPrice, newTotalPrice)
                            .Update();
                    }
                    else
                    {
                        double unitPrice = Convert.ToDouble(product.SellingPrice);
                        double initialQty = 1;

                        var newOrderItem = new HanashGamingCafe.SessionOrderDatabaseModel
                        {
                            Id = Guid.NewGuid().ToString(),
                            SessionId = _selectedSession.Id,
                            ItemName = product.Name,
                            Qty = initialQty,
                            Price = unitPrice,
                            TotalPrice = initialQty * unitPrice
                        };

                        await SupabaseService.Instance
                            .From<HanashGamingCafe.SessionOrderDatabaseModel>()
                            .Insert(newOrderItem);
                    }

                    await SyncSessionOrdersFromSupabaseAsync(_selectedSession.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"حدث خطأ أثناء حفظ الطلب في قاعدة البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task<bool> HasOpenShiftAsync()
        {
            try
            {
                await SupabaseService.InitializeAsync();

                var openShiftResponse = await SupabaseService.Instance
                    .From<ShiftModel>()
                    .Where(s => s.Status == "open")
                    .Get();

                return openShiftResponse.Models != null && openShiftResponse.Models.Any();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء التحقق من حالة الشفت: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private async void BtnFinishSession_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSession == null) return;

            bool hasOpenShift = await HasOpenShiftAsync();
            if (!hasOpenShift) return;

            try
            {
                await SupabaseService.InitializeAsync();

                var currentShiftResponse = await SupabaseService.Instance
                    .From<ShiftModel>()
                    .Where(s => s.Status == "open")
                    .Order(s => s.StartTime, Postgrest.Constants.Ordering.Descending)
                    .Get();

                var currentShift = currentShiftResponse.Models.FirstOrDefault();
                string cashierName = currentShift?.CashierName ?? "غير محدد";

                string rawAmount = TxtTotalAmount.Text
                    .Replace("د.ع", "")
                    .Replace(",", "")
                    .Trim();

                decimal.TryParse(rawAmount, out decimal finalTotal);

                TimeSpan duration = TimeSpan.Zero;
                decimal sessionCost = 0;

                if (IsRoundBasedSession(_selectedSession))
                {
                    int rounds = _selectedSession.RoundsCount > 0 ? _selectedSession.RoundsCount : 1;
                    sessionCost = _selectedSession.HourlyRate * rounds;
                    duration = TimeSpan.FromDays(rounds);
                }
                else
                {
                    DateTime startUtc = ConvertToUtc(_selectedSession.StartTime);
                    DateTime endUtc = (_selectedSession.Status == "pending_payment" && _selectedSession.EndTime.HasValue)
                        ? ConvertToUtc(_selectedSession.EndTime.Value)
                        : DateTime.UtcNow;

                    duration = endUtc - startUtc;
                    if (duration.TotalSeconds < 0) duration = TimeSpan.Zero;

                    decimal hourlyRate = _selectedSession.HourlyRate > 0 ? _selectedSession.HourlyRate : 5000m;
                    decimal minuteRate = hourlyRate / 60m;
                    sessionCost = Math.Round(((decimal)duration.TotalMinutes * minuteRate) / 250m) * 250m;
                }

                List<SessionOrderItem> sessionItems = _sessionExtraOrders.ContainsKey(_selectedSession.Id)
                    ? new List<SessionOrderItem>(_sessionExtraOrders[_selectedSession.Id])
                    : new List<SessionOrderItem>();

                try
                {
                    InvoicePrinter.PrintReceipt(
                        cashierName: cashierName,
                        deviceName: _selectedSession.DeviceName,
                        deviceType: _selectedSession.DeviceType,
                        durationOrRounds: duration,
                        sessionCost: sessionCost,
                        items: sessionItems,
                        grandTotal: finalTotal
                    );
                }
                catch (Exception printEx)
                {
                    MessageBox.Show($"تم حفظ الحساب ولكن تعذر طباعة الفاتورة:\n{printEx.Message}", "تنبيه طباعة", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                if (_sessionExtraOrders.ContainsKey(_selectedSession.Id))
                {
                    var orders = _sessionExtraOrders[_selectedSession.Id];

                    foreach (var orderItem in orders)
                    {
                        if (string.IsNullOrWhiteSpace(orderItem.Name)) continue;

                        string cleanName = orderItem.Name.Trim();

                        var productResponse = await SupabaseService.Instance
                            .From<Product>()
                            .Where(p => p.Name == cleanName)
                            .Get();

                        var product = productResponse.Models.FirstOrDefault();

                        if (product != null)
                        {
                            decimal newStock = product.StockQuantity - orderItem.Qty;
                            if (newStock < 0) newStock = 0m;

                            await SupabaseService.Instance
                                .From<Product>()
                                .Where(p => p.Id == product.Id)
                                .Set(p => p.StockQuantity, newStock)
                                .Update();
                        }

                        double singleUnitPrice = orderItem.Qty > 0 ? (double)(orderItem.Total / orderItem.Qty) : 0;

                        var orderDbRecord = new SessionOrderDatabaseModel
                        {
                            SessionId = _selectedSession.Id,
                            ItemName = cleanName,
                            Qty = orderItem.Qty,
                            Price = singleUnitPrice,
                            TotalPrice = (double)orderItem.Total,
                            CreatedAt = DateTime.UtcNow
                        };

                        await SupabaseService.Instance
                            .From<SessionOrderDatabaseModel>()
                            .Insert(orderDbRecord);
                    }
                }

                await SupabaseService.Instance
                    .From<SessionModel>()
                    .Where(s => s.Id == _selectedSession.Id)
                    .Set(s => s.Status, "completed")
                    .Set(s => s.IsActive, false)
                    .Set(s => s.EndTime, DateTime.UtcNow)
                    .Set(s => s.TotalAmount, finalTotal)
                    .Set(s => s.PaymentMethod, "كاش")
                    .Update();

                if (!string.IsNullOrWhiteSpace(_selectedSession.DeviceName))
                {
                    string deviceCleanName = _selectedSession.DeviceName.Trim();

                    var deviceResponse = await SupabaseService.Instance
                        .From<DeviceModel>()
                        .Where(d => d.Name == deviceCleanName)
                        .Get();

                    var device = deviceResponse.Models.FirstOrDefault();

                    if (device != null)
                    {
                        await SupabaseService.Instance
                            .From<DeviceModel>()
                            .Where(d => d.Id == device.Id)
                            .Set(d => d.Status, "free")
                            .Update();
                    }
                }

                if (_sessionExtraOrders.ContainsKey(_selectedSession.Id))
                {
                    _sessionExtraOrders.Remove(_selectedSession.Id);
                }

                _selectedSession = null;
                if (RoundsControlBorder != null) RoundsControlBorder.Visibility = Visibility.Collapsed;
                GridCurrentSessionOrders.ItemsSource = null;
                TxtSelectedSessionTitle.Text = "اختر جلسة من القائمة لعرض الفاتورة";
                TxtTotalAmount.Text = "0 د.ع";

                await LoadActiveSessionsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء إغلاق الجلسة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshCashier_Click(object sender, RoutedEventArgs e)
        {
            await LoadActiveSessionsAsync();

            if (_selectedSession != null)
            {
                await SyncSessionOrdersFromSupabaseAsync(_selectedSession.Id);
                RecalculateSelectedSessionBill();
            }
            else
            {
                System.Windows.MessageBox.Show("يرجى اختيار طاولة/جلسة من القائمة أولاً!");
            }
        }

        public async Task SyncSessionOrdersFromSupabaseAsync(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId)) return;

                var currentOrdersList = new List<SessionOrderItem>();

                if (Guid.TryParse(sessionId, out _))
                {
                    var response = await SupabaseService.Instance
                        .From<SessionOrderDatabaseModel>()
                        .Filter("session_id", Postgrest.Constants.Operator.Equals, sessionId)
                        .Get();

                    if (response != null && response.Models != null)
                    {
                        foreach (var item in response.Models)
                        {
                            double qty = item.Qty > 0 ? item.Qty : 1;
                            decimal unitPrice = Convert.ToDecimal(item.Price);
                            decimal totalItemPrice = Convert.ToDecimal(qty) * unitPrice;

                            string name = !string.IsNullOrEmpty(item.ItemName) ? item.ItemName : "طلب آيباد";

                            currentOrdersList.Add(new SessionOrderItem
                            {
                                Name = name,
                                Qty = (int)Math.Round(qty),
                                Total = totalItemPrice
                            });
                        }
                    }
                }

                if (_sessionExtraOrders.ContainsKey(sessionId))
                {
                    _sessionExtraOrders[sessionId] = currentOrdersList;
                }
                else
                {
                    _sessionExtraOrders.Add(sessionId, currentOrdersList);
                }

                Dispatcher.Invoke(() =>
                {
                    if (_selectedSession != null && _selectedSession.Id == sessionId)
                    {
                        GridCurrentSessionOrders.ItemsSource = null;
                        GridCurrentSessionOrders.ItemsSource = currentOrdersList;

                        RecalculateSelectedSessionBill();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في جلب الطلبات: {ex.Message}");
            }
        }

        #region نقل الفاتورة / تغيير الطاولة

        private async void BtnTransferSession_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSession == null)
            {
                MessageBox.Show("يرجى تحديد الجلسة المراد نقلها من قائمة الجلسات أولاً!", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await SupabaseService.InitializeAsync();

                var availableDevicesResponse = await SupabaseService.Instance
                    .From<DeviceModel>()
                    .Where(d => d.Status == "free")
                    .Get();

                var availableDevices = availableDevicesResponse.Models;

                if (availableDevices == null || availableDevices.Count == 0)
                {
                    MessageBox.Show("لا توجد طاولات أو أجهزة متاحة حالياً لنقل الفاتورة إليها!", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var transferWin = new TransferWindow(availableDevices);
                transferWin.Owner = this;

                if (transferWin.ShowDialog() == true && transferWin.SelectedDevice != null)
                {
                    var targetDevice = transferWin.SelectedDevice;
                    await TransferSessionAsync(_selectedSession, targetDevice);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء فحص الأجهزة المتاحة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task TransferSessionAsync(SessionModel currentSession, DeviceModel targetDevice)
        {
            try
            {
                await SupabaseService.InitializeAsync();

                decimal currentCost = 0;
                string transferNote = "";

                if (IsTimeBasedSession(currentSession))
                {
                    DateTime startTimeLocal = currentSession.StartTime.Kind == DateTimeKind.Utc
                        ? currentSession.StartTime.ToLocalTime()
                        : currentSession.StartTime;

                    TimeSpan elapsed = DateTime.Now - startTimeLocal;
                    if (elapsed.TotalSeconds < 0) elapsed = TimeSpan.Zero;

                    decimal hourlyRate = currentSession.HourlyRate;
                    double totalMinutes = elapsed.TotalMinutes;
                    decimal minuteRate = hourlyRate / 60m;

                    currentCost = Math.Round(((decimal)totalMinutes * minuteRate) / 250m) * 250m;
                    transferNote = $"وقت {currentSession.DeviceName} ({elapsed.Hours:D2}:{elapsed.Minutes:D2})";
                }
                else if (IsRoundBasedSession(currentSession))
                {
                    decimal roundRate = currentSession.HourlyRate;
                    currentCost = currentSession.RoundsCount * roundRate;

                    transferNote = $"جولات {currentSession.DeviceName} (عدد {currentSession.RoundsCount})";
                }

                if (currentCost > 0)
                {
                    var transferredOrderItem = new HanashGamingCafe.SessionOrderDatabaseModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        SessionId = currentSession.Id,
                        ItemName = transferNote,
                        Qty = 1.0,
                        Price = (double)currentCost,
                        TotalPrice = (double)currentCost
                    };

                    await SupabaseService.Instance
                        .From<HanashGamingCafe.SessionOrderDatabaseModel>()
                        .Insert(transferredOrderItem);
                }

                string cleanOldDeviceName = currentSession.DeviceName?.Trim() ?? "";

                if (!string.IsNullOrEmpty(cleanOldDeviceName))
                {
                    var oldDeviceResponse = await SupabaseService.Instance
                        .From<DeviceModel>()
                        .Where(d => d.Name == cleanOldDeviceName)
                        .Get();

                    var oldDevice = oldDeviceResponse.Models.FirstOrDefault();
                    if (oldDevice != null)
                    {
                        oldDevice.Status = "free";
                        await SupabaseService.Instance
                            .From<DeviceModel>()
                            .Update(oldDevice);
                    }
                }

                currentSession.DeviceName = targetDevice.Name;
                currentSession.DeviceType = targetDevice.Type;
                currentSession.HourlyRate = targetDevice.HourlyRate;
                currentSession.StartTime = DateTime.Now;
                currentSession.RoundsCount = 1;

                await SupabaseService.Instance
                    .From<SessionModel>()
                    .Update(currentSession);

                string targetDeviceId = targetDevice.Id;

                var targetDeviceResponse = await SupabaseService.Instance
                    .From<DeviceModel>()
                    .Where(d => d.Id == targetDeviceId)
                    .Get();

                var freshTargetDevice = targetDeviceResponse.Models.FirstOrDefault() ?? targetDevice;
                freshTargetDevice.Status = "busy";

                await SupabaseService.Instance
                    .From<DeviceModel>()
                    .Update(freshTargetDevice);

                MessageBox.Show($"تم نقل الفاتورة بنجاح وتحويل الحساب القديم إلى القائمة 🚚✨", "تم النقل", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadActiveSessionsAsync();
                await SyncSessionOrdersFromSupabaseAsync(currentSession.Id);
                RecalculateSelectedSessionBill();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء تنفيذ عملية النقل: {ex.Message}\n\n{ex.StackTrace}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region إدارة الشفتات والجرد

        private async void BtnStartShift_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await SupabaseService.InitializeAsync();

                var openShiftResponse = await SupabaseService.Instance
                    .From<ShiftModel>()
                    .Where(s => s.Status == "open")
                    .Get();

                if (openShiftResponse.Models != null && openShiftResponse.Models.Any())
                {
                    var result = MessageBox.Show("يوجد شفت مفتوح حالياً! هل تريد إغلاق الشفتات القديمة المعلقة وفتح شفت جديد؟",
                                                "تنبيه", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        foreach (var shift in openShiftResponse.Models)
                        {
                            shift.Status = "closed";
                            shift.EndTime = DateTime.Now;
                            await SupabaseService.Instance.From<ShiftModel>().Update(shift);
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                string cashierName = Microsoft.VisualBasic.Interaction.InputBox("أدخل اسم الكاشير / المستلم:", "فتح شفت جديد", "");
                if (string.IsNullOrWhiteSpace(cashierName)) return;

                string rawInitialCash = Microsoft.VisualBasic.Interaction.InputBox("أدخل مبلغ الخردة الافتتاحي في الصندوق (إن وجد):", "المبلغ الافتتاحي", "0");
                decimal.TryParse(rawInitialCash, out decimal initialCash);

                var newShift = new ShiftModel
                {
                    CashierName = cashierName,
                    StartTime = DateTime.Now,
                    InitialCash = initialCash,
                    Status = "open"
                };

                await SupabaseService.Instance
                    .From<ShiftModel>()
                    .Insert(newShift);

                MessageBox.Show($"تم فتح شفت جديد بنجاح باسم ({cashierName})! نتمنى لك يوماً سعيداً 🌟", "تم الفتح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء فتح الشفت: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCloseShift_Click(object sender, RoutedEventArgs e)
        {
            var closeShiftWin = new ShiftCloseWindow();
            closeShiftWin.Owner = this;

            if (closeShiftWin.ShowDialog() == true)
            {
                PrintShiftReport();
            }
        }

        private async void PrintShiftReport()
        {
            await InvoicePrinter.PrintShiftReportAsync();
        }
        #endregion

        private void BtnShiftsHistory_Click(object sender, RoutedEventArgs e)
        {
            var shiftsHistoryWin = new ShiftsHistoryWindow();
            shiftsHistoryWin.Owner = this;
            shiftsHistoryWin.ShowDialog();
        }

        private async void GridCurrentSessionOrders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid currentGrid && currentGrid.SelectedItem is SessionOrderItem selectedOrder)
            {
                if (_selectedSession == null) return;

                var result = MessageBox.Show(
                    $"هل أنت تأكد من حذف ({selectedOrder.Name}) من الجلسة الحالية؟",
                    "تأكيد الحذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                try
                {
                    await SupabaseService.Instance
                        .From<SessionOrderDatabaseModel>()
                        .Where(x => x.SessionId == _selectedSession.Id && x.ItemName == selectedOrder.Name)
                        .Delete();

                    await SyncSessionOrdersFromSupabaseAsync(_selectedSession.Id);

                    MessageBox.Show("تم حذف المادة بنجاح!", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"حدث خطأ أثناء الحذف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task GenerateMockOrdersAsync(int count = 5)
        {
            if (_selectedSession == null)
            {
                MessageBox.Show("يرجى اختيار جلسة أولاً لتوليد البيانات عليها!", "تنبيه");
                return;
            }

            string[] sampleItems = { "شاي", "قهوة تركية", "عصير برتقال", "ماء", "أرجيلة تفاحتين", "كبسة دجاج", "شيبس" };
            double[] samplePrices = { 1000, 2000, 3000, 500, 5000, 7000, 1000 };
            Random random = new Random();

            try
            {
                for (int i = 0; i < count; i++)
                {
                    int index = random.Next(sampleItems.Length);
                    string name = sampleItems[index];
                    double price = samplePrices[index];
                    double qty = random.Next(1, 4);

                    var mockOrder = new SessionOrderDatabaseModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        SessionId = _selectedSession.Id,
                        ItemName = name,
                        Qty = qty,
                        Price = price,
                        TotalPrice = qty * price
                    };

                    await SupabaseService.Instance
                        .From<SessionOrderDatabaseModel>()
                        .Insert(mockOrder);
                }

                await SyncSessionOrdersFromSupabaseAsync(_selectedSession.Id);
                MessageBox.Show($"تم إدراج {count} طلبات تجريبية بنجاح!", "نجاح الاختبار");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء توليد البيانات: {ex.Message}");
            }
        }

        private void BtnNavSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SettingsWindow settingsWin = new SettingsWindow();
                settingsWin.Owner = this;
                settingsWin.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء فتح النافذة:\n{ex.Message}\n\nالتفاصيل:\n{ex.StackTrace}",
                                "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}