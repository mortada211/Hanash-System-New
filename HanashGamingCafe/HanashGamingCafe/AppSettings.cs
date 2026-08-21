using System;
using System.IO;
using System.Text.Json;

namespace HanashGamingCafe
{
    public class PrintConfig
    {
        public string HallName { get; set; } = "🎮 حنش غيمينغ كافيه 🎮";
        public string PrinterName { get; set; } = "Generic IBM Graphics 9pin";
    }

    public static class AppSettings
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static PrintConfig Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    var defaultConfig = new PrintConfig();
                    Save(defaultConfig); // إنشاء الملف لأول مرة
                    return defaultConfig;
                }

                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<PrintConfig>(json) ?? new PrintConfig();
            }
            catch
            {
                return new PrintConfig(); // إرجاع قيم افتراضية في حال وجود أي خلل
            }
        }

        public static void Save(PrintConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);

                // استخدام WriteAllText لإنشاء أو استبدال الملف مباشرة دون الحاجة لقراءته
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"خطأ أثناء حفظ الإعدادات: {ex.Message}");
            }
        }
    }
}