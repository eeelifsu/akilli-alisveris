import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

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

  void _openChat([String? message]) {
    Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => ChatPage(initialMessage: message)),
    );
  }

  Widget _searchBox() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 20, 16, 0),
      child: TextField(
        onChanged: _onQuery,
        decoration: pillInput(
          hint: 'Mağazada ürün, marka ara…',
          prefix: const Icon(Icons.search_rounded, color: muted),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    // Üst kısım koyu: durum çubuğu yazıları açık renk olsun.
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: _scaffold(),
    );
  }

  Widget _scaffold() {
    return Scaffold(
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _openChat,
        backgroundColor: ink,
        foregroundColor: bg,
        elevation: 6,
        shape: const StadiumBorder(),
        icon: const Icon(Icons.auto_awesome_rounded, color: lime, size: 20),
        label: const Text(
          'Asistan',
          style: TextStyle(fontWeight: FontWeight.w600, fontSize: 15.5),
        ),
      ),
      body: Column(
        children: [
          _Header(onAsk: _openChat),
          _searchBox(),
          _filters(),
          Expanded(child: _body()),
        ],
      ),
    );
  }

  Widget _filters() {
    return SizedBox(
      height: 60,
      child: Row(
        children: [
          Expanded(
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.fromLTRB(16, 12, 0, 10),
              children: [
                for (final c in <Facet?>[null, ..._categories])
                  Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: Builder(
                      builder: (_) {
                        final selected = _category == c?.name;
                        return ChoiceChip(
                          avatar: c == null
                              ? null
                              : Icon(
                                  iconFor(c.name),
                                  size: 17,
                                  color: selected ? lime : muted,
                                ),
                          label: Text(c == null ? 'Tümü' : c.name),
                          selected: selected,
                          showCheckmark: false,
                          labelStyle: TextStyle(
                            color: selected ? bg : ink,
                            fontWeight: FontWeight.w500,
                          ),
                          side: BorderSide(color: selected ? ink : line),
                          onSelected: (_) {
                            _category = c?.name;
                            _load();
                          },
                        );
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
              color: _sort.isEmpty ? muted : ink,
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
                Icon(Icons.search_off_rounded, size: 40, color: muted),
                SizedBox(height: 8),
                Center(
                  child: Text(
                    'Aramana uygun ürün bulunamadı.',
                    style: TextStyle(color: muted),
                  ),
                ),
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
                              style: const TextStyle(
                                color: muted,
                                fontSize: 13,
                              ),
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

class _Header extends StatefulWidget {
  const _Header({required this.onAsk});

  final ValueChanged<String?> onAsk;

  @override
  State<_Header> createState() => _HeaderState();
}

class _HeaderState extends State<_Header> {
  final _controller = TextEditingController();

  static const _prompts = [
    '50 bin TL altı yazılım için laptop',
    '10 bin TL altı telefon',
    'Kablosuz kulaklık öner',
  ];

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _submit([String? text]) {
    final value = (text ?? _controller.text).trim();
    _controller.clear();
    FocusScope.of(context).unfocus();
    widget.onAsk(value.isEmpty ? null : value);
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: ink,
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
                  ClipRRect(
                    borderRadius: BorderRadius.circular(10),
                    child: Image.asset('assets/logo.png', height: 36),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text.rich(
                      TextSpan(
                        children: [
                          const TextSpan(text: 'Akıllı '),
                          TextSpan(
                            text: 'Alışveriş',
                            style: serif(
                              24,
                              color: bg,
                              style: FontStyle.italic,
                            ),
                          ),
                        ],
                      ),
                      style: serif(24, color: bg),
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
                        backgroundColor: lime,
                        textColor: ink,
                        label: Text('${cart.count}'),
                        child: const Icon(
                          Icons.shopping_bag_outlined,
                          color: bg,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 18),
              Text.rich(
                TextSpan(
                  children: [
                    const TextSpan(text: 'Doğru ürünü\n'),
                    TextSpan(
                      text: 'birlikte',
                      style: serif(36, color: lime, style: FontStyle.italic),
                    ),
                    const TextSpan(text: ' bulalım.'),
                  ],
                ),
                style: serif(36, color: bg),
              ),
              const SizedBox(height: 16),
              Padding(
                padding: const EdgeInsets.only(right: 8),
                child: TextField(
                  controller: _controller,
                  onSubmitted: _submit,
                  textInputAction: TextInputAction.send,
                  style: const TextStyle(color: ink),
                  decoration: InputDecoration(
                    hintText: 'Örn: 50.000 TL altı laptop…',
                    hintStyle: const TextStyle(color: muted),
                    prefixIcon: const Padding(
                      padding: EdgeInsets.only(left: 16, right: 8),
                      child: Icon(
                        Icons.auto_awesome_outlined,
                        size: 19,
                        color: muted,
                      ),
                    ),
                    prefixIconConstraints: const BoxConstraints(
                      minWidth: 0,
                      minHeight: 0,
                    ),
                    suffixIcon: Padding(
                      padding: const EdgeInsets.all(5),
                      child: IconButton.filled(
                        style: IconButton.styleFrom(
                          backgroundColor: lime,
                          foregroundColor: ink,
                        ),
                        onPressed: _submit,
                        icon: const Icon(Icons.arrow_forward_rounded, size: 20),
                      ),
                    ),
                    filled: true,
                    fillColor: surface,
                    isDense: true,
                    contentPadding: const EdgeInsets.symmetric(vertical: 16),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(999),
                      borderSide: BorderSide.none,
                    ),
                  ),
                ),
              ),
              const SizedBox(height: 12),
              SizedBox(
                height: 34,
                child: ListView(
                  scrollDirection: Axis.horizontal,
                  children: [
                    for (final p in _prompts)
                      Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: GestureDetector(
                          onTap: () => _submit(p),
                          child: Container(
                            alignment: Alignment.center,
                            padding: const EdgeInsets.symmetric(horizontal: 14),
                            decoration: BoxDecoration(
                              borderRadius: BorderRadius.circular(999),
                              border: Border.all(
                                color: bg.withValues(alpha: .28),
                              ),
                            ),
                            child: Text(
                              p,
                              style: TextStyle(
                                fontSize: 12.5,
                                color: bg.withValues(alpha: .85),
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                          ),
                        ),
                      ),
                  ],
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
            const Icon(Icons.cloud_off_rounded, size: 44, color: muted),
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
