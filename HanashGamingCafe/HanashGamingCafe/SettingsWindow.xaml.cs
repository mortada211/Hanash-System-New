using System.Drawing.Printing;
using System.Windows;

namespace HanashGamingCafe
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            // جلب الإعدادات الحالية
            var config = AppSettings.Load();
            TxtHallName.Text = config.HallName;

            // جلب جميع الطابعات المعرفة على جهاز الـ PC
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                CmbPrinters.Items.Add(printer);
            }

            // تحديد الطابعة المحفوظة حالياً
            if (CmbPrinters.Items.Contains(config.PrinterName))
            {
                CmbPrinters.SelectedItem = config.PrinterName;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var config = new PrintConfig
            {
                HallName = TxtHallName.Text,
                PrinterName = CmbPrinters.SelectedItem?.ToString() ?? "Generic IBM Graphics 9pin"
            };

            AppSettings.Save(config);
            MessageBox.Show("تم حفظ الإعدادات بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }
}