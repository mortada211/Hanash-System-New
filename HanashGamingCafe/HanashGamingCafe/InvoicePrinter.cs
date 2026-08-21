using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows;
using GdiFont = System.Drawing.Font;
using GdiFontStyle = System.Drawing.FontStyle;

namespace HanashGamingCafe
{
    public class InvoicePrinter
    {
        // ============ RAW Printing (WinSpool) ============
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In] ref DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        public static bool SendBytesToPrinter(string printerName, byte[] bytes)
        {
            IntPtr hPrinter = IntPtr.Zero;
            DOCINFOA di = new DOCINFOA
            {
                pDocName = "Receipt Order",
                pOutputFile = null,
                pDataType = "RAW"
            };

            bool success = false;
            IntPtr pBytes = Marshal.AllocHGlobal(bytes.Length);

            try
            {
                Marshal.Copy(bytes, 0, pBytes, bytes.Length);

                if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
                    throw new Exception($"تعذر فتح الطابعة '{printerName}'. تأكد من الاسم ومن أن الطابعة متصلة.");

                if (!StartDocPrinter(hPrinter, 1, ref di))
                    throw new Exception("تعذر بدء مهمة الطباعة (StartDocPrinter).");

                if (!StartPagePrinter(hPrinter))
                    throw new Exception("تعذر بدء الصفحة (StartPagePrinter).");

                success = WritePrinter(hPrinter, pBytes, bytes.Length, out int written);
                if (!success)
                    throw new Exception("فشل إرسال البيانات إلى الطابعة (WritePrinter).");

                EndPagePrinter(hPrinter);
                EndDocPrinter(hPrinter);
            }
            finally
            {
                Marshal.FreeHGlobal(pBytes);
                if (hPrinter != IntPtr.Zero) ClosePrinter(hPrinter);
            }

            return success;
        }

        // ============ طباعة الفاتورة كصورة (ESC/POS) ============
        private const int PrintWidthDots = 576;

