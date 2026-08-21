import 'package:supabase_flutter/supabase_flutter.dart';

class ApiService {
  // 🟢 1. جلب الطلبات أو إضافة طلب جديد مباشرة إلى Supabase
  static Future<bool> sendOrder(String tableName, String itemTitle, int qty, double price) async {
    try {
      // البحث عن الجلسة النشطة بنفس اسم الطاولة/الجهاز
      final response = await Supabase.instance.client
          .from('sessions')
          .select()
          .eq('device_name', tableName.trim())
          .order('start_time', ascending: false)
          .limit(1);

      if (response.isNotEmpty) {
        final activeSession = response.first;
        String sessionId = activeSession['id'].toString();

        // إدراج الطلب التفصيلي في جدول session_orders ليقرأه الكاشير (C#)
        await Supabase.instance.client.from('session_orders').insert({
          'session_id': sessionId,
          'item_name': itemTitle,
          'price': price,
          'qty': qty,
        });

        // تحديث المجموع الإجمالي للجلسة
        double currentTotal = (activeSession['total_amount'] ?? 0.0).toDouble();
        double newTotal = currentTotal + (price * qty);

        await Supabase.instance.client
            .from('sessions')
            .update({'total_amount': newTotal})
            .eq('id', sessionId);

        return true;
      } else {
        print('لم يتم العثور على جلسة مفتوحة لـ $tableName');
        return false;
      }
    } catch (e) {
      print('خطأ في إرسال الطلب لـ Supabase: $e');
      return false;
    }
  }
}