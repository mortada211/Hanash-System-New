using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HanashGamingCafe
{
    public static class PrintManager
    {
        // ⚙️ اسم الطابعة المحدد في جهازك
        public static string PrinterName = "Generic / Text Only)";

        public static void PrintReceipt(
            string cashierName,
            string deviceName,
            string deviceType,
            TimeSpan durationOrRounds,
            decimal sessionCost,
            List<MainWindow.SessionOrderItem> items,
            decimal grandTotal)
        {
            StringBuilder sb = new StringBuilder();

            // إرسال أمر تهيئة الطابعة ESC/POS (تصفير الطابعة)
            sb.Append((char)27).Append('@');

            sb.AppendLine("    حنش غيمينغ كافيه 🎮    ");
            sb.AppendLine("     فاتورة حساب نهائية     ");
            sb.AppendLine($"التاريخ: {DateTime.Now:yyyy-MM-dd hh:mm tt}");
            sb.AppendLine($"الكاشير: {cashierName}");
            sb.AppendLine(new string('-', 30));
            sb.AppendLine($"المكان: {deviceType} ({deviceName})");
            sb.AppendLine($"الوقت: {(int)durationOrRounds.TotalHours:D2}:{durationOrRounds.Minutes:D2}:{durationOrRounds.Seconds:D2}");
            sb.AppendLine($"كلفة الوقت: {sessionCost:N0} د.ع");
            sb.AppendLine(new string('-', 30));

            if (items != null && items.Count > 0)
            {
                sb.AppendLine("الطلبات والمشروبات:");
                foreach (var item in items)
                {
                    sb.AppendLine($"{item.Name} x{item.Qty} = {item.Total:N0} د.ع");
                }
                sb.AppendLine(new string('-', 30));
            }

            sb.AppendLine($"المجموع الكلي: {grandTotal:N0} د.ع");
            sb.AppendLine("       أهلاً وسهلاً بكم ✨      ");
            sb.AppendLine("\n\n\n"); // دفع الورقة للأعلى

            // أمر قطع الورقة (Paper Cut) لـ ESC/POS
            sb.Append((char)29).Append('V').Append((char)66).Append((char)0);

            RawPrinterHelper.SendStringToPrinter(PrinterName, sb.ToString());
        }

        public static void PrintBaristaOrder(string deviceName, string itemName, int qty, string notes = "")
        {
            StringBuilder sb = new StringBuilder();

            sb.Append((char)27).Append('@'); // Initialize

            sb.AppendLine("  ☕ طلب بارستا جديد ☕  ");
            sb.AppendLine($"الوقت: {DateTime.Now:hh:mm tt}");
            sb.AppendLine(new string('=', 26));
            sb.AppendLine($"الطاولة/الجهاز: {deviceName}");
            sb.AppendLine($"الطلب: {itemName} (العدد: {qty})");

            if (!string.IsNullOrEmpty(notes))
            {
                sb.AppendLine($"ملاحظات: {notes}");
            }

            sb.AppendLine("\n\n\n");
            sb.Append((char)29).Append('V').Append((char)66).Append((char)0); // Cut

            RawPrinterHelper.SendStringToPrinter(PrinterName, sb.ToString());
        }
    }

    // 🛠️ الكلاس المسؤول عن إرسال البيانات الخام للطابعة مباشرة عبر Win32 API
    public static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, Int32 level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, Int32 dwCount, out Int32 dwWritten);

        public static bool SendStringToPrinter(string szPrinterName, string szString)
        {
            IntPtr pBytes;
            Int32 dwCount;
            // ترميز UTF-8 أو OEM 864 لدعم اللغة العربية للطابعات الحرارية
            byte[] bytes = Encoding.GetEncoding(1256).GetBytes(szString);
            dwCount = bytes.Length;
            pBytes = Marshal.AllocCoTaskMem(dwCount);
            Marshal.Copy(bytes, 0, pBytes, dwCount);

            bool bSuccess = SendBytesToPrinter(szPrinterName, pBytes, dwCount);
            Marshal.FreeCoTaskMem(pBytes);
            return bSuccess;
        }

        public static bool SendBytesToPrinter(string szPrinterName, IntPtr pBytes, Int32 dwCount)
        {
            Int32 dwError = 0, dwWritten = 0;
            IntPtr hPrinter = new IntPtr(0);
            DOCINFOA di = new DOCINFOA();
            bool bSuccess = false;

            di.pDocName = "POS Receipt";
            di.pDataType = "RAW";

            if (OpenPrinter(szPrinterName.Trim(), out hPrinter, IntPtr.Zero))
            {
                if (StartDocPrinter(hPrinter, 1, di))
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        bSuccess = WritePrinter(hPrinter, pBytes, dwCount, out dwWritten);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }
            return bSuccess;
        }
    }
}