import 'package:flutter/material.dart';
import '../main.dart';
import '../models/app_models.dart';

class InventoryScreen extends StatefulWidget {
  const InventoryScreen({super.key});

  @override
  State<InventoryScreen> createState() => _InventoryScreenState();
}

class _InventoryScreenState extends State<InventoryScreen> {
  late Future<List<ProductModel>> _productsFuture;

  @override
  void initState() {
    super.initState();
    _loadProducts();
  }

  void _loadProducts() {
    setState(() {
      _productsFuture = supabase
          .from('products')
          .select()
          .then((data) => (data as List).map((json) => ProductModel.fromJson(json)).toList());
    });
  }

  // نافذة إضافة منتج جديد
  void _showAddProductDialog() {
    final nameController = TextEditingController();
    final buyPriceController = TextEditingController();
    final sellPriceController = TextEditingController();
    final stockController = TextEditingController();

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('إضافة منتج جديد للمخزن'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: nameController,
                decoration: const InputDecoration(labelText: 'اسم المنتج'),
              ),
              const SizedBox(height: 10),
              TextField(
                controller: buyPriceController,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'سعر الشراء (الجملة) - د.ع'),
              ),
              const SizedBox(height: 10),
              TextField(
                controller: sellPriceController,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'سعر البيع (المفرد) - د.ع'),
              ),
              const SizedBox(height: 10),
              TextField(
                controller: stockController,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'الكمية الافتراضية'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('إلغاء'),
          ),
          ElevatedButton(
            onPressed: () async {
              final name = nameController.text.trim();
              final buyPrice = double.tryParse(buyPriceController.text) ?? 0;
              final sellPrice = double.tryParse(sellPriceController.text) ?? 0;
              final stock = int.tryParse(stockController.text) ?? 0;

              if (name.isNotEmpty) {
                try {
                  await supabase.from('products').insert({
                    'name': name,
                    'cost_price': buyPrice,
                    'selling_price': sellPrice,
                    'stock_quantity': stock,
                  });
                  if (mounted) {
                    Navigator.pop(context);
                    _loadProducts();
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(content: Text('تمت إضافة $name بنجاح!')),
                    );
                  }
                } catch (e) {
                  if (mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(content: Text('خطأ أثناء الإضافة: $e')),
                    );
                  }
                }
              }
            },
            child: const Text('حفظ'),
          ),
        ],
      ),
    );
  }

  // نافذة التعديل الشامل للمنتج
  void _showUpdateDialog(ProductModel product) {
    final buyPriceController = TextEditingController(text: product.costPrice.toStringAsFixed(0));
    final sellPriceController = TextEditingController(text: product.sellingPrice.toStringAsFixed(0));
    final stockController = TextEditingController(text: product.stockQuantity.toString());

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('تعديل كامل: ${product.name}'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: buyPriceController,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'سعر الشراء (الجملة) - د.ع'),
              ),
              const SizedBox(height: 10),
              TextField(
                controller: sellPriceController,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'سعر البيع (المفرد) - د.ع'),
              ),
              const SizedBox(height: 10),
              TextField(
                controller: stockController,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'الكمية في المخزن'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('إلغاء'),
          ),
          ElevatedButton(
            onPressed: () async {
              final newBuyPrice = double.tryParse(buyPriceController.text) ?? product.costPrice;
              final newSellPrice = double.tryParse(sellPriceController.text) ?? product.sellingPrice;
              final newStock = int.tryParse(stockController.text) ?? product.stockQuantity;

              await supabase.from('products').update({
                'cost_price': newBuyPrice,
                'selling_price': newSellPrice,
                'stock_quantity': newStock,
              }).eq('id', product.id);

              if (mounted) {
                Navigator.pop(context);
                _loadProducts();
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(content: Text('تم تحديث ${product.name} بنجاح!')),
                );
              }
            },
            child: const Text('حفظ التعديلات'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('إدارة المخزن'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadProducts,
          )
        ],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: _showAddProductDialog,
        backgroundColor: Colors.amber,
        child: const Icon(Icons.add, color: Colors.black),
      ),
      body: FutureBuilder<List<ProductModel>>(
        future: _productsFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          final products = snapshot.data ?? [];

          if (products.isEmpty) {
            return const Center(child: Text('لا توجد منتجات في المخزن (انقر + للإضافة)'));
          }

          return RefreshIndicator(
            onRefresh: () async => _loadProducts(),
            child: ListView.builder(
              itemCount: products.length,
              itemBuilder: (context, index) {
                final item = products[index];
                final isLowStock = item.stockQuantity <= 5;

                return Card(
                  color: isLowStock ? Colors.red[900]?.withValues(alpha: 0.4) : Colors.grey[850],
                  margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  child: ListTile(
                    title: Text(item.name, style: const TextStyle(fontWeight: FontWeight.bold)),
                    subtitle: Text('شراء: ${item.costPrice.toStringAsFixed(0)} | بيع: ${item.sellingPrice.toStringAsFixed(0)} د.ع'),
                    trailing: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                          decoration: BoxDecoration(
                            color: isLowStock ? Colors.red : Colors.green[800],
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Text(
                            'العدد: ${item.stockQuantity}',
                            style: const TextStyle(fontWeight: FontWeight.bold),
                          ),
                        ),
                        IconButton(
                          icon: const Icon(Icons.edit, color: Colors.amber),
                          onPressed: () => _showUpdateDialog(item),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}