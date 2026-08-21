import 'package:flutter/material.dart';
import 'package:owner_app/main.dart';

class InvoiceModel {
  final String id;
  final String deviceName;
  final double totalAmount;
  final double profit;
  final DateTime startTime;
  final DateTime? endTime;
  final String status;
  final List<dynamic> orders;

  InvoiceModel({
    required this.id,
    required this.deviceName,
    required this.totalAmount,
    required this.profit,
    required this.startTime,
    this.endTime,
    required this.status,
    required this.orders,
  });
}

class ReportsScreen extends StatefulWidget {
  const ReportsScreen({super.key});

  @override
  State<ReportsScreen> createState() => _ReportsScreenState();
}

class _ReportsScreenState extends State<ReportsScreen> {
  late Future<List<InvoiceModel>> _invoicesFuture;
  DateTimeRange? _selectedDateRange;
  String _selectedFilterLabel = 'الكل';
  String _selectedCategory = 'الكل'; // فلترة الأجهزة/الطاولات

  @override
  void initState() {
    super.initState();
    _loadInvoices();
  }

  void _loadInvoices() {
    setState(() {
      _invoicesFuture = _fetchAndCalculateData();
    });
  }

  // تحويل الوقت مع إضافة 3 ساعات لتطابق توقيت بغداد
  DateTime _parseRawDateTime(String? dateStr) {
    if (dateStr == null || dateStr.isEmpty) return DateTime.now();
    DateTime parsed = DateTime.tryParse(dateStr) ?? DateTime.now();
    return parsed.add(const Duration(hours: 3));
  }

  Future<List<InvoiceModel>> _fetchAndCalculateData() async {
    final results = await Future.wait([
      supabase.from('sessions').select().order('start_time', ascending: false),
      supabase.from('session_orders').select(),
      supabase.from('products').select(),
    ]);

    final List<dynamic> sessionsData = results[0];
    final List<dynamic> ordersData = results[1];
    final List<dynamic> productsData = results[2];

    Map<String, double> productCosts = {};
    for (var p in productsData) {
      final name = p['name']?.toString() ?? '';
      final cost = (p['cost_price'] ?? 0).toDouble();
      productCosts[name] = cost;
    }

    Map<String, List<dynamic>> sessionOrdersMap = {};
    for (var o in ordersData) {
      String sId = o['session_id'].toString();
      if (!sessionOrdersMap.containsKey(sId)) {
        sessionOrdersMap[sId] = [];
      }
      sessionOrdersMap[sId]!.add(o);
    }

    List<InvoiceModel> invoices = [];
    for (var s in sessionsData) {
      String sId = s['id'].toString();
      double totalAmount = (s['total_amount'] ?? s['TotalAmount'] ?? 0).toDouble();

      double itemsRevenue = 0;
      double itemsCost = 0;

      List<dynamic> sessionOrders = sessionOrdersMap[sId] ?? [];

      for (var o in sessionOrders) {
        double qty = (o['quantity'] ?? 1).toDouble();
        double price = (o['unit_price'] ?? 0).toDouble();
        double totalP = (o['total_price'] ?? (qty * price)).toDouble();
        String itemName = o['item_name'] ?? '';

        itemsRevenue += totalP;
        itemsCost += (productCosts[itemName] ?? 0) * qty;
      }

      double playTimeRevenue = totalAmount - itemsRevenue;
      if (playTimeRevenue < 0) playTimeRevenue = 0;

      double finalProfit = playTimeRevenue + (itemsRevenue - itemsCost);

      invoices.add(InvoiceModel(
        id: sId,
        deviceName: s['device_name'] ?? s['device'] ?? 'جلسة/محل',
        totalAmount: totalAmount,
        profit: finalProfit,
        startTime: _parseRawDateTime(s['start_time']),
        endTime: s['end_time'] != null ? _parseRawDateTime(s['end_time']) : null,
        status: s['status'] ?? 'completed',
        orders: sessionOrders,
      ));
    }

    return invoices;
  }

