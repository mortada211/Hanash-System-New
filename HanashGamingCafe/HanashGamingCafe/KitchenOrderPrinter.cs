using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows;
using GdiFont = System.Drawing.Font;
using GdiFontStyle = System.Drawing.FontStyle;

namespace HanashGamingCafe
{
    public class KitchenOrderPrinter
    {
        private const int PrintWidthDots = 576; // 72mm للطابعة الخاصة بك

        public static void PrintKitchenOrder(
            string tableName,      // اسم الجلسة أو الطاولة (مثال: طاولة بلياردو 2)
            string itemName,       // اسم المادة (مثال: أرجيلة تفاحتين / بيبسي)
            int quantity = 1,       // الكمية
            string printerName = "Generic IBM Graphics 9pin")
        {
            try
            {
                byte[] escposData = RenderOrderToEscPos(tableName, itemName, quantity, PrintWidthDots);
                bool printed = InvoicePrinter.SendBytesToPrinter(printerName, escposData);

                if (!printed)
                {
                    MessageBox.Show($"لم يتم إرسال طلب المطبخ إلى الطابعة '{printerName}'.", "خطأ طباعة", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء طباعة طلب المطبخ:\n{ex.Message}", "خطأ طباعة", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static byte[] RenderOrderToEscPos(string tableName, string itemName, int quantity, int widthDots)
        {
            int totalHeight = 320; // ارتفاع الورقة الافتراضي للأوردر السريع

            byte[] imageBytes;

            using (Bitmap bmp = new Bitmap(widthDots, totalHeight))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                g.FillRectangle(Brushes.White, 0, 0, widthDots, totalHeight);

                float y = 15;

                using (StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center })
                using (StringFormat sfRight = new StringFormat(StringFormatFlags.DirectionRightToLeft) { Alignment = StringAlignment.Far })
                {
                    // 1. اسم الصالة
                    using (GdiFont fontHeader = new GdiFont("Segoe UI", 22f, GdiFontStyle.Bold))
                    {
                        g.DrawString("🎮 قاعة حنش الشهيرة 🎮", fontHeader, Brushes.Black, new RectangleF(0, y, widthDots, 40), sfCenter);
                        y += 45;
                    }

                    // 2. نوع الورقة (أوردر طلب)
                    using (GdiFont fontSub = new GdiFont("Segoe UI", 16f, GdiFontStyle.Bold))
                    {
                        g.DrawString("⚡ أمر تجهيز طلب جديد ⚡", fontSub, Brushes.Black, new RectangleF(0, y, widthDots, 30), sfCenter);
                        y += 35;
                    }

                    // خط فاصل
                    using (Pen pen = new Pen(Color.Black, 3) { DashStyle = DashStyle.Dash })
                    {
                        g.DrawLine(pen, 10, y, widthDots - 10, y);
                    }
                    y += 15;

                    // 3. اسم الجلسة / الطاولة (بخط كبير جداً وواضح)
                    using (GdiFont fontTable = new GdiFont("Segoe UI", 24f, GdiFontStyle.Bold))
                    {
                        g.DrawString($"الجلسة: {tableName}", fontTable, Brushes.Black, new RectangleF(0, y, widthDots, 50), sfCenter);
                        y += 55;
                    }

                    // خط فاصل رفيع
                    using (Pen pen = new Pen(Color.Black, 1))
                    {
                        g.DrawLine(pen, 20, y, widthDots - 20, y);
                    }
                    y += 15;

                    // 4. اسم المادة والكمية (كبيرة جداً ليسهل قراءتها في المطبخ/البار)
                    using (GdiFont fontItem = new GdiFont("Segoe UI", 22f, GdiFontStyle.Bold))
                    {
                        g.DrawString($"الطلب: {itemName}", fontItem, Brushes.Black, new RectangleF(10, y, widthDots - 20, 45), sfRight);
                        y += 50;
                    }

                    using (GdiFont fontQty = new GdiFont("Segoe UI", 20f, GdiFontStyle.Bold))
                    {
                        g.DrawString($"الكمية:  {quantity}×", fontQty, Brushes.Black, new RectangleF(10, y, widthDots - 20, 40), sfRight);
                        y += 45;
                    }

                    // خط فاصل
                    using (Pen pen = new Pen(Color.Black, 2) { DashStyle = DashStyle.Dash })
                    {
                        g.DrawLine(pen, 10, y, widthDots - 10, y);
                    }
                    y += 15;

                    // 5. الوقت والتاريخ
                    using (GdiFont fontTime = new GdiFont("Segoe UI", 13f, GdiFontStyle.Regular))
                    {
                        g.DrawString($"الوقت: {DateTime.Now:yyyy-MM-dd  hh:mm tt}", fontTime, Brushes.Black, new RectangleF(10, y, widthDots - 20, 30), sfRight);
                    }
                }

                imageBytes = ConvertBitmapToEscPosRaster(bmp, widthDots, totalHeight);
            }

            List<byte> output = new List<byte>();
            output.AddRange(new byte[] { 0x1B, 0x40 }); // Reset Printer
            output.AddRange(imageBytes);
            output.Add(0x0A);
            output.Add(0x0A);
            output.Add(0x0A);
            output.AddRange(new byte[] { 0x1D, 0x56, 0x00 }); // Cut paper

            return output.ToArray();
        }

        private static byte[] ConvertBitmapToEscPosRaster(Bitmap bmp, int width, int height)
        {
            int widthBytes = (width + 7) / 8;
            List<byte> data = new List<byte>();

            data.Add(0x1D); data.Add(0x76); data.Add(0x30); data.Add(0x00);
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
    }
}