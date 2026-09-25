import 'dart:convert';
import 'package:http/http.dart' as http;

import '../models/product.dart';
import 'product_service.dart';

class ChatMessage {
  final String role; // 'user' | 'assistant'
  final String content;
  final List<Product> products;
  final List<String> options;

  ChatMessage({
    required this.role,
    required this.content,
    this.products = const [],
    this.options = const [],
  });
}

class ChatService {
  Future<ChatMessage> send(List<ChatMessage> history) async {
    final response = await http
        .post(
          Uri.parse('${ProductService.baseUrl}/api/chat'),
          headers: {'Content-Type': 'application/json'},
          body: jsonEncode({
            'messages': history
                .map((m) => {'role': m.role, 'content': m.content})
                .toList(),
          }),
        )
        .timeout(const Duration(seconds: 120));

    if (response.statusCode != 200) {
      throw Exception(
        'Asistan şu an cevap veremiyor (${response.statusCode}). Biraz sonra tekrar dene.',
      );
    }

    final data = jsonDecode(utf8.decode(response.bodyBytes));
    return ChatMessage(
      role: 'assistant',
      content: data['reply'] as String,
      products: (data['products'] as List)
          .map((j) => Product.fromJson(j as Map<String, dynamic>))
          .toList(),
      options: ((data['options'] as List?) ?? const []).cast<String>(),
    );
  }
}
