import 'dart:async';

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
  static const _pageSize = 24;
  final _service = ProductService();
  final _scroll = ScrollController();
  Timer? _debounce;

  List<Product> _items = [];
  List<Facet> _categories = [];
  int _total = 0;
  int _page = 1;
  bool _loading = true;
  bool _loadingMore = false;
  Object? _error;
  int _requestId = 0;

  String _query = '';
  String? _category;
  String _sort = '';

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      if (_scroll.position.pixels > _scroll.position.maxScrollExtent - 400) {
        _loadMore();
      }
    });
    _service
        .getCategories()
        .then((c) {
          if (mounted) setState(() => _categories = c);
        })
        .catchError((_) {});
    _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final id = ++_requestId;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final page = await _service.getProducts(
        query: _query,
        category: _category,
        sort: _sort,
        pageSize: _pageSize,
      );
      if (!mounted || id != _requestId) return;
      setState(() {
        _items = page.items;
        _total = page.total;
        _page = page.page;
        _loading = false;
      });
    } catch (e) {
      if (!mounted || id != _requestId) return;
      setState(() {
        _error = e;
        _loading = false;
      });
    }
  }

  Future<void> _loadMore() async {
    if (_loading || _loadingMore || _items.length >= _total) return;
    final id = _requestId;
    setState(() => _loadingMore = true);
    try {
      final page = await _service.getProducts(
        query: _query,
        category: _category,
        sort: _sort,
        page: _page + 1,
        pageSize: _pageSize,
      );
      if (!mounted || id != _requestId) return;
      setState(() {
        _items = [..._items, ...page.items];
        _total = page.total;
        _page = page.page;
      });
    } catch (_) {
      // Sonraki kaydırmada tekrar denenir.
    } finally {
      if (mounted) setState(() => _loadingMore = false);
    }
  }

  void _onQuery(String value) {
    _query = value;
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 300), _load);
  }

  void _openChat() {
    Navigator.of(
      context,
    ).push(MaterialPageRoute(builder: (_) => const ChatPage()));
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
          _Header(onQuery: _onQuery),
          _filters(),
          Expanded(child: _body()),
        ],
      ),
    );
  }

  Widget _filters() {
    return SizedBox(
      height: 56,
      child: Row(
        children: [
          Expanded(
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.fromLTRB(16, 10, 0, 10),
              children: [
                for (final c in <Facet?>[null, ..._categories])
                  Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text(
                        c == null ? 'Tümü' : '${emojiFor(c.name)} ${c.name}',
                      ),
                      selected: _category == c?.name,
                      showCheckmark: false,
                      selectedColor: brand,
                      labelStyle: TextStyle(
                        color: _category == c?.name ? Colors.white : null,
                        fontWeight: FontWeight.w600,
                      ),
                      shape: StadiumBorder(
                        side: BorderSide(
                          color: _category == c?.name
                              ? brand
                              : const Color(0xFFE6E8F0),
                        ),
                      ),
                      backgroundColor: Colors.white,
                      onSelected: (_) {
                        _category = c?.name;
                        _load();
                      },
                    ),
                  ),
              ],
            ),
          ),
          PopupMenuButton<String>(
            tooltip: 'Sırala',
            icon: Icon(
              Icons.swap_vert_rounded,
              color: _sort.isEmpty ? null : brand,
            ),
            initialValue: _sort,
            onSelected: (v) {
              _sort = v;
              _load();
            },
            itemBuilder: (_) => const [
              PopupMenuItem(value: '', child: Text('Önerilen')),
              PopupMenuItem(
                value: 'price_asc',
                child: Text('Fiyat: düşükten yükseğe'),
              ),
              PopupMenuItem(
                value: 'price_desc',
                child: Text('Fiyat: yüksekten düşüğe'),
              ),
              PopupMenuItem(value: 'rating', child: Text('En yüksek puan')),
            ],
          ),
        ],
      ),
    );
  }

  Widget _body() {
    if (_loading && _items.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) return _ErrorView(error: _error!, onRetry: _load);
    return RefreshIndicator(
      onRefresh: _load,
      child: _items.isEmpty
          ? ListView(
              children: const [
                SizedBox(height: 80),
                Center(child: Text('🔍 Ürün bulunamadı.')),
              ],
            )
          : ListView.separated(
              controller: _scroll,
              padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
              itemCount: _items.length + 1,
              separatorBuilder: (_, _) => const SizedBox(height: 16),
              itemBuilder: (_, i) {
                if (i == _items.length) {
                  return Padding(
                    padding: const EdgeInsets.all(8),
                    child: Center(
                      child: _loadingMore
                          ? const CircularProgressIndicator()
                          : Text(
                              '$_total üründen ${_items.length} tanesi gösteriliyor',
                              style: const TextStyle(color: Colors.black45),
                            ),
                    ),
                  );
                }
                return ProductCard(product: _items[i]);
              },
            ),
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
