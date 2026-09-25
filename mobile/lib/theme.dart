import 'package:flutter/material.dart';

const brand = Color(0xFF5B4BFF);
const brand2 = Color(0xFF9B5BFF);
const brandGradient = LinearGradient(
  colors: [brand, brand2],
  begin: Alignment.topLeft,
  end: Alignment.bottomRight,
);

ThemeData buildTheme() {
  final scheme = ColorScheme.fromSeed(seedColor: brand, primary: brand);
  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: const Color(0xFFF6F7FB),
    cardTheme: CardThemeData(
      color: Colors.white,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(18),
        side: const BorderSide(color: Color(0xFFE6E8F0)),
      ),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: brand,
        minimumSize: const Size(0, 46),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      ),
    ),
  );
}

/// 49999 -> "49.999 ₺"
String tl(num n) {
  final s = n.round().toString();
  final b = StringBuffer();
  for (var i = 0; i < s.length; i++) {
    if (i > 0 && (s.length - i) % 3 == 0) b.write('.');
    b.write(s[i]);
  }
  return '$b ₺';
}

String emojiFor(String category) => switch (category) {
  'Laptop' => '💻',
  'Telefon' => '📱',
  'Kulaklık' => '🎧',
  'Saat' => '⌚',
  'Tablet' => '📲',
  'Televizyon' => '📺',
  'Hoparlör' => '🔊',
  'Kamera' => '📷',
  'Oyun Konsolu' => '🎮',
  'Aksesuar' => '🔌',
  _ => '🛍️',
};

List<Color> gradientFor(String category) => switch (category) {
  'Laptop' => const [Color(0xFFDFE4FF), Color(0xFFB9C4FF)],
  'Telefon' => const [Color(0xFFFFE0EC), Color(0xFFFFBDD6)],
  'Kulaklık' => const [Color(0xFFD6F5EC), Color(0xFFA8E6D2)],
  'Saat' => const [Color(0xFFFFF0D0), Color(0xFFFFD88F)],
  'Tablet' => const [Color(0xFFE6DCFF), Color(0xFFCBB8FF)],
  'Televizyon' => const [Color(0xFFD9E8FF), Color(0xFFA9C8F5)],
  'Hoparlör' => const [Color(0xFFFFE3D4), Color(0xFFFFC3A3)],
  'Kamera' => const [Color(0xFFE3E3EA), Color(0xFFC2C3D1)],
  'Oyun Konsolu' => const [Color(0xFFE0FFD9), Color(0xFFB5EAA9)],
  _ => const [Color(0xFFE8EAF3), Color(0xFFCFD3E6)],
};
