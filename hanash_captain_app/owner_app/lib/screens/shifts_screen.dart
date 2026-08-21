import 'package:flutter/material.dart';
import 'package:owner_app/main.dart';

// نموذج الشفت المحلي لمنع أي تعارض استيراد
class LocalShiftModel {
  final String id;
  final String cashierName;
  final DateTime startTime;
  final DateTime? endTime;
  final double initialCash;
  final double expectedCash;
  final double actualCash;
  final String status;

  LocalShiftModel({
    required this.id,
    required this.cashierName,
    required this.startTime,
    this.endTime,
    required this.initialCash,
    required this.expectedCash,
    required this.actualCash,
    required this.status,
  });

 factory LocalShiftModel.fromJson(Map<String, dynamic> json) {
  DateTime parseRawDateTime(String? dateStr) {
    if (dateStr == null || dateStr.isEmpty) return DateTime.now();
    
    // تحويل النص إلى DateTime
    DateTime parsed = DateTime.tryParse(dateStr) ?? DateTime.now();
    
    // إضافة 3 ساعات فوراً لتعويض النقص وتثبيت توقيت بغداد
    return parsed.add(const Duration(hours: 3));
  }

  return LocalShiftModel(
    id: json['id'].toString(),
    cashierName: json['cashier_name'] ?? json['user_name'] ?? 'كاشير غير معروف',
    startTime: parseRawDateTime(json['start_time']),
    endTime: json['end_time'] != null ? parseRawDateTime(json['end_time']) : null,
    initialCash: (json['initial_cash'] ?? json['start_cash'] ?? 0).toDouble(),
    expectedCash: (json['expected_cash'] ?? 0).toDouble(),
    actualCash: (json['actual_cash'] ?? json['end_cash'] ?? 0).toDouble(),
    status: json['status'] ?? 'open',
  );
}
}

class ShiftsScreen extends StatefulWidget {
  const ShiftsScreen({super.key});

  @override
  State<ShiftsScreen> createState() => _ShiftsScreenState();
}

class _ShiftsScreenState extends State<ShiftsScreen> {
  late Future<List<LocalShiftModel>> _shiftsFuture;

  @override
  void initState() {
    super.initState();
    _loadShifts();
  }

  void _loadShifts() {
    setState(() {
      _shiftsFuture = supabase
          .from('shifts')
          .select()
          .order('start_time', ascending: false)
          .then((data) => (data as List).map((json) => LocalShiftModel.fromJson(json)).toList());
    });
  }

  // تنسيق الوقت مع إضافة الصفر لضمان المظهر
  String _formatTime(DateTime dt) {
    final hour = dt.hour.toString().padLeft(2, '0');
    final minute = dt.minute.toString().padLeft(2, '0');
    return "$hour:$minute";
  }

  // نافذة تفاصيل الكاش والشفت كاملة عند الضغط
  void _showShiftDetails(LocalShiftModel shift) {
    final startTimeStr = _formatTime(shift.startTime);
    final endTimeStr = shift.endTime != null
        ? _formatTime(shift.endTime!)
        : 'مستمر حتى الآن';

    final diff = shift.actualCash - shift.expectedCash;
    String diffStatus = 'مطابق تماماً 🟢';
    Color diffColor = Colors.green;

    if (diff > 0) {
      diffStatus = 'زيادة: +${diff.toStringAsFixed(0)} د.ع 🔵';
      diffColor = Colors.blue;
    } else if (diff < 0) {
      diffStatus = 'عجز: ${diff.toStringAsFixed(0)} د.ع 🔴';
      diffColor = Colors.redAccent;
    }

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('تفاصيل شفت: ${shift.cashierName}'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _detailRow('الحالة:', shift.status == 'open' ? 'مفتوح حالياً' : 'مغلق'),
            _detailRow('وقت الفتح:', startTimeStr),
            _detailRow('وقت الإغلاق:', endTimeStr),
            const Divider(),
            _detailRow('الخردة (الافتتاحي):', '${shift.initialCash.toStringAsFixed(0)} د.ع'),
            _detailRow('المبيعات المتوقعة:', '${shift.expectedCash.toStringAsFixed(0)} د.ع'),
            _detailRow('الكاش الفعلي في الصندوق:', '${shift.actualCash.toStringAsFixed(0)} د.ع'),
            const Divider(),
            if (shift.status == 'closed')
              Padding(
                padding: const EdgeInsets.only(top: 8.0),
                child: Text(
                  'النتيجة النهائي: $diffStatus',
                  style: TextStyle(fontWeight: FontWeight.bold, color: diffColor, fontSize: 16),
                ),
              ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('إغلاق'),
          ),
        ],
      ),
    );
  }

  Widget _detailRow(String title, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4.0),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(title, style: const TextStyle(fontWeight: FontWeight.bold)),
          Text(value, style: const TextStyle(color: Colors.white70)),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('سجل الشفتات والكاش'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadShifts,
          )
        ],
      ),
      body: FutureBuilder<List<LocalShiftModel>>(
        future: _shiftsFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (snapshot.hasError) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(16.0),
                child: Text(
                  'خطأ في جلب الشفتات: ${snapshot.error}',
                  style: const TextStyle(color: Colors.redAccent),
                  textAlign: TextAlign.center,
                ),
              ),
            );
          }

          final shifts = snapshot.data ?? [];

          if (shifts.isEmpty) {
            return const Center(child: Text('لا توجد شفتات مسجلة في قاعدة البيانات'));
          }

          return RefreshIndicator(
            onRefresh: () async => _loadShifts(),
            child: ListView.builder(
              itemCount: shifts.length,
              itemBuilder: (context, index) {
                final shift = shifts[index];
                final isOpen = shift.status == 'open';

                return Card(
                  color: isOpen ? Colors.green[900]?.withValues(alpha: 0.4) : Colors.grey[850],
                  margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  child: ListTile(
                    onTap: () => _showShiftDetails(shift),
                    leading: Icon(
                      isOpen ? Icons.lock_open : Icons.lock,
                      color: isOpen ? Colors.greenAccent : Colors.grey,
                    ),
                    title: Text(
                      'الكاشير: ${shift.cashierName}',
                      style: const TextStyle(fontWeight: FontWeight.bold),
                    ),
                    subtitle: Text(
                      'وقت الفتح: ${_formatTime(shift.startTime)}\n'
                      'اضغط للظهور الكامل للتفاصيل والعجز/الزيادة',
                    ),
                    trailing: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                      decoration: BoxDecoration(
                        color: isOpen ? Colors.green : Colors.grey[700],
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Text(
                        isOpen ? 'مفتوح' : 'مغلق',
                        style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 12),
                      ),
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