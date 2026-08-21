import 'package:flutter/material.dart';
import 'package:owner_app/main.dart';

// تعريف النموذج مباشرة هنا لمنع أي تعارض استيراد
class LocalSessionModel {
  final String id;
  final String deviceName;
  final String status;
  final double totalAmount;

  LocalSessionModel({
    required this.id,
    required this.deviceName,
    required this.status,
    required this.totalAmount,
  });

  factory LocalSessionModel.fromJson(Map<String, dynamic> json) {
    return LocalSessionModel(
      id: json['id'].toString(),
      deviceName: json['device_name'] ?? json['device'] ?? 'جهاز',
      status: json['status'] ?? 'unknown',
      totalAmount: (json['total_amount'] ?? json['price'] ?? 0).toDouble(),
    );
  }
}

class DashboardScreen extends StatelessWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('لوحة التحكم - صاحب المال'),
        centerTitle: true,
      ),
      body: StreamBuilder<List<Map<String, dynamic>>>(
        stream: supabase.from('sessions').stream(primaryKey: ['id']),
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (!snapshot.hasData || snapshot.data!.isEmpty) {
            return const Center(child: Text('لا توجد جلسات مفتوحة حالياً'));
          }

          final sessions = snapshot.data!
              .map((json) => LocalSessionModel.fromJson(json))
              .toList();

          final activeSessions = sessions.where((s) => s.status != 'completed').toList();

          return Padding(
            padding: const EdgeInsets.all(12.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Card(
                  color: Colors.blueGrey[800],
                  child: Padding(
                    padding: const EdgeInsets.all(16.0),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        const Text('الجلسات النشطة حالياً:', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
                        Text('${activeSessions.length}', style: const TextStyle(fontSize: 22, color: Colors.amber, fontWeight: FontWeight.bold)),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 15),
                const Text('مراقبة الأجهزة اللحظية:', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                const SizedBox(height: 10),
                Expanded(
                  child: ListView.builder(
                    itemCount: activeSessions.length,
                    itemBuilder: (context, index) {
                      final item = activeSessions[index];
                      final isPending = item.status == 'pending_checkout';

                      return Card(
                        color: isPending ? Colors.orange[900] : Colors.grey[850],
                        child: ListTile(
                          leading: Icon(
                            isPending ? Icons.hourglass_top : Icons.gamepad,
                            color: isPending ? Colors.amber : Colors.greenAccent,
                          ),
                          title: Text(item.deviceName, style: const TextStyle(fontWeight: FontWeight.bold)),
                          subtitle: Text(isPending ? 'بانتظار الحساب' : 'لعب مستمر'),
                          trailing: Text(
                            '${item.totalAmount.toStringAsFixed(0)} د.ع',
                            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
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