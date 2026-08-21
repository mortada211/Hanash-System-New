import 'dart:async';
import 'dart:io';
import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:supabase_flutter/supabase_flutter.dart';
import 'dart:ui' as ui;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:image/image.dart' as img;
import 'package:flutter/rendering.dart';
void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  await Supabase.initialize(
    url: 'https://iihcevuyrdoezfozhots.supabase.co',
    anonKey: 'sb_publishable_jGD6jV7XZr2H8oXVKxxP4Q_IBI6eNwE',
  );

  runApp(const HanashSystemApp());
}

class HanashSystemApp extends StatelessWidget {
  const HanashSystemApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: 'نظام صالة حنش المتكامل',
      builder: (context, child) => Directionality(
        textDirection: TextDirection.rtl,
        child: child!,
      ),
      theme: ThemeData.dark().copyWith(
        scaffoldBackgroundColor: const Color(0xFF0F111A),
        cardColor: const Color(0xFF161824),
        primaryColor: const Color(0xFF6C5CE7),
      ),
      home: const CaptainDashboard(),
    );
  }
}

class CaptainDashboard extends StatefulWidget {
  const CaptainDashboard({super.key});

  @override
  State<CaptainDashboard> createState() => _CaptainDashboardState();
}

class _CaptainDashboardState extends State<CaptainDashboard> {
  int _selectedCategoryIndex = 0;
  List<Map<String, dynamic>> _devices = [];
  bool _isLoading = true;
  String? _errorMessage;
  StreamSubscription<List<Map<String, dynamic>>>? _devicesSubscription;
  Timer? _uiTimer;

  String _baristaPrinterIp = '192.168.0.102';
  double _psRoundPrice = 1000.0;

  final List<Map<String, String>> categories = [
    {'id': 'ps', 'name': '🎮 بلايستيشن'},
    {'id': 'bi', 'name': '🎱 بلياردو'},
    {'id': 'tab', 'name': '☕ طاولات جلوس'},
  ];

  @override
  void initState() {
    super.initState();
    _loadSettings();
    _setupRealtimeSubscription();
    _uiTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (mounted) setState(() {});
    });
  }

  Future<void> _loadSettings() async {
    final prefs = await SharedPreferences.getInstance();
    if (mounted) {
      setState(() {
        _baristaPrinterIp = prefs.getString('printer_ip') ?? _baristaPrinterIp;
        _psRoundPrice = prefs.getDouble('ps_round_price') ?? _psRoundPrice;
      });
    }
  }

  Future<void> _saveSettings(String newIp, double newPsPrice) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('printer_ip', newIp);
    await prefs.setDouble('ps_round_price', newPsPrice);
    if (mounted) {
      setState(() {
        _baristaPrinterIp = newIp;
        _psRoundPrice = newPsPrice;
      });
    }
  }

  void _openSettingsDialog() {
    final TextEditingController ipController = TextEditingController(text: _baristaPrinterIp);
    final TextEditingController psPriceController = TextEditingController(text: _psRoundPrice.toStringAsFixed(0));

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: const Color(0xFF161824),
        title: const Text('⚙️ الإعدادات العامة', style: TextStyle(color: Colors.white)),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('أدخل عنوان IP الخاص بطابعة البارستا:', style: TextStyle(color: Color(0xFFA0A5C0), fontSize: 13)),
            const SizedBox(height: 8),
            TextField(
              controller: ipController,
              style: const TextStyle(color: Colors.white),
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(
                hintText: 'مثال: 192.168.0.102',
                enabledBorder: OutlineInputBorder(borderSide: BorderSide(color: Color(0xFF2C2D3C))),
                focusedBorder: OutlineInputBorder(borderSide: BorderSide(color: Color(0xFFFFC048))),
              ),
            ),
            const SizedBox(height: 20),
            const Text('سعر الجولة/الجيم للبلايستيشن (د.ع):', style: TextStyle(color: Color(0xFFA0A5C0), fontSize: 13)),
            const SizedBox(height: 8),
            TextField(
              controller: psPriceController,
              style: const TextStyle(color: Colors.white),
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(
                hintText: 'مثال: 1000',
                enabledBorder: OutlineInputBorder(borderSide: BorderSide(color: Color(0xFF2C2D3C))),
                focusedBorder: OutlineInputBorder(borderSide: BorderSide(color: Color(0xFFFFC048))),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('إلغاء')),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFF00B894)),
            onPressed: () async {
              final String newIp = ipController.text.trim();
              final double newPrice = double.tryParse(psPriceController.text.trim()) ?? 1000.0;
              if (newIp.isNotEmpty) {
                await _saveSettings(newIp, newPrice);
                if (context.mounted) {
                  Navigator.pop(context);
                  ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تم حفظ الإعدادات بنجاح ✅'), backgroundColor: Color(0xFF00B894)));
                }
              }
            },
            child: const Text('حفظ', style: TextStyle(color: Colors.white)),
          ),
        ],
      ),
    );
  }

  void _setupRealtimeSubscription() {
    _devicesSubscription = Supabase.instance.client
        .from('devices')
        .stream(primaryKey: ['id'])
        .order('name', ascending: true)
        .listen((data) {
          if (mounted) {
            setState(() {
              _devices = data;
              _isLoading = false;
              _errorMessage = null;
            });
          }
        }, onError: (error) {
          if (mounted) {
            setState(() {
              _isLoading = false;
              _errorMessage = error.toString();
            });
          }
        });
  }

  @override
  void dispose() {
    _devicesSubscription?.cancel();
    _uiTimer?.cancel();
    super.dispose();
  }

  Future<void> _updateRounds({required String deviceName, required bool isIncrement}) async {
    try {
      final response = await Supabase.instance.client
          .from('sessions')
          .select()
          .eq('device_name', deviceName.trim())
          .eq('status', 'active')
          .order('start_time', ascending: false)
          .limit(1);

      if (response.isNotEmpty) {
        final activeSession = response.first;
        final String sessionId = activeSession['id'].toString();
        final int currentRounds = (activeSession['rounds_count'] ?? 1) as int;
        final int newRounds = isIncrement ? currentRounds + 1 : currentRounds - 1;

        if (newRounds < 1) return;

        await Supabase.instance.client
            .from('sessions')
            .update({'rounds_count': newRounds})
            .eq('id', sessionId);

        setState(() {});
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('خطأ أثناء تعديل الجولات: $e'), backgroundColor: Colors.red));
      }
    }
  }