        public static void PrintReceipt(
            string cashierName,
            string deviceName,
            string deviceType,
            TimeSpan durationOrRounds,
            decimal sessionCost,
            List<MainWindow.SessionOrderItem> items,
            decimal grandTotal,
            string printerName = null)
        {
            string dt = deviceType ?? string.Empty;
            var config = AppSettings.Load();
            string targetPrinter = string.IsNullOrEmpty(printerName) ? config.PrinterName : printerName;

            try
            {
                var lines = BuildLines(config.HallName, cashierName, deviceName, dt, durationOrRounds, sessionCost, items, grandTotal);
                byte[] escposData = RenderLinesToEscPos(lines, PrintWidthDots);

                bool printed = SendBytesToPrinter(targetPrinter, escposData);

                if (!printed)
                {
                    MessageBox.Show($"لم يتم إرسال الفاتورة إلى الطابعة '{targetPrinter}'.", "خطأ طباعة", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء الطباعة على '{targetPrinter}':\n{ex.Message}", "خطأ طباعة", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class ReceiptLine
        {
            public string Text { get; set; }
            public bool Bold { get; set; }
            public float Size { get; set; }
            public StringAlignment Alignment { get; set; }
            public bool IsDivider { get; set; }

            public ReceiptLine(string text, bool bold = false, float size = 14f, StringAlignment alignment = StringAlignment.Center, bool isDivider = false)
            {
                Text = text;
                Bold = bold;
                Size = size;
                Alignment = alignment;
                IsDivider = isDivider;
            }
        }

        private static List<ReceiptLine> BuildLines(
            string hallName,
            string cashierName,
            string deviceName,
            string dt,
            TimeSpan durationOrRounds,
            decimal sessionCost,
            List<MainWindow.SessionOrderItem> items,
            decimal grandTotal)
        {
            var lines = new List<ReceiptLine>();

            // الهيدر الرئيسي - ديناميكي حسب اسم القاعة
            string headerTitle = string.IsNullOrWhiteSpace(hallName) ? "🎮 حنش غيمينغ كافيه 🎮" : $"🎮 {hallName} 🎮";
            lines.Add(new ReceiptLine(headerTitle, true, 24f));
            lines.Add(new ReceiptLine("فاتورة حساب نهائية", false, 18f));
            lines.Add(new ReceiptLine("", false, 6f));
            lines.Add(new ReceiptLine("", false, 0f, StringAlignment.Center, true));

            // معلومات الفاتورة بتوقيت العراق المحلي
            DateTime localNow = DateTime.UtcNow.AddHours(3);
            lines.Add(new ReceiptLine($"التاريخ: {localNow:yyyy-MM-dd  hh:mm tt}", false, 15f, StringAlignment.Far));
            lines.Add(new ReceiptLine($"الكاشير: {cashierName}", false, 15f, StringAlignment.Far));
            lines.Add(new ReceiptLine("", false, 0f, StringAlignment.Center, true));

            // تفاصيل المكان والوقت
            lines.Add(new ReceiptLine($"{dt} ({deviceName})", true, 22f));

            bool isRoundBased = !string.IsNullOrEmpty(dt) &&
                (dt.Contains("bi", StringComparison.OrdinalIgnoreCase) ||
                 dt.Contains("billiard", StringComparison.OrdinalIgnoreCase) ||
                 dt.Contains("gm", StringComparison.OrdinalIgnoreCase));

            if (isRoundBased)
                lines.Add(new ReceiptLine($"الجولات: {(int)durationOrRounds.TotalDays}", true, 16f, StringAlignment.Far));
            else
                lines.Add(new ReceiptLine($"الوقت: {(int)durationOrRounds.TotalHours:D2}:{durationOrRounds.Minutes:D2}:{durationOrRounds.Seconds:D2}", true, 16f, StringAlignment.Far));

            lines.Add(new ReceiptLine($"كلفة الوقت: {sessionCost:N0} د.ع", false, 16f, StringAlignment.Far));

            // قائمة الطلبات
            if (items != null && items.Count > 0)
            {
                lines.Add(new ReceiptLine("", false, 0f, StringAlignment.Center, true));
                lines.Add(new ReceiptLine("🛒 الطلبات والمشروبات", true, 18f));
                lines.Add(new ReceiptLine("", false, 4f));

                foreach (var item in items)
                {
                    lines.Add(new ReceiptLine($"{item.Name} ×{item.Qty} = {item.Total:N0} د.ع", false, 15f, StringAlignment.Far));
                }
            }

            lines.Add(new ReceiptLine("", false, 0f, StringAlignment.Center, true));

            // المجموع الكلي
            lines.Add(new ReceiptLine("", false, 6f));
            lines.Add(new ReceiptLine($"المجموع: {grandTotal:N0} د.ع", true, 26f));
            lines.Add(new ReceiptLine("", false, 6f));

            lines.Add(new ReceiptLine("", false, 0f, StringAlignment.Center, true));
            lines.Add(new ReceiptLine("✨ أهلاً وسهلاً بكم ✨", false, 16f));

            return lines;
        }

        private static byte[] RenderLinesToEscPos(List<ReceiptLine> lines, int widthDots)
        {
            int totalHeight = 20;

            using (Bitmap dummyBmp = new Bitmap(widthDots, 100))
            using (Graphics g = Graphics.FromImage(dummyBmp))
            {
                foreach (var line in lines)
                {
                    if (line.IsDivider)
                    {
                        totalHeight += 12;
                    }
                    else if (string.IsNullOrEmpty(line.Text))
                    {
                        totalHeight += (int)line.Size;
                    }
                    else
                    {
                        using (GdiFont font = new GdiFont("Segoe UI", line.Size, line.Bold ? GdiFontStyle.Bold : GdiFontStyle.Regular, GraphicsUnit.Point))
                        {
                            SizeF size = g.MeasureString(line.Text, font, widthDots);
                            totalHeight += (int)Math.Ceiling(size.Height) + 4;
                        }
                    }
                }
            }

            totalHeight += 20;

            byte[] imageBytes;

            using (Bitmap bmp = new Bitmap(widthDots, totalHeight))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                g.FillRectangle(Brushes.White, 0, 0, widthDots, totalHeight);

                float currentY = 10;

                foreach (var line in lines)
                {
                    if (line.IsDivider)
                    {
                        using (Pen pen = new Pen(Color.Black, 2))
                        {
                            pen.DashStyle = DashStyle.Dash;
                            g.DrawLine(pen, 10, currentY + 6, widthDots - 10, currentY + 6);
                        }
                        currentY += 12;
                    }
                    else if (string.IsNullOrEmpty(line.Text))
                    {
                        currentY += line.Size;
                    }
                    else
                    {
                        using (GdiFont font = new GdiFont("Segoe UI", line.Size, line.Bold ? GdiFontStyle.Bold : GdiFontStyle.Regular, GraphicsUnit.Point))
                        using (StringFormat sf = new StringFormat(StringFormatFlags.DirectionRightToLeft))
                        {
                            sf.Alignment = line.Alignment;
                            SizeF size = g.MeasureString(line.Text, font, widthDots, sf);
                            RectangleF rect = new RectangleF(0, currentY, widthDots, size.Height);

                            g.DrawString(line.Text, font, Brushes.Black, rect, sf);
                            currentY += size.Height + 4;
                        }
                    }
                }

                imageBytes = ConvertBitmapToEscPosRaster(bmp, widthDots, totalHeight);
            }

            List<byte> output = new List<byte>();
            output.AddRange(new byte[] { 0x1B, 0x40 }); // ESC @
            output.AddRange(imageBytes);
            output.Add(0x0A);
            output.Add(0x0A);
            output.Add(0x0A);
            output.AddRange(new byte[] { 0x1D, 0x56, 0x00 }); // GS V 0

            return output.ToArray();
        }

        private static byte[] ConvertBitmapToEscPosRaster(Bitmap bmp, int width, int height)
        {
            int widthBytes = (width + 7) / 8;
            List<byte> data = new List<byte>();

            data.Add(0x1D);
            data.Add(0x76);
            data.Add(0x30);
            data.Add(0x00);
            data.Add((byte)(widthBytes % 256));
            data.Add((byte)(widthBytes / 256));
            data.Add((byte)(height % 256));
            data.Add((byte)(height / 256));

            for (int y = 0; y < height; y++)
            {
                for (int xByte = 0; xByte < widthBytes; xByte++)
                {
                    byte b = 0;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        int x = xByte * 8 + bit;
                        bool isBlack = false;
                        if (x < width)
                        {
                            Color pixel = bmp.GetPixel(x, y);
                            int luminance = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                            isBlack = luminance < 180;
                        }
                        if (isBlack)
                            b |= (byte)(1 << (7 - bit));
                    }
                    data.Add(b);
                }
            }

            return data.ToArray();
        }

        // ============ طباعة تقرير الشفت كصورة (ESC/POS) ============
        public static async System.Threading.Tasks.Task PrintShiftReportAsync(string printerName = null)
        {
            var config = AppSettings.Load();
            string targetPrinter = string.IsNullOrEmpty(printerName) ? config.PrinterName : printerName;

            try
            {
                var response = await SupabaseService.Instance
                    .From<ShiftModel>()
                    .Order("id", Postgrest.Constants.Ordering.Descending)
                    .Limit(1)
                    .Get();

                var currentShift = response.Models.FirstOrDefault();

                if (currentShift == null)
                {
                    MessageBox.Show("لم يتم العثور على بيانات أي شفت في النظام للطباعة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var lines = BuildShiftReportLines(config.HallName, currentShift);
                byte[] escposData = RenderLinesToEscPos(lines, PrintWidthDots);

                bool printed = SendBytesToPrinter(targetPrinter, escposData);

                if (!printed)
                {
                    MessageBox.Show($"لم يتم إرسال تقرير الشفت إلى الطابعة '{targetPrinter}'.", "خطأ طباعة", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء طباعة تقرير الشفت على '{targetPrinter}':\n{ex.Message}", "خطأ طباعة", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static List<ReceiptLine> BuildShiftReportLines(string hallName, ShiftModel currentShift)
        {
            var lines = new List<ReceiptLine>();

            // الهيدر الرئيسي
            string title = string.IsNullOrWhiteSpace(hallName) ? "🎮 صالة الألعاب 🎮" : $"🎮 {hallName} 🎮";
            lines.Add(new ReceiptLine(title, true, 24f));
            lines.Add(new ReceiptLine("تقرير إغلاق الشفت", true, 18f));
            lines.Add(new ReceiptLine("", false, 4f));
            lines.Add(new ReceiptLine("", false, 0f, StringAlignment.Center, true));

            // تحويل التوقيت القادم من قاعدة البيانات (UTC) إلى التوقيت المحلي للعراق (+3 hours)
            DateTime localStart = currentShift.StartTime.Kind == DateTimeKind.Utc
                ? currentShift.StartTime.AddHours(3)
                : currentShift.StartTime;

            DateTime? localEnd = currentShift.EndTime.HasValue
                ? (currentShift.EndTime.Value.Kind == DateTimeKind.Utc ? currentShift.EndTime.Value.AddHours(3) : currentShift.EndTime.Value)
                : DateTime.UtcNow.AddHours(3);

            // تفاصيل الشفت
            lines.Add(new ReceiptLine($"رقم الشفت: #{currentShift.Id}", false, 15f, StringAlignment.Far));
            lines.Add(new ReceiptLine($"الكاشير: {currentShift.CashierName}", false, 15f, StringAlignment.Far));
            lines.Add(new ReceiptLine($"تاريخ البداية: {localStart:yyyy-MM-dd hh:mm tt}", false, 14f, StringAlignment.Far));
            lines.Add(new ReceiptLine($"تاريخ الإغلاق: {localEnd:yyyy-MM-dd hh:mm tt}", false, 14f, StringAlignment.Far));
            lines.Add(new ReceiptLine("", false, 0f, StringAlignment.Center, true));

            // الحسابات المالية
            lines.Add(new ReceiptLine("💰 الحسابات المالية للشفت", true, 18f));
            lines.Add(new ReceiptLine("", false, 4f));

            lines.Add(new ReceiptLine($"المبلغ الافتتاحي: {currentShift.InitialCash:N0} د.ع", false, 15f, StringAlignment.Far));
            lines.Add(new ReceiptLine($"إجمالي المبيعات: {currentShift.TotalSales:N0} د.ع", false, 15f, StringAlignment.Far));
            lines.Add(new ReceiptLine($"إجمالي المصاريف: {currentShift.TotalExpenses:N0} د.ع", false, 15f, StringAlignment.Far));
            lines.Add(new ReceiptLine("", false, 0f, StringAlignment.Center, true));

            // المبالغ الصافية والفارق
            lines.Add(new ReceiptLine($"المتوقع بالصندوق: {currentShift.ExpectedCash:N0} د.ع", true, 16f, StringAlignment.Far));
            lines.Add(new ReceiptLine($"الفعلي المستلم: {currentShift.ActualCash:N0} د.ع", true, 16f, StringAlignment.Far));

            if (currentShift.Difference != 0)
            {
                string diffType = currentShift.Difference < 0 ? "عجز" : "زيادة";
                lines.Add(new ReceiptLine($"الفارق ({diffType}): {Math.Abs(currentShift.Difference):N0} د.ع", true, 16f, StringAlignment.Far));
            }

            if (!string.IsNullOrEmpty(currentShift.Notes))
            {
                lines.Add(new ReceiptLine("", false, 0f, StringAlignment.Center, true));
                lines.Add(new ReceiptLine($"ملاحظات: {currentShift.Notes}", false, 14f, StringAlignment.Far));
            }

            lines.Add(new ReceiptLine("", false, 0f, StringAlignment.Center, true));
            lines.Add(new ReceiptLine("✨ تم إغلاق الشفت وتصفية الصندوق ✨", false, 15f));

            return lines;
        }
    }
}