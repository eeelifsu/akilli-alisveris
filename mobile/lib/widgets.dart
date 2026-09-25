import 'package:flutter/material.dart';

import 'cart.dart';
import 'models/product.dart';
import 'product_detail_page.dart';
import 'theme.dart';

void addToCart(BuildContext context, Product p) {
  final ok = cart.add(p);
  final messenger = ScaffoldMessenger.of(context);
  messenger.hideCurrentSnackBar();
  messenger.showSnackBar(
    SnackBar(
      behavior: SnackBarBehavior.floating,
      duration: const Duration(seconds: 2),
      content: Text(
        ok ? '${p.name} sepete eklendi' : 'Bu üründen daha fazla eklenemez',
      ),
    ),
  );
}

class ProductVisual extends StatelessWidget {
  const ProductVisual({
    super.key,
    required this.product,
    this.height = 140,
    this.emojiSize = 60,
  });

  final Product product;
  final double height;
  final double emojiSize;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: height,
      width: double.infinity,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: gradientFor(product.category),
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
      ),
      child: Text(
        emojiFor(product.category),
        style: TextStyle(fontSize: emojiSize),
      ),
    );
  }
}

class StockTag extends StatelessWidget {
  const StockTag(this.stock, {super.key});
  final int stock;

  @override
  Widget build(BuildContext context) {
    final (label, bg, fg) = stock <= 0
        ? ('Tükendi', const Color(0xFFFFE5E5), const Color(0xFFD43B3B))
        : stock <= 10
        ? ('Son $stock adet', const Color(0xFFFFF1D6), const Color(0xFF9A5B00))
        : ('Stokta', Colors.white, const Color(0xFF14172B));
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: fg),
      ),
    );
  }
}

class ProductCard extends StatelessWidget {
  const ProductCard({super.key, required this.product});

  final Product product;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final p = product;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute(builder: (_) => ProductDetailPage(product: p)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Stack(
              children: [
                ProductVisual(product: p),
                Positioned(top: 12, left: 12, child: StockTag(p.stock)),
              ],
            ),
            Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '${p.brand} · ${p.category}'.toUpperCase(),
                    style: theme.textTheme.labelSmall?.copyWith(
                      color: Colors.black54,
                      fontWeight: FontWeight.w700,
                      letterSpacing: .6,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    p.name,
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    p.description,
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: Colors.black54,
                    ),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          tl(p.price),
                          style: theme.textTheme.titleLarge?.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      FilledButton.tonal(
                        onPressed: p.stock <= 0
                            ? null
                            : () => addToCart(context, p),
                        style: FilledButton.styleFrom(
                          minimumSize: const Size(0, 40),
                          backgroundColor: const Color(0xFFEEECFF),
                          foregroundColor: brand,
                        ),
                        child: Text(p.stock <= 0 ? 'Tükendi' : 'Sepete ekle'),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
