import 'package:flutter/material.dart';

import 'cart.dart';
import 'theme.dart';
import 'widgets.dart';

class CartPage extends StatelessWidget {
  const CartPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Sepetim')),
      body: ListenableBuilder(
        listenable: cart,
        builder: (context, _) {
          final lines = cart.lines;
          if (lines.isEmpty) {
            return Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: 80,
                    height: 80,
                    decoration: const BoxDecoration(
                      color: bg2,
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(
                      Icons.shopping_bag_outlined,
                      size: 34,
                      color: ink,
                    ),
                  ),
                  const SizedBox(height: 14),
                  Text('Sepetin boş', style: serif(26)),
                  const SizedBox(height: 4),
                  const Text(
                    'Beğendiğin ürünleri sepete ekle.',
                    style: TextStyle(color: muted),
                  ),
                ],
              ),
            );
          }
          return Column(
            children: [
              Expanded(
                child: ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: lines.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, i) {
                    final l = lines[i];
                    return Card(
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Row(
                          children: [
                            ClipRRect(
                              borderRadius: BorderRadius.circular(12),
                              child: SizedBox(
                                width: 64,
                                child: ProductVisual(
                                  product: l.product,
                                  height: 64,
                                  iconSize: 28,
                                ),
                              ),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    l.product.name,
                                    style: const TextStyle(
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                  Text(
                                    tl(l.product.price),
                                    style: const TextStyle(color: muted),
                                  ),
                                  Row(
                                    children: [
                                      IconButton(
                                        visualDensity: VisualDensity.compact,
                                        icon: const Icon(Icons.remove_rounded),
                                        onPressed: () =>
                                            cart.setQty(l.product, l.qty - 1),
                                      ),
                                      Text(
                                        '${l.qty}',
                                        style: const TextStyle(
                                          fontWeight: FontWeight.w700,
                                        ),
                                      ),
                                      IconButton(
                                        visualDensity: VisualDensity.compact,
                                        icon: const Icon(Icons.add_rounded),
                                        onPressed: () =>
                                            cart.setQty(l.product, l.qty + 1),
                                      ),
                                    ],
                                  ),
                                ],
                              ),
                            ),
                            IconButton(
                              icon: const Icon(
                                Icons.delete_outline_rounded,
                                color: muted,
                              ),
                              onPressed: () => cart.setQty(l.product, 0),
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                ),
              ),
              SafeArea(
                child: Container(
                  padding: const EdgeInsets.fromLTRB(20, 16, 20, 12),
                  decoration: const BoxDecoration(
                    color: surface,
                    border: Border(top: BorderSide(color: line)),
                  ),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          const Text(
                            'Toplam',
                            style: TextStyle(fontSize: 15, color: muted),
                          ),
                          Text(tl(cart.total), style: serif(34)),
                        ],
                      ),
                      const SizedBox(height: 12),
                      SizedBox(
                        width: double.infinity,
                        child: FilledButton(
                          onPressed: () {
                            cart.clear();
                            Navigator.of(context).pop();
                            ScaffoldMessenger.of(context).showSnackBar(
                              const SnackBar(
                                behavior: SnackBarBehavior.floating,
                                content: Row(
                                  children: [
                                    Icon(
                                      Icons.check_circle_outline_rounded,
                                      color: lime,
                                      size: 20,
                                    ),
                                    SizedBox(width: 8),
                                    Text('Siparişin alındı! (demo)'),
                                  ],
                                ),
                              ),
                            );
                          },
                          child: const Text('Siparişi tamamla'),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
