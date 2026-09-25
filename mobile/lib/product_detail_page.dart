import 'package:flutter/material.dart';

import 'models/product.dart';
import 'theme.dart';
import 'widgets.dart';

class ProductDetailPage extends StatelessWidget {
  const ProductDetailPage({super.key, required this.product});

  final Product product;

  @override
  Widget build(BuildContext context) {
    final p = product;
    final inStock = p.stock > 0;
    return Scaffold(
      body: CustomScrollView(
        slivers: [
          SliverAppBar(
            pinned: true,
            expandedHeight: 300,
            backgroundColor: visualBg,
            flexibleSpace: FlexibleSpaceBar(
              background: ProductVisual(product: p, height: 300, iconSize: 96),
            ),
          ),
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      _Tag(p.brand),
                      _Tag(p.category),
                      _Tag(
                        inStock ? 'Stokta: ${p.stock}' : 'Stokta yok',
                        bg: inStock
                            ? const Color(0xFFE3F1DC)
                            : const Color(0xFFF8E1DA),
                        fg: inStock ? ok : bad,
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Text(p.name, style: serif(30, height: 1.1)),
                  if (p.rating != null) ...[
                    const SizedBox(height: 6),
                    RatingLabel(p),
                  ],
                  const SizedBox(height: 8),
                  const SizedBox(height: 6),
                  Text(tl(p.price), style: serif(42)),
                  const SizedBox(height: 20),
                  const _Label('Açıklama'),
                  const SizedBox(height: 4),
                  Text(
                    p.description,
                    style: const TextStyle(
                      fontSize: 15,
                      color: muted,
                      height: 1.5,
                    ),
                  ),
                  if (p.specifications.isNotEmpty) ...[
                    const SizedBox(height: 16),
                    const _Label('Özellikler'),
                    const SizedBox(height: 4),
                    for (final e in p.specifications.entries)
                      Container(
                        padding: const EdgeInsets.symmetric(vertical: 10),
                        decoration: const BoxDecoration(
                          border: Border(bottom: BorderSide(color: line)),
                        ),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Expanded(
                              flex: 2,
                              child: Text(
                                e.key,
                                style: const TextStyle(
                                  color: muted,
                                  fontSize: 14,
                                ),
                              ),
                            ),
                            Expanded(
                              flex: 3,
                              child: Text(
                                e.value,
                                style: const TextStyle(
                                  fontSize: 14,
                                  fontWeight: FontWeight.w500,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 8, 20, 12),
          child: FilledButton(
            onPressed: inStock ? () => addToCart(context, p) : null,
            child: Text(inStock ? 'Sepete ekle' : 'Tükendi'),
          ),
        ),
      ),
    );
  }
}

class _Tag extends StatelessWidget {
  const _Tag(this.text, {this.bg = bg2, this.fg = ink});
  final String text;
  final Color bg;
  final Color fg;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 5),
    decoration: BoxDecoration(
      color: bg,
      borderRadius: BorderRadius.circular(999),
    ),
    child: Text(
      text,
      style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: fg),
    ),
  );
}

class _Label extends StatelessWidget {
  const _Label(this.text);
  final String text;

  @override
  Widget build(BuildContext context) => Text(
    text.toUpperCase(),
    style: const TextStyle(
      fontSize: 12,
      fontWeight: FontWeight.w600,
      letterSpacing: 1,
      color: muted,
    ),
  );
}
