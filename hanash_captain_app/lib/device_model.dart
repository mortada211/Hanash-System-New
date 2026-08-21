class DeviceModel {
  final String id;
  final String name;
  final String type;
  final bool isActive;
  final String timeElapsed;
  final double totalAmount;

  DeviceModel({
    required this.id,
    required this.name,
    required this.type,
    required this.isActive,
    required this.timeElapsed,
    required this.totalAmount,
  });

 factory DeviceModel.fromJson(Map<String, dynamic> json) {
  // 🎯 قراءة النوع أو الفئة بأمان
  String deviceType = json['type'] ?? json['category'] ?? 'ps';
  
  return DeviceModel(
    id: json['id'] ?? json['name'] ?? '',
    name: json['name'] ?? '',
    type: deviceType,
    isActive: json['isActive'] ?? (json['orders'] != null && json['orders'].isNotEmpty 
        ? (json['orders'][0]['status'] == 'busy') 
        : false),
    timeElapsed: json['timeElapsed'] ?? '00:00',
    totalAmount: (json['totalAmount'] ?? 0).toDouble(),
  );
}
}