  // دالة تصفية البيانات (تاريخ + طاولات/أجهزة)
  List<InvoiceModel> _filterInvoices(List<InvoiceModel> invoices) {
    List<InvoiceModel> filtered = invoices;

    // 1. فلترة النوع (طاولة أو أجهزة)
    if (_selectedCategory == 'طاولات فقط') {
      filtered = filtered.where((inv) => inv.deviceName.contains('طاولة') || inv.deviceName.contains('طاوله')).toList();
    } else if (_selectedCategory == 'أجهزة فقط') {
      filtered = filtered.where((inv) => !inv.deviceName.contains('طاولة') && !inv.deviceName.contains('طاوله')).toList();
    }

    // 2. فلترة التاريخ
    if (_selectedDateRange != null) {
      final start = DateTime(_selectedDateRange!.start.year, _selectedDateRange!.start.month, _selectedDateRange!.start.day, 0, 0, 0);
      final end = DateTime(_selectedDateRange!.end.year, _selectedDateRange!.end.month, _selectedDateRange!.end.day, 23, 59, 59);
      filtered = filtered.where((inv) => inv.startTime.isAfter(start) && inv.startTime.isBefore(end)).toList();
    }

    return filtered;
  }

  void _setFilterPreset(String preset) {
    final now = DateTime.now();
    setState(() {
      _selectedFilterLabel = preset;
      if (preset == 'اليوم') {
        _selectedDateRange = DateTimeRange(start: now, end: now);
      } else if (preset == 'الأمس') {
        final yesterday = now.subtract(const Duration(days: 1));
        _selectedDateRange = DateTimeRange(start: yesterday, end: yesterday);
      } else if (preset == 'آخر 7 أيام') {
        _selectedDateRange = DateTimeRange(start: now.subtract(const Duration(days: 6)), end: now);
      } else if (preset == 'هذا الشهر') {
        _selectedDateRange = DateTimeRange(start: DateTime(now.year, now.month, 1), end: now);
      } else {
        _selectedDateRange = null;
      }
    });
  }