Future<List<Map<String, dynamic>>> _fetchOrdersForDevice(String deviceName) async {
    final sessionResponse = await Supabase.instance.client
        .from('sessions')
        .select('id')
        .eq('device_name', deviceName.trim())
        .eq('status', 'active')
        .order('start_time', ascending: false)
        .limit(1);

    if (sessionResponse.isEmpty) return [];

    final sessionId = sessionResponse.first['id'].toString();

    final ordersResponse = await Supabase.instance.client
        .from('session_orders')
        .select()
        .eq('session_id', sessionId);

    return List<Map<String, dynamic>>.from(ordersResponse);
  }

  void _startSession(String docId, String displayName, String category, double hourlyRate) {
    if (category == 'ps' || category == 'bi') {
      _executeStartSession(docId, displayName, category, hourlyRate, 'rounds');
    } else {
      _executeStartSession(docId, displayName, category, 0.0, 'none');
    }
  }

  Future<void> _executeStartSession(String docId, String displayName, String category, double hourlyRate, String playMode) async {
    try {
      String finalDeviceType = category;
      if (category == 'ps' && playMode == 'rounds') {
        finalDeviceType = 'ps_rounds';
      }

      double effectiveRate = (category == 'ps' && playMode == 'rounds') ? _psRoundPrice : hourlyRate;

      final String nowStr = DateTime.now().toIso8601String();

      await Supabase.instance.client.from('sessions').insert({
        'device_name': displayName,
        'device_type': finalDeviceType,
        'start_time': nowStr,
        'status': 'active',
        'rounds_count': 1,
        'hourly_rate': effectiveRate,
        'play_mode': playMode,
        'round_price': (playMode == 'rounds' && category == 'ps') ? _psRoundPrice : 0.0,
      });

      await Supabase.instance.client.from('devices').update({'status': 'busy'}).eq('id', docId);

      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('تم فتح ($displayName) بنجاح 🚀')));
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('خطأ في الفتح: $e'), backgroundColor: Colors.red));
      }
    }
  }

  void _showOrdersDetailDialog(String tableName, List<Map<String, dynamic>> orders) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: const Color(0xFF161824),
        title: Text('طلبات $tableName', style: const TextStyle(color: Colors.white)),
        content: SizedBox(
          width: double.maxFinite,
          child: orders.isEmpty
              ? const Padding(padding: EdgeInsets.symmetric(vertical: 16), child: Text('لا توجد طلبات بعد', style: TextStyle(color: Colors.grey)))
              : ListView.builder(
                  shrinkWrap: true,
                  itemCount: orders.length,
                  itemBuilder: (context, index) {
                    final o = orders[index];
                    final String name = (o['item_name'] ?? '').toString();
                    final num qty = o['quantity'] ?? 1;
                    final num total = o['total_price'] ?? 0;

                    return ListTile(
                      dense: true,
                      title: Text(name, style: const TextStyle(color: Colors.white)),
                      trailing: Text('×${qty.toStringAsFixed(0)} - ${total.toStringAsFixed(0)} د.ع', style: const TextStyle(color: Color(0xFFFFCC00), fontWeight: FontWeight.bold)),
                    );
                  },
                ),
        ),
        actions: [TextButton(onPressed: () => Navigator.pop(context), child: const Text('إغلاق'))],
      ),
    );
  }

  void _finishSessionDialog(String docId, String tableName) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: const Color(0xFF161824),
        title: Text('إيقاف جلسة $tableName', style: const TextStyle(color: Colors.white)),
        content: const Text('هل تريد إيقاف الجلسة وتحرير الجهاز؟ الفاتورة تبقى بانتظار الكاشير.', style: TextStyle(color: Colors.white70)),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('إلغاء')),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: Colors.redAccent),
            onPressed: () async {
              try {
                await Supabase.instance.client.from('devices').update({'status': 'free'}).eq('id', docId);

                await Supabase.instance.client
                    .from('sessions')
                    .update({
                      'status': 'pending_payment',
                      'end_time': DateTime.now().toIso8601String(),
                    })
                    .eq('device_name', tableName)
                    .eq('status', 'active');
                if (context.mounted) Navigator.pop(context);
              } catch (e) {
                if (context.mounted) {
                  Navigator.pop(context);
                  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('خطأ: $e'), backgroundColor: Colors.red));
                }
              }
            },
            child: const Text('إيقاف وإرسال للكاشير', style: TextStyle(color: Colors.white)),
          ),
        ],
      ),
    );
  }

  void _addNewStationDialog(String categoryId) {
    final TextEditingController nameController = TextEditingController();
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: const Color(0xFF161824),
        title: Text('إضافة عنصر جديد في قسم ($categoryId)', style: const TextStyle(color: Colors.white)),
        content: TextField(
          controller: nameController,
          style: const TextStyle(color: Colors.white),
          decoration: const InputDecoration(hintText: 'اسم الطاولة / الجهاز (مثال: جهاز 1)'),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('إلغاء')),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFF00B894)),
            onPressed: () async {
              if (nameController.text.isNotEmpty) {
                await Supabase.instance.client.from('devices').insert({
                  'name': nameController.text.trim(),
                  'type': categoryId,
                  'status': 'free',
                  'hourly_rate': 2000.0,
                });
                if (context.mounted) Navigator.pop(context);
              }
            },
            child: const Text('إضافة', style: TextStyle(color: Colors.white)),
          ),
        ],
      ),
    );
  }

  void _showOrderBottomSheetWithCart(String docId, String tableName) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: const Color(0xFF161824),
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(20))),
      builder: (context) => OrderCartSheet(
        tableName: tableName,
        printerIp: _baristaPrinterIp,
        onOrderAdded: () {
          setState(() {});
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final String currentCategoryId = categories[_selectedCategoryIndex]['id']!;

    final filteredDevices = _devices.where((data) {
      final String type = (data['type'] ?? '').toString().toLowerCase().trim();
      return type == currentCategoryId;
    }).toList();

    return Scaffold(
      body: Row(
        children: [
          NavigationRail(
            backgroundColor: const Color(0xFF13141F),
            selectedIndex: _selectedCategoryIndex,
            onDestinationSelected: (int index) {
              setState(() {
                _selectedCategoryIndex = index;
              });
            },
            labelType: NavigationRailLabelType.all,
            selectedLabelTextStyle: const TextStyle(color: Color(0xFFFFC048), fontWeight: FontWeight.bold, fontSize: 14),
            unselectedLabelTextStyle: const TextStyle(color: Color(0xFFA0A5C0), fontSize: 12),
            leading: const Padding(
              padding: EdgeInsets.symmetric(vertical: 20),
              child: Column(
                children: [
                  Icon(Icons.stars, color: Color(0xFFFFC048), size: 32),
                  SizedBox(height: 5),
                  Text('كابتن الصالة', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Colors.white)),
                ],
              ),
            ),
            destinations: categories.map((cat) {
              return NavigationRailDestination(
                icon: const Icon(Icons.grid_view_rounded, color: Color(0xFFA0A5C0)),
                selectedIcon: const Icon(Icons.grid_view_rounded, color: Color(0xFFFFC048)),
                label: Text(cat['name']!),
              );
            }).toList(),
          ),
          const VerticalDivider(thickness: 1, width: 1, color: Color(0xFF1F2233)),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        categories[_selectedCategoryIndex]['name']!,
                        style: const TextStyle(fontSize: 24, fontWeight: FontWeight.bold, color: Colors.white),
                      ),
                      Row(
                        children: [
                          IconButton(
                            tooltip: 'الإعدادات العامة',
                            onPressed: _openSettingsDialog,
                            icon: const Icon(Icons.settings, color: Color(0xFFA0A5C0)),
                          ),
                          const SizedBox(width: 8),
                          ElevatedButton.icon(
                            style: ElevatedButton.styleFrom(
                              backgroundColor: const Color(0xFF00B894),
                              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                            ),
                            onPressed: () => _addNewStationDialog(currentCategoryId),
                            icon: const Icon(Icons.add, color: Colors.white),
                            label: const Text('إضافة عنصر جديد', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                          ),
                        ],
                      ),
                    ],
                  ),
                  const SizedBox(height: 20),
                  Expanded(
                    child: _isLoading
                        ? const Center(child: CircularProgressIndicator(color: Color(0xFFFFC048)))
                        : _errorMessage != null
                            ? Center(child: Text('خطأ في الاتصال: $_errorMessage', style: const TextStyle(color: Colors.redAccent)))
                            : filteredDevices.isEmpty
                                ? Center(
                                    child: Column(
                                      mainAxisAlignment: MainAxisAlignment.center,
                                      children: [
                                        const Icon(Icons.inbox_outlined, size: 48, color: Colors.grey),
                                        const SizedBox(height: 10),
                                        const Text('لا توجد عناصر في هذا القسم', style: TextStyle(color: Colors.grey, fontSize: 16)),
                                        const SizedBox(height: 10),
                                        ElevatedButton(
                                          onPressed: () => _addNewStationDialog(currentCategoryId),
                                          child: const Text('اضغط هنا لإضافة عنصر جديد'),
                                        ),
                                      ],
                                    ),
                                  )
                                : GridView.builder(
                                    gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                                      crossAxisCount: 3,
                                      childAspectRatio: 0.82,
                                      crossAxisSpacing: 16,
                                      mainAxisSpacing: 16,
                                    ),
                                    itemCount: filteredDevices.length,
                                    itemBuilder: (context, index) {
                                      return _buildStationCard(filteredDevices[index]);
                                    },
                                  ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildStationCard(Map<String, dynamic> data) {
    final String docId = (data['id'] ?? '').toString();
    final String status = (data['status'] ?? '').toString().toLowerCase().trim();
    final bool isActive = status == 'busy' || status == 'active';
    final String category = (data['type'] ?? '').toString().toLowerCase();
    final String displayName = (data['name'] ?? 'عنصر').toString();
    final double hourlyRate = (data['hourly_rate'] ?? 2000.0).toDouble();

    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFF161824),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: isActive ? const Color(0xFF00B894) : const Color(0xFF23263B),
          width: isActive ? 2 : 1,
        ),
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Text(displayName, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Colors.white), overflow: TextOverflow.ellipsis),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: isActive ? const Color(0xFF00B894).withValues(alpha: 0.2) : Colors.grey.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(6),
                ),
                child: Text(isActive ? 'مشغولة' : 'متاحة', style: TextStyle(color: isActive ? const Color(0xFF00B894) : Colors.grey, fontSize: 12, fontWeight: FontWeight.bold)),
              ),
            ],
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (isActive) ...[
                StreamBuilder<List<Map<String, dynamic>>>(
                  stream: Supabase.instance.client
                      .from('sessions')
                      .stream(primaryKey: ['id'])
                      .eq('device_name', displayName.trim())
                      .order('start_time', ascending: false)
                      .limit(1),
                  builder: (context, snapshot) {
                    if (!snapshot.hasData || snapshot.data!.isEmpty) {
                      return const Text('الجلسة قائمة الآن 🟢', style: TextStyle(color: Color(0xFFA0A5C0), fontSize: 13));
                    }

                    final activeSessions = snapshot.data!.where((s) => s['status'] == 'active').toList();
                    if (activeSessions.isEmpty) {
                      return const Text('الجلسة قائمة الآن 🟢', style: TextStyle(color: Color(0xFFA0A5C0), fontSize: 13));
                    }

                    final sessionData = activeSessions.first;

                    return Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        if (category == 'ps' || category == 'bi') ...[
                          const Text('نظام اللعب: جولات/جيمات 🎮', style: TextStyle(color: Color(0xFF0984E3), fontSize: 12, fontWeight: FontWeight.bold)),
                          const SizedBox(height: 6),
                          Builder(
                            builder: (context) {
                              int rounds = sessionData['rounds_count'] ?? 1;
                              return Container(
                                padding: const EdgeInsets.symmetric(vertical: 2, horizontal: 4),
                                decoration: BoxDecoration(color: const Color(0xFF23263B), borderRadius: BorderRadius.circular(8)),
                                child: Row(
                                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                  children: [
                                    InkWell(
                                      onTap: () => _updateRounds(deviceName: displayName, isIncrement: false),
                                      child: const Padding(padding: EdgeInsets.all(4), child: Icon(Icons.remove_circle_outline, color: Colors.redAccent, size: 22)),
                                    ),
                                    Text('$rounds جولة/جيم', style: const TextStyle(color: Color(0xFFFFC048), fontWeight: FontWeight.bold, fontSize: 13)),
                                    InkWell(
                                      onTap: () => _updateRounds(deviceName: displayName, isIncrement: true),
                                      child: const Padding(padding: EdgeInsets.all(4), child: Icon(Icons.add_circle_outline, color: Color(0xFF00B894), size: 22)),
                                    ),
                                  ],
                                ),
                              );
                            },
                          ),
                        ] else if (category == 'tab') ...[
                          const Text('طاولة جلوس (بدون رسوم لعب) ☕', style: TextStyle(color: Color(0xFF00B894), fontSize: 12, fontWeight: FontWeight.bold)),
                        ],
                      ],
                    );
                  },
                ),
                const SizedBox(height: 8),
                FutureBuilder<List<Map<String, dynamic>>>(
                  future: _fetchOrdersForDevice(displayName),
                  builder: (context, snapshot) {
                    if (!snapshot.hasData) return const SizedBox.shrink();

                    final orders = snapshot.data!;
                    if (orders.isEmpty) {
                      return const Text('🧾 لا توجد طلبات بعد', style: TextStyle(color: Colors.grey, fontSize: 11));
                    }

                    final double total = orders.fold(0, (sum, o) => sum + (o['total_price'] ?? 0).toDouble());

                    return InkWell(
                      onTap: () => _showOrdersDetailDialog(displayName, orders),
                      child: Container(
                        padding: const EdgeInsets.all(8),
                        decoration: BoxDecoration(
                          color: const Color(0xFF1F2233),
                          borderRadius: BorderRadius.circular(8),
                          border: Border.all(color: const Color(0xFF2C2D3C)),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            ...orders.take(3).map((o) {
                              final String name = (o['item_name'] ?? '').toString();
                              final num qty = o['quantity'] ?? 1;
                              return Padding(
                                padding: const EdgeInsets.symmetric(vertical: 1.5),
                                child: Row(
                                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                  children: [
                                    Expanded(child: Text('• $name', style: const TextStyle(color: Colors.white70, fontSize: 11), overflow: TextOverflow.ellipsis)),
                                    Text('×${qty.toStringAsFixed(0)}', style: const TextStyle(color: Color(0xFFFFC048), fontSize: 11, fontWeight: FontWeight.bold)),
                                  ],
                                ),
                              );
                            }),
                            if (orders.length > 3)
                              Padding(
                                padding: const EdgeInsets.only(top: 2),
                                child: Text('+ ${orders.length - 3} طلبات أخرى...', style: const TextStyle(color: Colors.grey, fontSize: 10, fontStyle: FontStyle.italic)),
                              ),
                            const Divider(color: Color(0xFF2C2D3C), height: 10, thickness: 0.5),
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                const Text('المجموع:', style: TextStyle(color: Color(0xFFA0A5C0), fontSize: 11, fontWeight: FontWeight.bold)),
                                Text('${total.toStringAsFixed(0)} د.ع', style: const TextStyle(color: Color(0xFF00B894), fontSize: 11, fontWeight: FontWeight.bold)),
                              ],
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                ),
              ] else
                const Center(
                  child: Padding(
                    padding: EdgeInsets.symmetric(vertical: 10),
                    child: Text('جاهزة للاستخدام', style: TextStyle(color: Colors.grey)),
                  ),
                ),
            ],
          ),
          Row(
            children: [
              if (!isActive)
                Expanded(
                  child: ElevatedButton(
                    style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFF6C5CE7)),
                    onPressed: () => _startSession(docId, displayName, category, hourlyRate),
                    child: const Text('بدء الجلسة', style: TextStyle(color: Colors.white)),
                  ),
                )
              else ...[
                Expanded(
                  child: ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFFFFC048)),
                    onPressed: () => _showOrderBottomSheetWithCart(docId, displayName),
                    icon: const Icon(Icons.shopping_cart, color: Colors.black, size: 18),
                    label: const Text('طلب جديد', style: TextStyle(color: Colors.black, fontWeight: FontWeight.bold)),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton(
                  icon: const Icon(Icons.stop_circle, color: Colors.redAccent),
                  onPressed: () => _finishSessionDialog(docId, displayName),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

// ==========================================
// ✅ شاشة الطلبات وعملية الطباعة النصية المباشرة (Direct ESC/POS)
// ==========================================
class OrderCartSheet extends StatefulWidget {
  final String tableName;
  final String printerIp;
  final VoidCallback onOrderAdded;

  const OrderCartSheet({
    super.key,
    required this.tableName,
    required this.printerIp,
    required this.onOrderAdded,
  });

  @override
  State<OrderCartSheet> createState() => _OrderCartSheetState();
}

class _OrderCartSheetState extends State<OrderCartSheet> {
  List<Map<String, dynamic>> menuItems = [];
  List<String> _categories = [];
  Map<String, int> cart = {};
  bool _isProcessing = false;
final GlobalKey _receiptKey = GlobalKey();
  @override
  void initState() {
    super.initState();
    _fetchMenu();
  }

 Future<void> _fetchMenu() async {
    try {
      final response = await Supabase.instance.client.from('products').select();
      if (mounted) {
        setState(() {
          menuItems = List<Map<String, dynamic>>.from(response);
          _categories = menuItems
              .map((e) => (e['category'] ?? 'عام').toString().trim())
              .where((c) => c.isNotEmpty)
              .toSet()
              .toList();
          if (_categories.isEmpty) _categories = ['عام'];
        });
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('فشل تحميل المنيو: $e'), backgroundColor: Colors.red),
        );
      }
    }
  }

 Future<void> _submitOrderAndPrint() async {
    if (cart.isEmpty) return;
    setState(() => _isProcessing = true);

    try {
      final sessionResponse = await Supabase.instance.client
          .from('sessions')
          .select('id')
          .eq('device_name', widget.tableName.trim())
          .eq('status', 'active')
          .order('start_time', ascending: false)
          .limit(1)
          .single();

      final sessionId = sessionResponse['id'];

      List<Map<String, dynamic>> orderInserts = [];
      for (var entry in cart.entries) {
        final item = menuItems.firstWhere((e) => e['id'].toString() == entry.key);
        final String itemName = (item['name'] ?? item['Name'] ?? 'عنصر').toString();
        final double itemPrice = (item['selling_price'] ?? item['SellingPrice'] ?? item['price'] ?? 0.0).toDouble();

        orderInserts.add({
          'session_id': sessionId,
          'inventory_id': item['id'],
          'item_name': itemName,
          'quantity': entry.value,
          'unit_price': itemPrice,
          'total_price': itemPrice * entry.value,
        });
      }

      await Supabase.instance.client.from('session_orders').insert(orderInserts);

      if (mounted) {
        widget.onOrderAdded();
      }

      // ✅ استدعاء دالة طباعة الصورة المحدثة
      bool printSuccess = await _printReceiptImageAsync();

      if (mounted) {
        setState(() => _isProcessing = false);
        if (printSuccess) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم حفظ الطلبات وطباعتها بنجاح ✅'), backgroundColor: Colors.green),
          );
          Navigator.pop(context);
        } else {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم حفظ الطلب بكتيب المبيعات، لكن تعذر الاتصال بالطابعة.'), backgroundColor: Colors.orange),
          );
          Navigator.pop(context);
        }
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isProcessing = false);
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('خطأ في حفظ الطلب: $e'), backgroundColor: Colors.red));
      }
    }
  }

  // 🖨️ دالة طباعة الصورة مع معالجة خطأ الشفافية
  Future<bool> _printReceiptImageAsync() async {
    final String cleanIp = widget.printerIp.trim();
    if (cleanIp.isEmpty) return false;

    try {
      // ✅ إعطاء المهلة للرسم
      await Future.delayed(const Duration(milliseconds: 300));

      final renderObject = _receiptKey.currentContext?.findRenderObject();
      if (renderObject == null) return false;

      final boundary = renderObject as RenderRepaintBoundary;
      final ui.Image image = await boundary.toImage(pixelRatio: 2.0);
      final ByteData? byteData = await image.toByteData(format: ui.ImageByteFormat.png);
      
      if (byteData == null) return false;

      final Uint8List pngBytes = byteData.buffer.asUint8List();
      final img.Image? decodedImage = img.decodeImage(pngBytes);
      if (decodedImage == null) return false;

      final img.Image resizedImage = img.copyResize(decodedImage, width: 576);

      List<int> bytes = [];
      bytes.addAll([0x1B, 0x40]); // Reset printer

      // تحويل الصورة إلى بايتات حرارية Monochromatic
      for (int y = 0; y < resizedImage.height; y += 24) {
        bytes.addAll([0x1B, 0x2A, 33, (resizedImage.width % 256), (resizedImage.width ~/ 256)]);
        for (int x = 0; x < resizedImage.width; x++) {
          for (int k = 0; k < 3; k++) {
            int slice = 0;
            for (int b = 0; b < 8; b++) {
              int yy = y + k * 8 + b;
              if (yy < resizedImage.height) {
                final pixel = resizedImage.getPixel(x, yy);
                if (pixel.r < 128 || pixel.g < 128 || pixel.b < 128) {
                  slice |= (0x80 >> b);
                }
              }
            }
            bytes.add(slice);
          }
        }
        bytes.addAll([0x1B, 0x4A, 0x00]);
      }

      bytes.addAll('\n\n\n'.codeUnits);
      bytes.addAll([0x1D, 0x56, 0x42, 0x00]); // Cut paper

      final socket = await Socket.connect(cleanIp, 9100, timeout: const Duration(seconds: 4));
      socket.add(Uint8List.fromList(bytes));
      await socket.flush();
      await socket.close();

      return true;
    } catch (e) {
      debugPrint('🛑 خطأ في طباعة الصورة: $e');
      return false;
    }
  }

  /// 🖨️ طباعة نصية مباشرة بدعم أجهزة ESC/POS (تتفادى مشاكل RenderBoundary نهائياً)
  Future<bool> _directPrintToPrinter() async {
    final String cleanIp = widget.printerIp.trim();
    if (cleanIp.isEmpty) return false;

    try {
      final socket = await Socket.connect(cleanIp, 9100, timeout: const Duration(seconds: 4));
      
      List<int> bytes = [];

      // تهيئة الطابعة وترميز النواة
      bytes.addAll([0x1B, 0x40]); // Reset Printer
      bytes.addAll([0x1B, 0x74, 22]); // Arabic Code Page (CP864)

      // الهيدر / العنوان
      bytes.addAll([0x1B, 0x61, 0x01]); // Align Center
      bytes.addAll([0x1B, 0x21, 0x30]); // Double height & width
      bytes.addAll('HANASH GAME CENTER\n'.codeUnits);
      bytes.addAll([0x1B, 0x21, 0x00]); // Normal text
      bytes.addAll('--------------------------------\n'.codeUnits);
      
      // تفاصيل الطاولة والوقت
      bytes.addAll([0x1B, 0x61, 0x00]); // Align Left
      bytes.addAll('Table: ${widget.tableName}\n'.codeUnits);
      final now = DateTime.now();
      bytes.addAll('Time: ${now.hour}:${now.minute} - ${now.day}/${now.month}/${now.year}\n'.codeUnits);
      bytes.addAll('--------------------------------\n'.codeUnits);

      // المواد المطلوبة
      for (var entry in cart.entries) {
        final item = menuItems.firstWhere((e) => e['id'].toString() == entry.key, orElse: () => {'name': 'Item'});
        final String itemName = (item['name'] ?? item['Name'] ?? 'Item').toString();
        bytes.addAll('${entry.value} x $itemName\n'.codeUnits);
      }

      bytes.addAll('--------------------------------\n'.codeUnits);
      bytes.addAll([0x1B, 0x61, 0x01]); // Align Center
      bytes.addAll('Kitchen Order Receipt\n\n\n\n'.codeUnits);

      // قص الورقة
      bytes.addAll([0x1D, 0x56, 0x42, 0x00]);

      socket.add(Uint8List.fromList(bytes));
      await socket.flush();
      await socket.close();

      return true;
    } catch (e) {
      debugPrint('🛑 خطأ في الطباعة المباشرة: $e');
      return false;
    }
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: MediaQuery.of(context).size.height * 0.85,
      child: Stack(
        children: [
          // ==========================================
          // 1. تصميم الفاتورة العربي (موجود خلف الواجهة ليتم تصويره)
          // ==========================================
          Positioned(
            top: 0,
            left: 0,
            child: RepaintBoundary(
              key: _receiptKey,
              child: Container(
                width: 576,
                color: Colors.white,
                padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 20),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: [
                    const Text('🎮 صالة حنش للألعاب 🎮', textAlign: TextAlign.center, style: TextStyle(fontSize: 32, fontWeight: FontWeight.bold, color: Colors.black)),
                    const SizedBox(height: 6),
                    const Text('وصل طلبات البارستا', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w600, color: Colors.black87)),
                    const SizedBox(height: 10),
                    const Text('================================', maxLines: 1, style: TextStyle(color: Colors.black, fontWeight: FontWeight.bold, fontSize: 16)),
                    const SizedBox(height: 8),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text('الطاولة: ${widget.tableName}', style: const TextStyle(fontSize: 24, fontWeight: FontWeight.bold, color: Colors.black)),
                        Text('الوقت: ${DateTime.now().hour.toString().padLeft(2, '0')}:${DateTime.now().minute.toString().padLeft(2, '0')}', style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold, color: Colors.black)),
                      ],
                    ),
                    const SizedBox(height: 8),
                    const Text('------------------------------------------------', maxLines: 1, style: TextStyle(color: Colors.black, fontSize: 14)),
                    const SizedBox(height: 8),
                    const Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text('المادة / المشروب', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Colors.black)),
                        Text('العدد', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Colors.black)),
                      ],
                    ),
                    const SizedBox(height: 6),
                    ...cart.entries.map((entry) {
                      final item = menuItems.firstWhere((e) => e['id'].toString() == entry.key, orElse: () => {'name': 'عنصر'});
                      final String itemName = (item['name'] ?? item['Name'] ?? 'عنصر').toString();
                      return Padding(
                        padding: const EdgeInsets.symmetric(vertical: 4),
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Expanded(child: Text(itemName, style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w600, color: Colors.black))),
                            Text('${entry.value}', style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Colors.black)),
                          ],
                        ),
                      );
                    }),
                    const SizedBox(height: 10),
                    const Text('------------------------------------------------', maxLines: 1, style: TextStyle(color: Colors.black, fontSize: 14)),
                    const SizedBox(height: 12),
                    const Text('يرجى تسليم الطلب للطاولة مباشرة', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Colors.black)),
                    const SizedBox(height: 10),
                  ],
                ),
              ),
            ),
          ),

          // ==========================================
          // 2. الواجهة الظاهرة للمستخدم
          // ==========================================
          Positioned.fill(
            child: Container(
              padding: const EdgeInsets.all(20),
              decoration: const BoxDecoration(
                color: Color(0xFF161824),
                borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
              ),
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('طلبات: ${widget.tableName}', style: const TextStyle(fontSize: 20, color: Colors.white, fontWeight: FontWeight.bold)),
                      IconButton(icon: const Icon(Icons.close, color: Colors.white), onPressed: () => Navigator.pop(context))
                    ],
                  ),
                  const Divider(color: Color(0xFF2C2D3C)),
                  const SizedBox(height: 10),
                  
                  Expanded(
                    child: _categories.isEmpty
                        ? const Center(child: CircularProgressIndicator(color: Color(0xFFFFC048)))
                        : DefaultTabController(
                            length: _categories.length,
                            child: Column(
                              children: [
                                TabBar(
                                  isScrollable: true,
                                  indicatorColor: const Color(0xFF00B894),
                                  labelColor: const Color(0xFFFFC048),
                                  unselectedLabelColor: Colors.grey,
                                  tabs: _categories.map((cat) => Tab(text: cat)).toList(),
                                ),
                                const SizedBox(height: 10),
                                Expanded(
                                  child: TabBarView(
                                    children: _categories.map((category) {
                                      final categoryItems = menuItems.where((e) => (e['category'] ?? 'عام').toString().trim() == category).toList();
                                      
                                      if (categoryItems.isEmpty) {
                                        return const Center(child: Text('لا توجد عناصر في هذا القسم', style: TextStyle(color: Colors.grey)));
                                      }

                                      return ListView.builder(
                                        itemCount: categoryItems.length,
                                        itemBuilder: (context, index) {
                                          final item = categoryItems[index];
                                          final id = item['id'].toString();
                                          final String itemName = (item['name'] ?? item['Name'] ?? 'عنصر').toString();
                                          final double itemPrice = (item['selling_price'] ?? item['SellingPrice'] ?? item['price'] ?? 0.0).toDouble();
                                          final int currentQty = cart[id] ?? 0;

                                          return Card(
                                            color: const Color(0xFF1F2233),
                                            margin: const EdgeInsets.symmetric(vertical: 5),
                                            child: ListTile(
                                              title: Text(itemName, style: const TextStyle(color: Colors.white)),
                                              subtitle: Text('${itemPrice.toStringAsFixed(0)} د.ع', style: const TextStyle(color: Color(0xFFFFC048))),
                                              trailing: Row(
                                                mainAxisSize: MainAxisSize.min,
                                                children: [
                                                  IconButton(
                                                    icon: const Icon(Icons.remove_circle_outline, color: Colors.redAccent),
                                                    onPressed: () {
                                                      if (currentQty > 0) {
                                                        setState(() {
                                                          cart[id] = currentQty - 1;
                                                          if (cart[id] == 0) cart.remove(id);
                                                        });
                                                      }
                                                    },
                                                  ),
                                                  Text('$currentQty', style: const TextStyle(fontSize: 16, color: Colors.white, fontWeight: FontWeight.bold)),
                                                  IconButton(
                                                    icon: const Icon(Icons.add_circle_outline, color: Color(0xFF00B894)),
                                                    onPressed: () {
                                                      setState(() {
                                                        cart[id] = currentQty + 1;
                                                      });
                                                    },
                                                  ),
                                                ],
                                              ),
                                            ),
                                          );
                                        },
                                      );
                                    }).toList(),
                                  ),
                                ),
                              ],
                            ),
                          ),
                  ),

                  ElevatedButton(
                    style: ElevatedButton.styleFrom(
                      minimumSize: const Size(double.infinity, 50),
                      backgroundColor: const Color(0xFF00B894),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    onPressed: _isProcessing || cart.isEmpty ? null : _submitOrderAndPrint,
                    child: _isProcessing
                        ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
                        : const Text('حفظ وإرسال للبارستا 🚀', style: TextStyle(fontSize: 18, color: Colors.white, fontWeight: FontWeight.bold)),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}