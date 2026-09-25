import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

// Web sitesiyle aynı palet: krem kâğıt, mürekkep siyahı, tek vurgu (limon yeşili).
const bg = Color(0xFFF4F1EA);
const bg2 = Color(0xFFEBE6DB);
const surface = Color(0xFFFFFDF8);
const ink = Color(0xFF171512);
const muted = Color(0xFF6B655B);
const line = Color(0xFFE2DBCD);
const lime = Color(0xFFC6EE5A);
const visualBg = Color(0xFFECE7DC);
const ok = Color(0xFF2F7A3A);
const bad = Color(0xFFB8402B);
const star = Color(0xFFC98A00);

/// Başlıklar için serif yazı (iOS'ta Georgia, Android'de sistem serif).
TextStyle serif(
  double size, {
  Color color = ink,
  FontStyle? style,
  double height = 1.05,
}) => TextStyle(
  fontFamily: 'Georgia',
  fontFamilyFallback: const ['serif'],
  fontSize: size,
  fontStyle: style,
  fontWeight: FontWeight.w400,
  height: height,
  color: color,
);

ThemeData buildTheme() {
  final scheme = ColorScheme.fromSeed(
    seedColor: lime,
    primary: ink,
    onPrimary: bg,
    secondary: lime,
    onSecondary: ink,
    surface: surface,
    onSurface: ink,
    error: bad,
  );
  final pill = RoundedRectangleBorder(borderRadius: BorderRadius.circular(999));
  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: bg,
    splashFactory: InkSparkle.splashFactory,
    appBarTheme: AppBarTheme(
      backgroundColor: bg,
      foregroundColor: ink,
      surfaceTintColor: Colors.transparent,
      elevation: 0,
      scrolledUnderElevation: 0,
      systemOverlayStyle: SystemUiOverlayStyle.dark,
      centerTitle: false,
      titleTextStyle: serif(26),
      shape: const Border(bottom: BorderSide(color: line)),
    ),
    cardTheme: CardThemeData(
      color: surface,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(18),
        side: const BorderSide(color: line),
      ),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: ink,
        foregroundColor: bg,
        disabledBackgroundColor: line,
        minimumSize: const Size(0, 50),
        textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15.5),
        shape: pill,
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: ink,
        side: const BorderSide(color: ink),
        minimumSize: const Size(0, 38),
        padding: const EdgeInsets.symmetric(horizontal: 16),
        textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13.5),
        shape: pill,
      ),
    ),
    chipTheme: ChipThemeData(
      backgroundColor: Colors.transparent,
      selectedColor: ink,
      side: const BorderSide(color: line),
      shape: pill,
      labelStyle: const TextStyle(
        color: ink,
        fontWeight: FontWeight.w500,
        fontSize: 13.5,
      ),
      secondaryLabelStyle: const TextStyle(
        color: bg,
        fontWeight: FontWeight.w500,
      ),
    ),
    snackBarTheme: SnackBarThemeData(
      backgroundColor: ink,
      contentTextStyle: const TextStyle(color: bg, fontSize: 14),
      shape: pill,
    ),
    progressIndicatorTheme: const ProgressIndicatorThemeData(color: ink),
    popupMenuTheme: PopupMenuThemeData(
      color: surface,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: const BorderSide(color: line),
      ),
    ),
    textSelectionTheme: const TextSelectionThemeData(cursorColor: ink),
  );
}

/// Yuvarlak, çizgili arama/mesaj kutusu kenarı.
InputDecoration pillInput({
  required String hint,
  Widget? prefix,
  Widget? suffix,
}) => InputDecoration(
  hintText: hint,
  hintStyle: const TextStyle(color: muted),
  prefixIcon: prefix,
  suffixIcon: suffix,
  filled: true,
  fillColor: surface,
  isDense: true,
  contentPadding: const EdgeInsets.symmetric(horizontal: 18, vertical: 15),
  enabledBorder: OutlineInputBorder(
    borderRadius: BorderRadius.circular(999),
    borderSide: const BorderSide(color: line),
  ),
  focusedBorder: OutlineInputBorder(
    borderRadius: BorderRadius.circular(999),
    borderSide: const BorderSide(color: ink, width: 1.4),
  ),
);

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

IconData iconFor(String category) => switch (category) {
  'Laptop' => Icons.laptop_mac_outlined,
  'Telefon' => Icons.smartphone_outlined,
  'Kulaklık' => Icons.headphones_outlined,
  'Saat' => Icons.watch_outlined,
  'Tablet' => Icons.tablet_mac_outlined,
  'Televizyon' => Icons.tv_outlined,
  'Hoparlör' => Icons.speaker_outlined,
  'Kamera' => Icons.photo_camera_outlined,
  'Oyun Konsolu' => Icons.sports_esports_outlined,
  'Aksesuar' => Icons.power_outlined,
  _ => Icons.shopping_bag_outlined,
};
