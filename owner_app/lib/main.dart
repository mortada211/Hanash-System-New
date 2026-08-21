import 'package:flutter/material.dart';
import 'package:supabase_flutter/supabase_flutter.dart';
import 'screens/main_navigation_screen.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  await Supabase.initialize(
    url: 'https://iihcevuyrdoezfozhots.supabase.co',
    anonKey: 'sb_publishable_jGD6jV7XZr2H8oXVKxxP4Q_IBI6eNwE',
  );

  runApp(const OwnerApp());
}

final supabase = Supabase.instance.client;

class OwnerApp extends StatelessWidget {
  const OwnerApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: 'إدارة الصالة - صاحب المال',
      theme: ThemeData.dark(),
      // 💡 دعم اللغة العربية وتوجيه النص من اليمين لليسار
      builder: (context, child) {
        return Directionality(
          textDirection: TextDirection.rtl,
          child: child!,
        );
      },
      home: const MainNavigationScreen(),
    );
  }
}