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
            return const Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text('🛒', style: TextStyle(fontSize: 56)),
                  SizedBox(height: 8),
                  Text(
                    'Sepetin boş',
                    style: TextStyle(fontWeight: FontWeight.w700),
                  ),
                  Text('Beğendiğin ürünleri sepete ekle.'),
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
                                  emojiSize: 30,
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
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                  Text(
                                    tl(l.product.price),
                                    style: const TextStyle(
                                      color: Colors.black54,
                                    ),
                                  ),
                                  Row(
                                    children: [
                                      IconButton(
                                        visualDensity: VisualDensity.compact,
                                        icon: const Icon(
                                          Icons.remove_circle_outline,
                                        ),
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
                                        icon: const Icon(
                                          Icons.add_circle_outline,
                                        ),
                                        onPressed: () =>
                                            cart.setQty(l.product, l.qty + 1),
                                      ),
                                    ],
                                  ),
                                ],
                              ),
                            ),
                            IconButton(
                              icon: const Icon(Icons.delete_outline),
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
                    color: Colors.white,
                    border: Border(top: BorderSide(color: Color(0xFFE6E8F0))),
                  ),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          const Text(
                            'Toplam',
                            style: TextStyle(
                              fontSize: 18,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                          Text(
                            tl(cart.total),
                            style: const TextStyle(
                              fontSize: 24,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
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
                                content: Text('🎉 Siparişin alındı! (demo)'),
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
