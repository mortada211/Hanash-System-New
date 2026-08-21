using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HanashGamingCafe
{
    public partial class TransferWindow : Window
    {
        // الجهاز الذي سيتم اختياره من القائمة
        public DeviceModel SelectedDevice { get; private set; }

        public TransferWindow(List<DeviceModel> availableDevices)
        {
            InitializeComponent();
            ItemsAvailableDevices.ItemsSource = availableDevices;
        }

        private void DeviceCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // الحصول على العنصر المزدوج الذي تم الضغط عليه وضمان تعيين القيمة بشكل آمن
            if (sender is FrameworkElement element && element.DataContext is DeviceModel device)
            {
                SelectedDevice = device;
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}