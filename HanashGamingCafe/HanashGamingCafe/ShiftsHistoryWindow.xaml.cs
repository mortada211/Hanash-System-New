using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;

namespace HanashGamingCafe
{
    public partial class ShiftsHistoryWindow : Window
    {
        public ShiftsHistoryWindow()
        {
            InitializeComponent();
            Loaded += ShiftsHistoryWindow_Loaded;
        }

        private async void ShiftsHistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DpStartDate.SelectedDate = DateTime.Today.AddDays(-7);
            DpEndDate.SelectedDate = DateTime.Today;

            await LoadShiftsHistoryAsync();
        }

        // 🟢 1. دالة استخراج الوقت الحقيقي للمقارنة (تمنع ذهاب المبيعات للكاشير السابق)
        private DateTime GetTrueUtc(DateTime dbTime)
        {
            if (dbTime == DateTime.MinValue || dbTime == DateTime.MaxValue) return dbTime;

            DateTime utcTime = dbTime.Kind == DateTimeKind.Unspecified
                               ? DateTime.SpecifyKind(dbTime, DateTimeKind.Utc)
                               : dbTime.ToUniversalTime();

            if (utcTime > DateTime.UtcNow.AddMinutes(30))
            {
                utcTime = utcTime.AddHours(-3);
            }
            return utcTime;
        }

        // 🟢 2. دالة العرض (تجمد الوقت كـ Unspecified لمنع الويندوز من التلاعب به)
        private DateTime GetDisplayTime(DateTime dbTime)
        {
            if (dbTime == DateTime.MinValue || dbTime == DateTime.MaxValue) return dbTime;

            DateTime trueUtc = GetTrueUtc(dbTime);
            DateTime iraqTime = trueUtc.AddHours(3);

            // السر هنا: Unspecified تجبر الجدول على عرض الوقت كما هو دون زيادته أو نقصانه
            return DateTime.SpecifyKind(iraqTime, DateTimeKind.Unspecified);
        }

        private async Task LoadShiftsHistoryAsync()
        {
            try
            {
                await SupabaseService.InitializeAsync();

                DateTime startLocal = DpStartDate.SelectedDate ?? DateTime.Today.AddDays(-7);
                DateTime endLocal = DpEndDate.SelectedDate ?? DateTime.Today;

                DateTime searchStart = startLocal.AddDays(-2);
                DateTime searchEnd = endLocal.AddDays(2);

                var response = await SupabaseService.Instance
                    .From<ShiftModel>()
                    .Where(s => s.StartTime >= searchStart && s.StartTime <= searchEnd)
                    .Get();

                var shiftsList = response.Models.OrderByDescending(s => s.StartTime).ToList();

                var sessionsResponse = await SupabaseService.Instance
                    .From<SessionModel>()
                    .Where(s => s.Status == "completed")
                    .Get();

                var allSessions = sessionsResponse.Models ?? new List<SessionModel>();

                foreach (var shift in shiftsList)
                {
                    // 🔴 الحساب يتم باستخدام التوقيت العالمي الحقيقي لضمان العدالة 100%
                    DateTime shiftStartUtc = GetTrueUtc(shift.StartTime);
                    DateTime shiftEndUtc = shift.EndTime.HasValue ? GetTrueUtc(shift.EndTime.Value) : DateTime.UtcNow;

                    decimal calculatedTotal = (decimal)allSessions
                        .Where(s => {
                            if (!s.EndTime.HasValue) return false;
                            DateTime sessionUtc = GetTrueUtc(s.EndTime.Value);
                            return sessionUtc >= shiftStartUtc.AddMinutes(-2) && sessionUtc <= shiftEndUtc.AddMinutes(2);
                        })
                        .Sum(s => s.TotalAmount);

                    if (shift.TotalSales == 0 || calculatedTotal > 0 || shift.Status == "open")
                    {
                        shift.TotalSales = calculatedTotal;
                        shift.ExpectedCash = shift.InitialCash + shift.TotalSales - shift.TotalExpenses;
                        if (shift.Status == "open") shift.Difference = 0;
                        else shift.Difference = shift.ActualCash - shift.ExpectedCash;
                    }

                    // 🔴 العرض للواجهة: تجميد الوقت لكي يظهر بتوقيت العراق المضبوط
                    shift.StartTime = GetDisplayTime(shift.StartTime);
                    if (shift.EndTime.HasValue)
                        shift.EndTime = GetDisplayTime(shift.EndTime.Value);
                }

                var finalDisplayList = shiftsList.Where(s =>
                    s.StartTime.Date >= startLocal.Date &&
                    s.StartTime.Date <= endLocal.Date).ToList();

                DgShifts.ItemsSource = null;
                DgShifts.ItemsSource = finalDisplayList;

                decimal totalDiff = finalDisplayList.Where(s => s.Status == "closed").Sum(s => s.Difference);
                if (totalDiff < 0) TxtTotalDifference.Text = $"{totalDiff:N0} د.ع (عجز 🔴)";
                else if (totalDiff > 0) TxtTotalDifference.Text = $"+{totalDiff:N0} د.ع (زيادة 🔵)";
                else TxtTotalDifference.Text = "0 د.ع (مطابق 🟢)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            await LoadShiftsHistoryAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}