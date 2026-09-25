import 'package:flutter/material.dart';

import 'models/product.dart';

class ProductDetailPage extends StatelessWidget {
  const ProductDetailPage({super.key, required this.product});

  final Product product;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final inStock = product.stock > 0;
    return Scaffold(
      appBar: AppBar(title: Text(product.name)),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Container(
            height: 180,
            decoration: BoxDecoration(
              color: theme.colorScheme.primaryContainer,
              borderRadius: BorderRadius.circular(16),
            ),
            child: Icon(
              Icons.shopping_bag_outlined,
              size: 72,
              color: theme.colorScheme.onPrimaryContainer,
            ),
          ),
          const SizedBox(height: 16),
          Text(product.name, style: theme.textTheme.headlineSmall),
          const SizedBox(height: 4),
          Text(
            '${product.price.toStringAsFixed(0)} ₺',
            style: theme.textTheme.headlineSmall?.copyWith(
              color: theme.colorScheme.primary,
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            children: [
              Chip(label: Text(product.brand)),
              Chip(label: Text(product.category)),
              Chip(
                avatar: Icon(
                  inStock ? Icons.check_circle : Icons.cancel,
                  size: 18,
                  color: inStock ? Colors.green : theme.colorScheme.error,
                ),
                label: Text(
                  inStock ? 'Stokta: ${product.stock}' : 'Stokta yok',
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Text('Açıklama', style: theme.textTheme.titleMedium),
          const SizedBox(height: 4),
          Text(product.description, style: theme.textTheme.bodyLarge),
        ],
      ),
    );
  }
}
