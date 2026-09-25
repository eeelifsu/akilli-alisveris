import 'package:flutter/material.dart';

import 'chat_page.dart';
import 'models/product.dart';
import 'product_detail_page.dart';
import 'services/product_service.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Alışveriş Asistanı',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.deepPurple),
        useMaterial3: true,
      ),
      home: const ProductListPage(),
    );
  }
}

class ProductListPage extends StatefulWidget {
  const ProductListPage({super.key});

  @override
  State<ProductListPage> createState() => _ProductListPageState();
}

class _ProductListPageState extends State<ProductListPage> {
  final _service = ProductService();
  late Future<List<Product>> _future;
  String _query = '';
  String? _category;

  @override
  void initState() {
    super.initState();
    _future = _service.getProducts();
  }

  Future<void> _reload() async {
    final future = _service.getProducts();
    setState(() => _future = future);
    try {
      await future;
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Ürünler')),
      floatingActionButton: FloatingActionButton.extended(
        icon: const Icon(Icons.chat_bubble_outline),
        label: const Text('Asistan'),
        onPressed: () async {
          final List<Product> products;
          try {
            products = await _future;
          } catch (_) {
            return;
          }
          if (!context.mounted) return;
          Navigator.of(context).push(
            MaterialPageRoute(builder: (_) => ChatPage(products: products)),
          );
        },
      ),
      body: FutureBuilder<List<Product>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.cloud_off, size: 48),
                    const SizedBox(height: 12),
                    Text(
                      'Sunucuya bağlanılamadı.\n${snapshot.error}',
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: _reload,
                      child: const Text('Tekrar dene'),
                    ),
                  ],
                ),
              ),
            );
          }
          final all = snapshot.data!;
          final categories = all.map((p) => p.category).toSet().toList()
            ..sort();
          final q = _query.toLowerCase();
          final products = all.where((p) {
            final matchesCategory =
                _category == null || p.category == _category;
            final matchesQuery =
                q.isEmpty ||
                p.name.toLowerCase().contains(q) ||
                p.brand.toLowerCase().contains(q) ||
                p.description.toLowerCase().contains(q);
            return matchesCategory && matchesQuery;
          }).toList();
          return Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(12, 8, 12, 0),
                child: TextField(
                  onChanged: (v) => setState(() => _query = v),
                  decoration: const InputDecoration(
                    hintText: 'Ürün, marka ara...',
                    prefixIcon: Icon(Icons.search),
                    border: OutlineInputBorder(),
                    isDense: true,
                  ),
                ),
              ),
              SizedBox(
                height: 52,
                child: ListView(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  children: [
                    for (final c in [null, ...categories])
                      Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: Center(
                          child: ChoiceChip(
                            label: Text(c ?? 'Tümü'),
                            selected: _category == c,
                            onSelected: (_) => setState(() => _category = c),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
              Expanded(
                child: RefreshIndicator(
                  onRefresh: _reload,
                  child: products.isEmpty
                      ? ListView(
                          children: const [
                            SizedBox(height: 80),
                            Center(child: Text('Ürün bulunamadı.')),
                          ],
                        )
                      : ListView.builder(
                          padding: const EdgeInsets.all(12),
                          itemCount: products.length,
                          itemBuilder: (context, i) =>
                              ProductCard(product: products[i]),
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

class ProductCard extends StatelessWidget {
  const ProductCard({super.key, required this.product});

  final Product product;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute(
            builder: (_) => ProductDetailPage(product: product),
          ),
        ),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      product.name,
                      style: theme.textTheme.titleMedium,
                    ),
                  ),
                  Text(
                    '${product.price.toStringAsFixed(0)} ₺',
                    style: theme.textTheme.titleMedium?.copyWith(
                      color: theme.colorScheme.primary,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 4),
              Text(product.description),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                children: [
                  Chip(label: Text(product.brand)),
                  Chip(label: Text(product.category)),
                  Chip(label: Text('Stok: ${product.stock}')),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
