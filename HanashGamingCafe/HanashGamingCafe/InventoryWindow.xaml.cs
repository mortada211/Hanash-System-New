using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace HanashGamingCafe
{
    public partial class InventoryWindow : Window
    {
        private List<Product> _allProducts = new List<Product>();
        private Product _selectedProduct = null;

        public InventoryWindow()
        {
            InitializeComponent();
            Loaded += InventoryWindow_Loaded;
        }

        private async void InventoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadProductsAsync();
        }

        private async System.Threading.Tasks.Task LoadProductsAsync()
        {
            try
            {
                await SupabaseService.InitializeAsync();
                var response = await SupabaseService.Instance.From<Product>().Get();
                _allProducts = response.Models;
                GridProducts.ItemsSource = _allProducts;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء تحميل المنتجات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnAddStock_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("يرجى إدخال اسم المنتج!", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(TxtStockQuantity.Text, out decimal addedQty) || addedQty <= 0)
            {
                MessageBox.Show("يرجى إدخال كمية صحيحة أكبر من صفر للتوريد!", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string barcode = string.IsNullOrWhiteSpace(TxtBarcode.Text) ? null : TxtBarcode.Text.Trim();
                string name = TxtName.Text.Trim();

                // البحث هل المنتج موجود سابقاً بنفس الباركود أو الاسم؟
                var existingProduct = _allProducts.FirstOrDefault(p =>
                    (!string.IsNullOrEmpty(barcode) && p.Barcode == barcode) ||
                    p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (existingProduct != null)
                {
                    // توريد فقط: زيادة الكمية الحالية بالكمية المضافة دون تعديل باقي البيانات الأساسية
                    existingProduct.StockQuantity += addedQty;

                    await SupabaseService.Instance
                        .From<Product>()
                        .Where(p => p.Id == existingProduct.Id)
                        .Update(existingProduct);

                    MessageBox.Show($"تم توريد وزيادة الكمية بنجاح!\nالمنتج: {existingProduct.Name}\nالكمية الجديدة: {existingProduct.StockQuantity}", "نجاح التوريد", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // منتج جديد بالكامل: يتم إدراجه في قاعدة البيانات
                    var newProduct = new Product
                    {
                        Barcode = barcode,
                        Name = name,
                        Category = (CmbCategory.SelectedItem as ComboBoxItem)?.Content.ToString(),
                        Unit = (CmbUnit.SelectedItem as ComboBoxItem)?.Content.ToString(),
                        CostPrice = decimal.TryParse(TxtCostPrice.Text, out decimal cp) ? cp : 0,
                        SellingPrice = decimal.TryParse(TxtSellingPrice.Text, out decimal sp) ? sp : 0,
                        StockQuantity = addedQty,
                        MinStockLevel = decimal.TryParse(TxtMinStock.Text, out decimal ms) ? ms : 5
                    };

                    await SupabaseService.Instance.From<Product>().Insert(newProduct);
                    MessageBox.Show("تمت إضافة المنتج الجديد إلى المخزن بنجاح!", "نجاح الإضافة", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                ClearFields();
                await LoadProductsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء حفظ/توريد المنتج: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GridProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridProducts.SelectedItem is Product prod)
            {
                _selectedProduct = prod;
                TxtBarcode.Text = prod.Barcode;
                TxtName.Text = prod.Name;
                TxtCostPrice.Text = prod.CostPrice.ToString();
                TxtSellingPrice.Text = prod.SellingPrice.ToString();
                TxtStockQuantity.Text = "1"; // الافتراضي عند اختيار منتج موجود هو إضافة 1 كمية توريد
                TxtMinStock.Text = prod.MinStockLevel.ToString();
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ClearFields();
        }

        private void ClearFields()
        {
            _selectedProduct = null;
            TxtBarcode.Clear();
            TxtName.Clear();
            TxtCostPrice.Text = "0";
            TxtSellingPrice.Text = "0";
            TxtStockQuantity.Text = "1";
            TxtMinStock.Text = "5";
            GridProducts.UnselectAll();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadProductsAsync();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtSearch.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(query))
            {
                GridProducts.ItemsSource = _allProducts;
            }
            else
            {
                GridProducts.ItemsSource = _allProducts.Where(p =>
                    (!string.IsNullOrEmpty(p.Name) && p.Name.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(p.Barcode) && p.Barcode.ToLower().Contains(query))
                ).ToList();
            }
        }
    }
}