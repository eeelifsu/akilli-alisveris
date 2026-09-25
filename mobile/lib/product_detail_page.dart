import 'package:flutter/material.dart';

import 'models/product.dart';
import 'theme.dart';
import 'widgets.dart';

class ProductDetailPage extends StatelessWidget {
  const ProductDetailPage({super.key, required this.product});

  final Product product;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final p = product;
    final inStock = p.stock > 0;
    return Scaffold(
      body: CustomScrollView(
        slivers: [
          SliverAppBar(
            pinned: true,
            expandedHeight: 280,
            flexibleSpace: FlexibleSpaceBar(
              background: ProductVisual(
                product: p,
                height: 280,
                emojiSize: 110,
              ),
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
                      Chip(label: Text(p.brand)),
                      Chip(label: Text(p.category)),
                      Chip(
                        avatar: Icon(
                          inStock ? Icons.check_circle : Icons.cancel,
                          size: 18,
                          color: inStock
                              ? Colors.green
                              : theme.colorScheme.error,
                        ),
                        label: Text(
                          inStock ? 'Stokta: ${p.stock}' : 'Stokta yok',
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Text(
                    p.name,
                    style: theme.textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  if (p.rating != null) ...[
                    const SizedBox(height: 6),
                    RatingLabel(p),
                  ],
                  const SizedBox(height: 8),
                  Text(
                    tl(p.price),
                    style: theme.textTheme.headlineMedium?.copyWith(
                      color: brand,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 16),
                  Text('Açıklama', style: theme.textTheme.titleMedium),
                  const SizedBox(height: 4),
                  Text(
                    p.description,
                    style: theme.textTheme.bodyLarge?.copyWith(
                      color: Colors.black54,
                    ),
                  ),
                  if (p.specifications.isNotEmpty) ...[
                    const SizedBox(height: 16),
                    Text('Özellikler', style: theme.textTheme.titleMedium),
                    const SizedBox(height: 4),
                    for (final e in p.specifications.entries)
                      Padding(
                        padding: const EdgeInsets.symmetric(vertical: 2),
                        child: Text(
                          '${e.key}: ${e.value}',
                          style: theme.textTheme.bodyLarge?.copyWith(
                            color: Colors.black54,
                          ),
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
