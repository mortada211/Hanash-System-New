using System;
using System.Windows;

namespace HanashGamingCafe
{
    public partial class SessionWindow : Window
    {
        private FloorItem _item;
        private int _gamesPlayed = 0;

        public SessionWindow(FloorItem item)
        {
            InitializeComponent();
            _item = item;
            TxtDeviceName.Text = _item.Name;

            // إخفاء أو إظهار خيارات الجولات حسب نوع الطاولة
            if (_item.Type == "billiards" || _item.Type == "tawla")
            {
                PanelGames.Visibility = Visibility.Visible;
            }
            else
            {
                PanelGames.Visibility = Visibility.Collapsed;
            }

            UpdateTotal();
        }

        private void BtnPlusGame_Click(object sender, RoutedEventArgs e)
        {
            _gamesPlayed++;
            TxtGameCount.Text = _gamesPlayed.ToString();
            UpdateTotal();
        }

        private void BtnMinusGame_Click(object sender, RoutedEventArgs e)
        {
            if (_gamesPlayed > 0)
            {
                _gamesPlayed--;
                TxtGameCount.Text = _gamesPlayed.ToString();
                UpdateTotal();
            }
        }

        private void UpdateTotal()
        {
            decimal total = _gamesPlayed * _item.GameRate;
            TxtTotalAmount.Text = $"{total:N0} د.ع";
        }

        private void BtnCloseSession_Click(object sender, RoutedEventArgs e)
        {
            decimal total = _gamesPlayed * _item.GameRate;
            MessageBox.Show($"تم إغلاق الجلسة بنجاح!\nالمبلغ المطلوب: {total:N0} د.ع", "تحصيل الحساب", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }
}
