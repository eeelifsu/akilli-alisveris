import 'dart:convert';
import 'package:http/http.dart' as http;

import '../models/product.dart';

class ProductPage {
  ProductPage({required this.items, required this.total, required this.page});
  final List<Product> items;
  final int total;
  final int page;
}

class Facet {
  Facet(this.name, this.count);
  final String name;
  final int count;
}

class ProductService {
  /// Varsayılan: canlı sunucu. Yerel sunucu için: flutter run `--dart-define=API_URL=http://IP:PORT`
  static const String baseUrl = String.fromEnvironment(
    'API_URL',
    defaultValue: 'https://akilli-alisveris.onrender.com',
  );

  Future<dynamic> _get(String path, [Map<String, String>? query]) async {
    final uri = Uri.parse('$baseUrl$path').replace(queryParameters: query);
    final response = await http.get(uri).timeout(const Duration(seconds: 20));
    if (response.statusCode != 200) {
      throw Exception('Sunucu hatası (${response.statusCode})');
    }
    return jsonDecode(utf8.decode(response.bodyBytes));
  }

  Future<ProductPage> getProducts({
    String? query,
    String? category,
    String? sort,
    int page = 1,
    int pageSize = 24,
  }) async {
    final data = await _get('/api/products', {
      'page': '$page',
      'pageSize': '$pageSize',
      if (query != null && query.trim().isNotEmpty) 'q': query.trim(),
      'category': ?category,
      if (sort != null && sort.isNotEmpty) 'sort': sort,
    });
    return ProductPage(
      items: (data['items'] as List)
          .map((j) => Product.fromJson(j as Map<String, dynamic>))
          .toList(),
      total: data['total'] as int,
      page: data['page'] as int,
    );
  }

  Future<List<Facet>> getCategories() async {
    final data = await _get('/api/facets');
    return (data['categories'] as List)
        .map((c) => Facet(c['name'] as String, c['count'] as int))
        .toList();
  }
}
