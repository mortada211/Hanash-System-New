using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static Postgrest.Constants;

namespace HanashGamingCafe
{
    public partial class ShiftCloseWindow : Window
    {
        private ShiftModel _currentShift;

        public ShiftCloseWindow()
        {
            InitializeComponent();
            Loaded += ShiftCloseWindow_Loaded;
        }

        // 🟢 1. دالة استخراج الوقت الحقيقي (تصلح أخطاء الأوقات المحفوظة مستقبلاً)
        private DateTime GetTrueUtc(DateTime dbTime)
        {
            if (dbTime == DateTime.MinValue || dbTime == DateTime.MaxValue) return dbTime;

            DateTime utcTime = dbTime.Kind == DateTimeKind.Unspecified
                               ? DateTime.SpecifyKind(dbTime, DateTimeKind.Utc)
                               : dbTime.ToUniversalTime();

            // إذا كان الوقت المسجل متقدماً على الوقت الحالي (بسبب حفظه محلياً في الماضي)
            // نقوم بإرجاعه 3 ساعات للخلف ليعود لواقعه الحقيقي.
            if (utcTime > DateTime.UtcNow.AddMinutes(30))
            {
                utcTime = utcTime.AddHours(-3);
            }
            return utcTime;
        }

        private async void ShiftCloseWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCurrentShiftDataAsync();
        }

        private async Task LoadCurrentShiftDataAsync()
        {
            try
            {
                await SupabaseService.InitializeAsync();

                var response = await SupabaseService.Instance
                    .From<ShiftModel>()
                    .Where(s => s.Status == "open")
                    .Order(s => s.StartTime, Ordering.Descending)
                    .Get();

                _currentShift = response.Models.FirstOrDefault();

                if (_currentShift == null)
                {
                    MessageBox.Show("لا يوجد شفت مفتوح حالياً لإغلاقه!", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Close();
                    return;
                }

                if (TxtCashierName != null) TxtCashierName.Text = _currentShift.CashierName;

                var sessionsResponse = await SupabaseService.Instance
                    .From<SessionModel>()
                    .Where(s => s.Status == "completed")
                    .Get();

                decimal totalSales = 0;

                if (sessionsResponse.Models != null && sessionsResponse.Models.Any())
                {
                    // 🔴 استخدام الوقت الحقيقي والمصحح للشفت
                    DateTime shiftStartUtc = GetTrueUtc(_currentShift.StartTime);

                    var shiftSessions = sessionsResponse.Models.Where(s =>
                    {
                        DateTime sessionTime = s.EndTime ?? s.StartTime;
                        // 🔴 استخدام الوقت الحقيقي والمصحح للجلسة
                        DateTime sessionUtc = GetTrueUtc(sessionTime);

                        // الحساب أصبح عادلاً: هل تمت الجلسة بعد فتح الشفت؟
                        return sessionUtc >= shiftStartUtc.AddMinutes(-2);
                    }).ToList();

                    totalSales = shiftSessions.Sum(s => s.TotalAmount);
                }

                _currentShift.TotalSales = totalSales;
                _currentShift.ExpectedCash = _currentShift.InitialCash + _currentShift.TotalSales - _currentShift.TotalExpenses;

                if (TxtInitialCash != null) TxtInitialCash.Text = $"{_currentShift.InitialCash:N0} د.ع";
                if (TxtTotalSales != null) TxtTotalSales.Text = $"{_currentShift.TotalSales:N0} د.ع";
                if (TxtExpectedCash != null) TxtExpectedCash.Text = $"{_currentShift.ExpectedCash:N0} د.ع";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtActualCash_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_currentShift == null) return;
            if (decimal.TryParse(TxtActualCash.Text, out decimal actualCash))
            {
                decimal difference = actualCash - _currentShift.ExpectedCash;
                if (TxtDifference != null)
                {
                    if (difference < 0) TxtDifference.Text = $"{difference:N0} د.ع (عجز 🔴)";
                    else if (difference > 0) TxtDifference.Text = $"+{difference:N0} د.ع (زيادة 🔵)";
                    else TxtDifference.Text = "0 د.ع (مطابق 🟢)";
                }
            }
            else { if (TxtDifference != null) TxtDifference.Text = "0 د.ع"; }
        }

        private async void BtnConfirmCloseShift_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentShift == null) return;
                if (!decimal.TryParse(TxtActualCash.Text, out decimal actualCash))
                {
                    MessageBox.Show("يرجى إدخال المبلغ الفعلي بصورة صحيحة!", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _currentShift.ActualCash = actualCash;
                _currentShift.Difference = _currentShift.ActualCash - _currentShift.ExpectedCash;

                // الحفظ يتم كـ UTC حقيقي لتجنب تكرار المشكلة مستقبلاً
                _currentShift.EndTime = DateTime.UtcNow;
                _currentShift.Status = "closed";

                await SupabaseService.Instance.From<ShiftModel>().Update(_currentShift);

                MessageBox.Show("تم إغلاق الشفت بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}