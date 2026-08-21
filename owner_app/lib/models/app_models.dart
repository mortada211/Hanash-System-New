import 'package:intl/intl.dart';

// دالة عامة لتنسيق جميع المبالغ المالية مع الفواصل
String formatCurrency(num amount) {
  final formatter = NumberFormat('#,##0', 'en_US');
  return formatter.format(amount);
}

class SessionModel {
  final String id;
  final String deviceName;
  final String status;
  final double totalAmount;

  SessionModel({
    required this.id,
    required this.deviceName,
    required this.status,
    required this.totalAmount,
  });

  // خاصية للحصول على المبلغ المنسق بالفواصل
  String get formattedTotalAmount => formatCurrency(totalAmount);

  factory SessionModel.fromJson(Map<String, dynamic> json) {
    return SessionModel(
      id: json['id'].toString(),
      deviceName: json['device_name'] ?? json['device'] ?? 'جهاز',
      status: json['status'] ?? 'unknown',
      totalAmount: (json['total_amount'] ?? json['price'] ?? 0).toDouble(),
    );
  }
}

class ProductModel {
  final String id;
  final String name;
  final double costPrice;
  final double sellingPrice;
  final int stockQuantity;
  final int soldTodayQuantity; // إضافة كمية المبيعات اليومية للتقرير

  ProductModel({
    required this.id,
    required this.name,
    required this.costPrice,
    required this.sellingPrice,
    required this.stockQuantity,
    this.soldTodayQuantity = 0,
  });

  // خصائص للحصول على الأسعار المنسقة بالفواصل
  String get formattedCostPrice => formatCurrency(costPrice);
  String get formattedSellingPrice => formatCurrency(sellingPrice);

  factory ProductModel.fromJson(Map<String, dynamic> json) {
    return ProductModel(
      id: json['id'].toString(),
      name: json['name'] ?? '',
      costPrice: (json['cost_price'] ?? json['buy_price'] ?? 0).toDouble(),
      sellingPrice: (json['selling_price'] ?? 0).toDouble(),
      stockQuantity: (json['stock_quantity'] ?? 0).toInt(),
      soldTodayQuantity: (json['sold_today_quantity'] ?? json['sold_today'] ?? 0).toInt(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'cost_price': costPrice,
      'selling_price': sellingPrice,
      'stock_quantity': stockQuantity,
      'sold_today_quantity': soldTodayQuantity,
    };
  }
}

class ShiftModel {
  final String id;
  final String cashierName;
  final DateTime startTime;
  final DateTime? endTime;
  final double initialCash;
  final double expectedCash;
  final double actualCash;
  final String status;

  ShiftModel({
    required this.id,
    required this.cashierName,
    required this.startTime,
    this.endTime,
    required this.initialCash,
    required this.expectedCash,
    required this.actualCash,
    required this.status,
  });

  // خصائص تنسيق أرقام الكاش بالفواصل للواجهة
  String get formattedInitialCash => formatCurrency(initialCash);
  String get formattedExpectedCash => formatCurrency(expectedCash);
  String get formattedActualCash => formatCurrency(actualCash);

  factory ShiftModel.fromJson(Map<String, dynamic> json) {
    return ShiftModel(
      id: json['id'].toString(),
      cashierName: json['cashier_name'] ?? 'كاشير غير معروف',
      startTime: DateTime.parse(json['start_time']),
      endTime: json['end_time'] != null ? DateTime.parse(json['end_time']) : null,
      initialCash: (json['initial_cash'] ?? 0).toDouble(),
      expectedCash: (json['expected_cash'] ?? 0).toDouble(),
      actualCash: (json['actual_cash'] ?? 0).toDouble(),
      status: json['status'] ?? 'open',
    );
  }
}