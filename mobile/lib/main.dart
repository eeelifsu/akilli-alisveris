import 'package:flutter/material.dart';

import 'cart.dart';
import 'cart_page.dart';
import 'chat_page.dart';
import 'models/product.dart';
import 'services/product_service.dart';
import 'theme.dart';
import 'widgets.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Akıllı Alışveriş',
      debugShowCheckedModeBanner: false,
      theme: buildTheme(),
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

  Future<void> _openChat() async {
    final List<Product> products;
    try {
      products = await _future;
    } catch (_) {
      return;
    }
    if (!mounted) return;
    Navigator.of(
      context,
    ).push(MaterialPageRoute(builder: (_) => ChatPage(products: products)));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      floatingActionButton: DecoratedBox(
        decoration: BoxDecoration(
          gradient: brandGradient,
          borderRadius: BorderRadius.circular(999),
          boxShadow: [
            BoxShadow(
              color: brand.withValues(alpha: .4),
              blurRadius: 18,
              offset: const Offset(0, 8),
            ),
          ],
        ),
        child: TextButton.icon(
          onPressed: _openChat,
          style: TextButton.styleFrom(
            foregroundColor: Colors.white,
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
          ),
          icon: const Text('✨', style: TextStyle(fontSize: 18)),
          label: const Text(
            'Asistan',
            style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16),
          ),
        ),
      ),
      body: Column(
        children: [
          _Header(onQuery: (v) => setState(() => _query = v)),
          Expanded(
            child: FutureBuilder<List<Product>>(
              future: _future,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Center(child: CircularProgressIndicator());
                }
                if (snapshot.hasError) {
                  return _ErrorView(error: snapshot.error!, onRetry: _reload);
                }
                return _buildList(snapshot.data!);
              },
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildList(List<Product> all) {
    final categories = all.map((p) => p.category).toSet().toList()..sort();
    final q = _query.toLowerCase();
    final products = all.where((p) {
      final okCategory = _category == null || p.category == _category;
      final okQuery =
          q.isEmpty ||
          p.name.toLowerCase().contains(q) ||
          p.brand.toLowerCase().contains(q) ||
          p.description.toLowerCase().contains(q);
      return okCategory && okQuery;
    }).toList();

    return Column(
      children: [
        SizedBox(
          height: 56,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            children: [
              for (final c in <String?>[null, ...categories])
                Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: ChoiceChip(
                    label: Text(c == null ? 'Tümü' : '${emojiFor(c)} $c'),
                    selected: _category == c,
                    showCheckmark: false,
                    selectedColor: brand,
                    labelStyle: TextStyle(
                      color: _category == c ? Colors.white : null,
                      fontWeight: FontWeight.w600,
                    ),
                    shape: StadiumBorder(
                      side: BorderSide(
                        color: _category == c ? brand : const Color(0xFFE6E8F0),
                      ),
                    ),
                    backgroundColor: Colors.white,
                    onSelected: (_) => setState(() => _category = c),
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
                      Center(child: Text('🔍 Ürün bulunamadı.')),
                    ],
                  )
                : ListView.separated(
                    padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
                    itemCount: products.length,
                    separatorBuilder: (_, _) => const SizedBox(height: 16),
                    itemBuilder: (_, i) => ProductCard(product: products[i]),
                  ),
          ),
        ),
      ],
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.onQuery});

  final ValueChanged<String> onQuery;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        gradient: brandGradient,
        borderRadius: BorderRadius.vertical(bottom: Radius.circular(28)),
      ),
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 8, 12, 20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  const Expanded(
                    child: Text(
                      'Akıllı Alışveriş',
                      style: TextStyle(
                        color: Colors.white,
                        fontSize: 24,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                  ListenableBuilder(
                    listenable: cart,
                    builder: (context, _) => IconButton(
                      onPressed: () => Navigator.of(context).push(
                        MaterialPageRoute(builder: (_) => const CartPage()),
                      ),
                      icon: Badge(
                        isLabelVisible: cart.count > 0,
                        label: Text('${cart.count}'),
                        child: const Icon(
                          Icons.shopping_cart_outlined,
                          color: Colors.white,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
              const Text(
                'Ne aradığını söyle, en uygununu bulalım.',
                style: TextStyle(color: Colors.white70, fontSize: 14),
              ),
              const SizedBox(height: 14),
              Padding(
                padding: const EdgeInsets.only(right: 8),
                child: TextField(
                  onChanged: onQuery,
                  decoration: InputDecoration(
                    hintText: 'Ürün, marka ara…',
                    prefixIcon: const Icon(Icons.search),
                    filled: true,
                    fillColor: Colors.white,
                    isDense: true,
                    contentPadding: const EdgeInsets.symmetric(vertical: 14),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(999),
                      borderSide: BorderSide.none,
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.error, required this.onRetry});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.cloud_off, size: 48),
            const SizedBox(height: 12),
            Text(
              'Sunucuya bağlanılamadı.\n$error',
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 16),
            FilledButton(onPressed: onRetry, child: const Text('Tekrar dene')),
          ],
        ),
      ),
    );
  }
}
