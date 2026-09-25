import 'dart:convert';
import 'package:http/http.dart' as http;

import 'product_service.dart';

class ChatMessage {
  final String role; // 'user' | 'assistant'
  final String content;
  final List<int> productIds;

  ChatMessage({
    required this.role,
    required this.content,
    this.productIds = const [],
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
      final detail =
          (jsonDecode(utf8.decode(response.bodyBytes))
              as Map<String, dynamic>)['detail'];
      throw Exception(
        'Asistan cevap veremedi (${response.statusCode}): $detail',
      );
    }

    final data = jsonDecode(utf8.decode(response.bodyBytes));
    return ChatMessage(
      role: 'assistant',
      content: data['reply'] as String,
      productIds: (data['productIds'] as List).cast<int>(),
    );
  }
}
