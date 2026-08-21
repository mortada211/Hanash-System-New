using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HanashGamingCafe
{
    public partial class MenuWindow : Window
    {
        // الخاصية المرتجعة للنافذة عند اختيار الكاشير لمنتج معين
        public Product SelectedProduct { get; private set; }

        public MenuWindow()
        {
            InitializeComponent();
            Loaded += MenuWindow_Loaded;
        }

        private async void MenuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadMenuProductsAsync();
        }

        private async Task LoadMenuProductsAsync()
        {
            try
            {
                await SupabaseService.InitializeAsync();

                // جلب جميع المنتجات التي تملك كمية متوفرة في المخزن
                var response = await SupabaseService.Instance
                    .From<Product>()
                    .Where(p => p.StockQuantity > 0)
                    .Get();

                var products = response.Models;

                // تفريغ الحاويات قبل التعبئة
                PanelJuices.Children.Clear();
                PanelHotDrinks.Children.Clear();
                PanelShisha.Children.Clear();
                PanelSnacks.Children.Clear();

                // توزيع المنتجات على WrapPanels بحسب القسم (Category)
                foreach (var product in products)
                {
                    var card = CreateProductCard(product);

                    string category = product.Category ?? "";

                    if (category.Contains("عصائر") || category.Contains("مشروبات باردة") || category.Contains("بارد"))
                    {
                        PanelJuices.Children.Add(card);
                    }
                    else if (category.Contains("ساخن") || category.Contains("مشروبات ساخنة"))
                    {
                        PanelHotDrinks.Children.Add(card);
                    }
                    else if (category.Contains("أراجيل") || category.Contains("ارجيلة") || category.Contains("شيشة"))
                    {
                        PanelShisha.Children.Add(card);
                    }
                    else
                    {
                        // التسالي والخدمات وأي قسم آخر
                        PanelSnacks.Children.Add(card);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء تحميل قائمة المنيو: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // إنشاء كارت أزرار بالخدمات لملاءمة الشاشات اللمسية والسرعة
        private Border CreateProductCard(Product product)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C2D3C")),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(5),
                Padding = new Thickness(8),
                Cursor = Cursors.Hand,
                Tag = product
            };

            // تأثير عند تحريك الماوس فوق الكارت
            border.MouseEnter += (s, e) => border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3C52"));
            border.MouseLeave += (s, e) => border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C2D3C"));

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            var nameText = new TextBlock
            {
                Text = product.Name,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var priceText = new TextBlock
            {
                Text = $"{product.SellingPrice:N0} د.ع",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC00")),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 5, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var stockText = new TextBlock
            {
                Text = $"المتوفر: {product.StockQuantity}",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0A0B0")),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stack.Children.Add(nameText);
            stack.Children.Add(priceText);
            stack.Children.Add(stockText);
            border.Child = stack;

            // عند النقر على المنتج يتم تحديده وإغلاق النافذة بنجاح
            border.MouseDown += (s, e) =>
            {
                SelectedProduct = product;
                DialogResult = true;
                Close();
            };

            return border;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}