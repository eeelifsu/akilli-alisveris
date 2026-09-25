import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/main.dart';

void main() {
  testWidgets('uygulama açılır', (tester) async {
    await tester.pumpWidget(const MyApp());
    expect(find.text('Ürünler'), findsOneWidget);
  });
}
