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
    this.iconSize = 56,
  });

  final Product product;
  final double height;
  final double iconSize;

  @override
  Widget build(BuildContext context) {
    final fallback = Icon(
      iconFor(product.category),
      size: iconSize,
      color: ink.withValues(alpha: .35),
    );
    return Container(
      height: height,
      width: double.infinity,
      alignment: Alignment.center,
      color: visualBg,
      child: product.imageUrl == null
          ? fallback
          : Padding(
              padding: EdgeInsets.all(height * 0.1),
              child: Image.network(
                product.imageUrl!,
                fit: BoxFit.contain,
                // Beyaz ürün fotoğrafı zemini krem arka plana karışsın.
                color: visualBg,
                colorBlendMode: BlendMode.multiply,
                errorBuilder: (_, _, _) => fallback,
                frameBuilder: (_, child, frame, _) => AnimatedOpacity(
                  opacity: frame == null ? 0 : 1,
                  duration: const Duration(milliseconds: 250),
                  child: child,
                ),
              ),
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
        ? ('Tükendi', const Color(0xFFF8E1DA), bad)
        : stock <= 10
        ? ('Son $stock adet', const Color(0xFFF8ECD3), const Color(0xFF8A5300))
        : ('Stokta', surface, ink);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 11.5,
          fontWeight: FontWeight.w600,
          color: fg,
        ),
      ),
    );
  }
}

class RatingLabel extends StatelessWidget {
  const RatingLabel(this.product, {super.key});
  final Product product;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(Icons.star_rounded, size: 17, color: star),
        const SizedBox(width: 2),
        Text(
          '${product.rating!.toStringAsFixed(1)}'
          '${product.ratingCount > 0 ? ' (${product.ratingCount})' : ''}',
          style: const TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: muted,
          ),
        ),
      ],
    );
  }
}

class ProductCard extends StatelessWidget {
  const ProductCard({super.key, required this.product});

  final Product product;

  @override
  Widget build(BuildContext context) {
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
            Padding(
              padding: const EdgeInsets.fromLTRB(8, 8, 8, 0),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(12),
                child: Stack(
                  children: [
                    ProductVisual(product: p, height: 190),
                    Positioned(top: 10, left: 10, child: StockTag(p.stock)),
                  ],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 14, 16, 16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '${p.brand} · ${p.category}'.toUpperCase(),
                    style: const TextStyle(
                      fontSize: 11,
                      color: muted,
                      fontWeight: FontWeight.w600,
                      letterSpacing: .8,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    p.name,
                    style: const TextStyle(
                      fontSize: 16,
                      height: 1.3,
                      fontWeight: FontWeight.w600,
                      color: ink,
                    ),
                  ),
                  if (p.rating != null) ...[
                    const SizedBox(height: 4),
                    RatingLabel(p),
                  ],
                  const SizedBox(height: 4),
                  Text(
                    p.description,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 13.5,
                      color: muted,
                      height: 1.4,
                    ),
                  ),
                  const SizedBox(height: 12),
                  const DashedDivider(),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(child: Text(tl(p.price), style: serif(26))),
                      OutlinedButton(
                        onPressed: p.stock <= 0
                            ? null
                            : () => addToCart(context, p),
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

/// Kartlardaki kesik çizgili ayraç.
class DashedDivider extends StatelessWidget {
  const DashedDivider({super.key});

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (_, c) {
        final n = (c.maxWidth / 8).floor();
        return Row(
          children: List.generate(
            n,
            (_) => Expanded(
              child: Container(
                height: 1,
                margin: const EdgeInsets.symmetric(horizontal: 2),
                color: line,
              ),
            ),
          ),
        );
      },
    );
  }
}