  Future<void> _pickCustomDateRange() async {
    final picked = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2020),
      lastDate: DateTime.now(),
      initialDateRange: _selectedDateRange,
      builder: (context, child) {
        return Theme(
          data: Theme.of(context).copyWith(
            colorScheme: const ColorScheme.dark(primary: Colors.amber, onPrimary: Colors.black, surface: Color(0xFF212121)),
          ),
          child: child!,
        );
      },
    );
    if (picked != null) {
      setState(() {
        _selectedDateRange = picked;
        _selectedFilterLabel = 'مخصص';
      });
    }
  }

  String _formatTime(DateTime dt) {
    final hour = dt.hour.toString().padLeft(2, '0');
    final minute = dt.minute.toString().padLeft(2, '0');
    return "$hour:$minute";
  }

  void _showInvoiceDetails(InvoiceModel invoice) {
    final startStr = "${invoice.startTime.year}/${invoice.startTime.month}/${invoice.startTime.day} - ${_formatTime(invoice.startTime)}";
    final endStr = invoice.endTime != null 
        ? "${invoice.endTime!.year}/${invoice.endTime!.month}/${invoice.endTime!.day} - ${_formatTime(invoice.endTime!)}" 
        : "نشطة (غير مغلقة)";

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('تفاصيل الفاتورة #${invoice.id.length > 8 ? invoice.id.substring(0, 8) : invoice.id}'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _detailRow('المكان / الجهاز:', invoice.deviceName),
            _detailRow('وقت البدء:', startStr),
            _detailRow('وقت الإغلاق:', endStr),
            const Divider(),
            if (invoice.orders.isNotEmpty) ...[
              const Text('الطلبات:', style: TextStyle(fontWeight: FontWeight.bold, color: Colors.amber)),
              const SizedBox(height: 5),
              ...invoice.orders.map((o) => Padding(
                padding: const EdgeInsets.only(bottom: 4.0),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text('${o['item_name']} (x${o['quantity']})', style: const TextStyle(fontSize: 13)),
                    Text('${o['total_price']} د.ع', style: const TextStyle(fontSize: 13)),
                  ],
                ),
              )),
              const Divider(),
            ],
            _detailRow('إجمالي الفاتورة:', '${invoice.totalAmount.toStringAsFixed(0)} د.ع'),
            _detailRow('صافي الربح:', '${invoice.profit.toStringAsFixed(0)} د.ع', color: Colors.greenAccent),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('إغلاق')),
        ],
      ),
    );
  }

  void _showDetailedReport(List<InvoiceModel> filteredInvoices) {
    double totalSales = 0;
    double totalProfit = 0;
    double highestInvoice = 0;
    double lowestInvoice = filteredInvoices.isNotEmpty ? filteredInvoices.first.totalAmount : 0;
    int completedSessions = 0;
    int activeSessions = 0;

    Map<String, double> salesByDevice = {};
    Map<String, double> profitByDevice = {};

    for (var inv in filteredInvoices) {
      totalSales += inv.totalAmount;
      totalProfit += inv.profit;

      if (inv.totalAmount > highestInvoice) highestInvoice = inv.totalAmount;
      if (inv.totalAmount < lowestInvoice) lowestInvoice = inv.totalAmount;

      if (inv.endTime != null || inv.status == 'completed') {
        completedSessions++;
      } else {
        activeSessions++;
      }

      salesByDevice[inv.deviceName] = (salesByDevice[inv.deviceName] ?? 0) + inv.totalAmount;
      profitByDevice[inv.deviceName] = (profitByDevice[inv.deviceName] ?? 0) + inv.profit;
    }

    final avgInvoice = filteredInvoices.isNotEmpty ? totalSales / filteredInvoices.length : 0;

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.grey[900],
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(20))),
      builder: (context) {
        return DraggableScrollableSheet(
          expand: false,
          initialChildSize: 0.75,
          maxChildSize: 0.9,
          builder: (context, scrollController) {
            return ListView(
              controller: scrollController,
              padding: const EdgeInsets.all(20),
              children: [
                Center(
                  child: Container(
                    width: 40,
                    height: 4,
                    decoration: BoxDecoration(color: Colors.grey[600], borderRadius: BorderRadius.circular(10)),
                  ),
                ),
                const SizedBox(height: 15),
                const Row(
                  children: [
                    Icon(Icons.analytics, color: Colors.amber, size: 28),
                    SizedBox(width: 10),
                    Text('التقرير المالي والتفصيلي', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
                  ],
                ),
                Text(
                  'الفترة: $_selectedFilterLabel | التصنيف: $_selectedCategory',
                  style: const TextStyle(color: Colors.white60, fontSize: 13),
                ),
                const Divider(height: 20),
                Row(
                  children: [
                    Expanded(child: _reportCard(title: 'إجمالي المبيعات', value: '${totalSales.toStringAsFixed(0)} د.ع', icon: Icons.monetization_on, color: Colors.blueAccent)),
                    const SizedBox(width: 10),
                    Expanded(child: _reportCard(title: 'صافي الربح', value: '${totalProfit.toStringAsFixed(0)} د.ع', icon: Icons.account_balance_wallet, color: Colors.greenAccent)),
                  ],
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(child: _reportCard(title: 'معدل قيمة الفاتورة', value: '${avgInvoice.toStringAsFixed(0)} د.ع', icon: Icons.show_chart, color: Colors.purpleAccent)),
                    const SizedBox(width: 10),
                    Expanded(child: _reportCard(title: 'إجمالي الفواتير', value: '${filteredInvoices.length}', icon: Icons.receipt, color: Colors.amberAccent)),
                  ],
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(child: _reportCard(title: 'أعلى فاتورة', value: '${highestInvoice.toStringAsFixed(0)} د.ع', icon: Icons.arrow_upward, color: Colors.tealAccent)),
                    const SizedBox(width: 10),
                    Expanded(child: _reportCard(title: 'أدنى فاتورة', value: '${lowestInvoice.toStringAsFixed(0)} د.ع', icon: Icons.arrow_downward, color: Colors.orangeAccent)),
                  ],
                ),
                const SizedBox(height: 20),
                const Text('حالة الجلسات:', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: Colors.amber)),
                const SizedBox(height: 8),
                _detailRow('جلسات مكتملة ومغلقة:', '$completedSessions', color: Colors.white),
                _detailRow('جلسات قيد التشغيل حالياً:', '$activeSessions', color: Colors.greenAccent),
                const Divider(height: 30),
                const Text('أداء الأجهزة والمبيعات:', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: Colors.amber)),
                const SizedBox(height: 10),
                ...salesByDevice.entries.map((entry) {
                  final deviceName = entry.key;
                  final sales = entry.value;
                  final profit = profitByDevice[deviceName] ?? 0;
                  final percentage = totalSales > 0 ? (sales / totalSales) * 100 : 0.0;

                  return Padding(
                    padding: const EdgeInsets.symmetric(vertical: 8.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text(deviceName, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15)),
                            Text('${sales.toStringAsFixed(0)} د.ع', style: const TextStyle(fontWeight: FontWeight.bold)),
                          ],
                        ),
                        const SizedBox(height: 2),
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text('نسبة المبيعات: ${percentage.toStringAsFixed(1)}%', style: const TextStyle(fontSize: 11, color: Colors.white54)),
                            Text('الربح: ${profit.toStringAsFixed(0)} د.ع', style: const TextStyle(fontSize: 11, color: Colors.greenAccent)),
                          ],
                        ),
                        const SizedBox(height: 6),
                        LinearProgressIndicator(
                          value: percentage / 100,
                          backgroundColor: Colors.grey[800],
                          color: Colors.amber,
                          minHeight: 6,
                          borderRadius: BorderRadius.circular(10),
                        ),
                      ],
                    ),
                  );
                }),
                const SizedBox(height: 20),
              ],
            );
          },
        );
      },
    );
  }

  Widget _reportCard({required String title, required String value, required IconData icon, required Color color}) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(color: Colors.grey[850], borderRadius: BorderRadius.circular(12), border: Border.all(color: Colors.white12)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, color: color, size: 18),
              const SizedBox(width: 6),
              Text(title, style: const TextStyle(fontSize: 11, color: Colors.white70)),
            ],
          ),
          const SizedBox(height: 8),
          Text(value, style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: color)),
        ],
      ),
    );
  }

  Widget _detailRow(String title, String value, {Color? color}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4.0),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(title, style: const TextStyle(fontWeight: FontWeight.bold)),
          Text(value, style: TextStyle(color: color ?? Colors.white70, fontWeight: color != null ? FontWeight.bold : null)),
        ],
      ),
    );
  }

  Widget _filterChip(String label) {
    final isSelected = _selectedFilterLabel == label;
    return Padding(
      padding: const EdgeInsets.only(left: 6.0),
      child: ChoiceChip(
        label: Text(label),
        selected: isSelected,
        selectedColor: Colors.amber,
        labelStyle: TextStyle(color: isSelected ? Colors.black : Colors.white),
        onSelected: (selected) { if (selected) _setFilterPreset(label); },
      ),
    );
  }

  Widget _categoryChip(String label) {
    final isSelected = _selectedCategory == label;
    return ChoiceChip(
      label: Text(label),
      selected: isSelected,
      selectedColor: Colors.blueAccent,
      labelStyle: TextStyle(color: isSelected ? Colors.white : Colors.white70, fontWeight: FontWeight.bold),
      onSelected: (selected) {
        if (selected) {
          setState(() {
            _selectedCategory = label;
          });
        }
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('المبيعات والأرباح'),
        actions: [IconButton(icon: const Icon(Icons.refresh), onPressed: _loadInvoices)],
      ),
      body: FutureBuilder<List<InvoiceModel>>(
        future: _invoicesFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) return const Center(child: CircularProgressIndicator());
          if (snapshot.hasError) return Center(child: Text('خطأ: ${snapshot.error}', style: const TextStyle(color: Colors.redAccent)));

          final invoices = _filterInvoices(snapshot.data ?? []);
          double totalSales = 0, totalProfit = 0;
          for (var inv in invoices) {
            totalSales += inv.totalAmount;
            totalProfit += inv.profit;
          }

          return RefreshIndicator(
            onRefresh: () async => _loadInvoices(),
            child: Column(
              children: [
                // فلترة الأجهزة/الطاولات
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 6.0, horizontal: 12.0),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                    children: [
                      _categoryChip('الكل'),
                      _categoryChip('أجهزة فقط'),
                      _categoryChip('طاولات فقط'),
                    ],
                  ),
                ),
                // فلترة التواريخ
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  child: Row(
                    children: [
                      _filterChip('الكل'), _filterChip('اليوم'), _filterChip('الأمس'), _filterChip('آخر 7 أيام'), _filterChip('هذا الشهر'),
                      ActionChip(
                        avatar: const Icon(Icons.calendar_month, size: 16, color: Colors.amber),
                        label: Text(_selectedFilterLabel == 'مخصص' ? 'مخصص' : 'تحديد تاريخ'),
                        backgroundColor: _selectedFilterLabel == 'مخصص' ? Colors.amber.withValues(alpha: 0.3) : Colors.grey[800],
                        onPressed: _pickCustomDateRange,
                      ),
                    ],
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 12.0),
                  child: Row(
                    children: [
                      Expanded(
                        child: Card(
                          color: Colors.blueGrey[900],
                          child: Padding(
                            padding: const EdgeInsets.all(12.0),
                            child: Column(
                              children: [
                                const Text('المبيعات', style: TextStyle(fontSize: 12, color: Colors.white70)),
                                Text('${totalSales.toStringAsFixed(0)} د.ع', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Colors.blueAccent)),
                              ],
                            ),
                          ),
                        ),
                      ),
                      Expanded(
                        child: Card(
                          color: Colors.blueGrey[900],
                          child: Padding(
                            padding: const EdgeInsets.all(12.0),
                            child: Column(
                              children: [
                                const Text('صافي الربح', style: TextStyle(fontSize: 12, color: Colors.white70)),
                                Text('${totalProfit.toStringAsFixed(0)} د.ع', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Colors.greenAccent)),
                              ],
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 12.0, vertical: 4.0),
                  child: SizedBox(
                    width: double.infinity,
                    child: ElevatedButton.icon(
                      style: ElevatedButton.styleFrom(backgroundColor: Colors.amber, foregroundColor: Colors.black),
                      icon: const Icon(Icons.assessment),
                      label: const Text('عرض التقرير المفصل'),
                      onPressed: () => _showDetailedReport(invoices),
                    ),
                  ),
                ),
                const Divider(),
                Expanded(
                  child: invoices.isEmpty
                      ? const Center(child: Text('لا توجد بيانات مطابقة للفلترة'))
                      : ListView.builder(
                          itemCount: invoices.length,
                          itemBuilder: (context, index) {
                            final inv = invoices[index];
                            final time = "${_formatTime(inv.startTime)} - ${inv.startTime.day}/${inv.startTime.month}";
                            return Card(
                              color: Colors.grey[850],
                              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                              child: ListTile(
                                onTap: () => _showInvoiceDetails(inv),
                                leading: const CircleAvatar(backgroundColor: Colors.amber, child: Icon(Icons.receipt_long, color: Colors.black)),
                                title: Text('${inv.deviceName} | فاتورة #${inv.id.length > 5 ? inv.id.substring(0, 5) : inv.id}', style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13)),
                                subtitle: Text('الوقت: $time'),
                                trailing: Column(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  crossAxisAlignment: CrossAxisAlignment.end,
                                  children: [
                                    Text('${inv.totalAmount.toStringAsFixed(0)} د.ع', style: const TextStyle(fontWeight: FontWeight.bold, color: Colors.blueAccent)),
                                    Text('ربح: ${inv.profit.toStringAsFixed(0)}', style: const TextStyle(fontSize: 11, color: Colors.greenAccent)),
                                  ],
                                ),
                              ),
                            );
                          },
                        ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}