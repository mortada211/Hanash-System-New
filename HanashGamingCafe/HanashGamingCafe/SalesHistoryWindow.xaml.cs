using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HanashGamingCafe
{
    public partial class SalesHistoryWindow : Window
    {
        public SalesHistoryWindow()
        {
            InitializeComponent();
            Loaded += SalesHistoryWindow_Loaded;
        }

        private async void SalesHistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // ضبط تاريخ اليوم تلقائياً
            DpStartDate.SelectedDate = DateTime.Today;
            DpEndDate.SelectedDate = DateTime.Today;

            await LoadSalesHistoryAsync();
        }

        // 🟢 1. دالة استخراج الوقت الحقيقي (للمطابقة الدقيقة مع الشفتات)
        private DateTime GetTrueUtc(DateTime dbTime)
        {
            if (dbTime == DateTime.MinValue || dbTime == DateTime.MaxValue) return dbTime;

            DateTime utcTime = dbTime.Kind == DateTimeKind.Unspecified
                               ? DateTime.SpecifyKind(dbTime, DateTimeKind.Utc)
                               : dbTime.ToUniversalTime();

            // إذا كان الوقت المسجل متقدماً (بسبب حفظه محلياً في الماضي) نرجعه لأصله
            if (utcTime > DateTime.UtcNow.AddMinutes(30))
            {
                utcTime = utcTime.AddHours(-3);
            }
            return utcTime;
        }

        // 🟢 2. دالة العرض (لضبط توقيت الجلسات وعرضها بتوقيت العراق في الجدول)
        private DateTime GetDisplayTime(DateTime dbTime)
        {
            if (dbTime == DateTime.MinValue || dbTime == DateTime.MaxValue) return dbTime;

            DateTime trueUtc = GetTrueUtc(dbTime);
            DateTime iraqTime = trueUtc.AddHours(3); // إضافة 3 ساعات لتوقيت بغداد

            // Unspecified تجمد الوقت وتمنع الويندوز أو الجدول من تغييره
            return DateTime.SpecifyKind(iraqTime, DateTimeKind.Unspecified);
        }

        private async Task LoadSalesHistoryAsync()
        {
            try
            {
                await SupabaseService.InitializeAsync();

                // 1. جلب الشفتات لربط كل جلسة باسم الكاشير المسئول عنها
                var shiftsResponse = await SupabaseService.Instance
                    .From<ShiftModel>()
                    .Get();

                var shifts = shiftsResponse.Models ?? new List<ShiftModel>();

                // 2. جلب الجلسات المكتملة فقط
                var sessionsResponse = await SupabaseService.Instance
                    .From<SessionModel>()
                    .Where(s => s.Status == "completed")
                    .Get();

                if (sessionsResponse.Models == null || !sessionsResponse.Models.Any())
                {
                    DgSales.ItemsSource = null;
                    TxtTotalSales.Text = "0 د.ع";
                    return;
                }

                // 🟢 3. مطابقة الكاشير وتصحيح الأوقات
                foreach (var session in sessionsResponse.Models)
                {
                    // استخراج الوقت الحقيقي للجلسة للمطابقة
                    DateTime sessionEndUtc = GetTrueUtc(session.EndTime ?? session.StartTime);

                    // إذا كانت الخاصية غير مسجلة، نبحث عنها في الشفتات بناءً على الوقت الحقيقي
                    if (string.IsNullOrWhiteSpace(session.CashierName))
                    {
                        var matchingShift = shifts.FirstOrDefault(sh =>
                        {
                            DateTime shStartUtc = GetTrueUtc(sh.StartTime);
                            DateTime? shEndUtc = sh.EndTime.HasValue ? GetTrueUtc(sh.EndTime.Value) : (DateTime?)null;

                            // أضفنا دقيقتين سماحية للفروقات البسيطة
                            return sessionEndUtc >= shStartUtc.AddMinutes(-2) &&
                                   (shEndUtc == null || sessionEndUtc <= shEndUtc.Value.AddMinutes(2));
                        });

                        session.CashierName = matchingShift != null ? matchingShift.CashierName : "غير محدد";
                    }

                    // 🔴 الأهم: تجميد الأوقات بتوقيت العراق لعرضها في الجدول بشكل صحيح!
                    session.StartTime = GetDisplayTime(session.StartTime);
                    if (session.EndTime.HasValue)
                    {
                        session.EndTime = GetDisplayTime(session.EndTime.Value);
                    }
                }

                // 🟢 4. تعبئة قائمة الكاشيرية (ComboBox) ديناميكياً لأول مرة
                PopulateCashierComboBox(sessionsResponse.Models);

                // 5. فلترة الجلسات بناءً على التواريخ المحددة واسم الكاشير
                DateTime startDate = (DpStartDate.SelectedDate ?? DateTime.Today).Date;
                DateTime endDate = (DpEndDate.SelectedDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
                string selectedCashier = CmbCashiers?.SelectedItem?.ToString();

                var filteredSessions = sessionsResponse.Models.Where(s =>
                {
                    DateTime sessionTime = s.EndTime ?? s.StartTime;
                    bool matchDate = sessionTime >= startDate && sessionTime <= endDate;

                    // شرط الفلترة حسب الكاشير
                    bool matchCashier = string.IsNullOrEmpty(selectedCashier) ||
                                        selectedCashier == "الكل" ||
                                        s.CashierName == selectedCashier;

                    return matchDate && matchCashier;
                }).ToList();

                // 6. ترتيب وعرض البيانات
                var salesList = filteredSessions.OrderByDescending(s => s.EndTime ?? s.StartTime).ToList();
                DgSales.ItemsSource = null; // تفريغ الجدول أولاً لتحديثه
                DgSales.ItemsSource = salesList;

                // 7. حساب إجمالي المبيعات
                decimal totalSales = salesList.Sum(s => s.TotalAmount);
                TxtTotalSales.Text = $"{totalSales:N0} د.ع";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء تحميل سجل المبيعات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 🟢 دالة ملء القائمة المنسدلة بأسماء الكاشيرية
        private void PopulateCashierComboBox(List<SessionModel> sessions)
        {
            if (CmbCashiers == null) return;

            string currentSelection = CmbCashiers.SelectedItem?.ToString();

            var cashiers = sessions
                .Where(s => !string.IsNullOrWhiteSpace(s.CashierName))
                .Select(s => s.CashierName)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            cashiers.Insert(0, "الكل");

            CmbCashiers.ItemsSource = cashiers;

            if (!string.IsNullOrEmpty(currentSelection) && cashiers.Contains(currentSelection))
            {
                CmbCashiers.SelectedItem = currentSelection;
            }
            else
            {
                CmbCashiers.SelectedIndex = 0;
            }
        }

        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            await LoadSalesHistoryAsync();
        }

        private void BtnViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SessionModel session)
            {
                string cashier = !string.IsNullOrWhiteSpace(session.CashierName) ? session.CashierName : "غير محدد";

                MessageBox.Show(
                    $"تفاصيل الفاتورة رقم #{session.Id}\n\n" +
                    $"الكاشير المسؤول: {cashier}\n" +
                    $"المكان: {session.DeviceName}\n" +
                    $"وقت البدء: {session.StartTime:hh:mm tt}\n" +
                    $"وقت الإغلاق: {session.EndTime:hh:mm tt}\n" +
                    $"المبلغ الكلي: {session.TotalAmount:N0} د.ع",
                    "تفاصيل الفاتورة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}