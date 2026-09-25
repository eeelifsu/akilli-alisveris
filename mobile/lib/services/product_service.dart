import 'dart:convert';
import 'package:http/http.dart' as http;

import '../models/product.dart';

class ProductService {
  static const String baseUrl = 'http://192.168.1.11:5065';

  Future<List<Product>> getProducts() async {
    final response = await http.get(Uri.parse('$baseUrl/api/products'));

    if (response.statusCode == 200) {
      final List<dynamic> data = jsonDecode(response.body);

      return data.map((json) => Product.fromJson(json)).toList();
    }

    throw Exception('Ürünler alınamadı. Status code: ${response.statusCode}');
  }
}
