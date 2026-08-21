import 'package:flutter/material.dart';
import 'dashboard_screen.dart';
import 'inventory_screen.dart';
import 'shifts_screen.dart';
import 'reports_screen.dart';

class MainNavigationScreen extends StatefulWidget {
  const MainNavigationScreen({super.key});

  @override
  State<MainNavigationScreen> createState() => _MainNavigationScreenState();
}

class _MainNavigationScreenState extends State<MainNavigationScreen> {
  int _selectedIndex = 0;

  // قائمة الشاشات الأربعة بدون const لتجنب الخطأ
  final List<Widget> _screens = [
    const DashboardScreen(),
    const InventoryScreen(),
    const ShiftsScreen(),
    const ReportsScreen(),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: _selectedIndex,
        children: _screens,
      ),
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: _selectedIndex,
        onTap: (index) {
          setState(() {
            _selectedIndex = index;
          });
        },
        type: BottomNavigationBarType.fixed,
        backgroundColor: Colors.grey[900],
        selectedItemColor: Colors.amber,
        unselectedItemColor: Colors.grey,
        items: const [
          BottomNavigationBarItem(
            icon: Icon(Icons.dashboard),
            label: 'الرئيسية',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.inventory_2),
            label: 'المخزن',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.badge),
            label: 'الشفتات',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.analytics),
            label: 'المبيعات',
          ),
        ],
      ),
    );
  }
}