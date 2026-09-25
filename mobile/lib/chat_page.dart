import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'models/product.dart';
import 'product_detail_page.dart';
import 'services/chat_service.dart';
import 'theme.dart';
import 'widgets.dart';

const _suggestions = [
  '50 bin TL altı bir laptop öner',
  'Kablosuz kulaklık arıyorum',
  'En yüksek puanlı telefonlar hangileri?',
  'Samsung tablet var mı?',
];

class ChatPage extends StatefulWidget {
  const ChatPage({super.key, this.initialMessage});

  /// Açılır açılmaz asistana gönderilecek mesaj (ana ekrandaki arama kutusundan).
  final String? initialMessage;

  @override
  State<ChatPage> createState() => _ChatPageState();
}

class _ChatPageState extends State<ChatPage> {
  final _service = ChatService();
  final _controller = TextEditingController();
  final _scroll = ScrollController();
  final List<ChatMessage> _messages = [];
  bool _loading = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    final first = widget.initialMessage;
    if (first != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _send(first));
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    _scroll.dispose();
    super.dispose();
  }

  void _scrollToEnd() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scroll.hasClients) {
        _scroll.animateTo(
          _scroll.position.maxScrollExtent,
          duration: const Duration(milliseconds: 250),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Future<void> _send([String? preset]) async {
    final text = (preset ?? _controller.text).trim();
    if (text.isEmpty || _loading) return;
    _controller.clear();
    setState(() {
      _messages.add(ChatMessage(role: 'user', content: text));
      _loading = true;
      _error = null;
    });
    _scrollToEnd();
    try {
      final reply = await _service.send(_messages);
      if (mounted) setState(() => _messages.add(reply));
    } catch (e) {
      if (mounted) {
        setState(() {
          _messages.removeLast(); // başarısız mesaj geçmişte kalmasın
          _error = e.toString().replaceFirst('Exception: ', '');
        });
      }
    } finally {
      if (mounted) setState(() => _loading = false);
      _scrollToEnd();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        backgroundColor: ink,
        foregroundColor: bg,
        systemOverlayStyle: SystemUiOverlayStyle.light,
        titleTextStyle: const TextStyle(color: bg),
        shape: const Border(),
        titleSpacing: 0,
        title: Row(
          children: [
            Container(
              width: 36,
              height: 36,
              decoration: const BoxDecoration(
                color: lime,
                shape: BoxShape.circle,
              ),
              child: const Icon(
                Icons.auto_awesome_rounded,
                size: 18,
                color: ink,
              ),
            ),
            const SizedBox(width: 12),
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Alışveriş Asistanı',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w600,
                    color: bg,
                  ),
                ),
                Row(
                  children: [
                    Container(
                      width: 6,
                      height: 6,
                      decoration: const BoxDecoration(
                        color: Color(0xFF5CB85C),
                        shape: BoxShape.circle,
                      ),
                    ),
                    const SizedBox(width: 6),
                    Text(
                      'Sana uygun ürünü bulalım',
                      style: TextStyle(
                        fontSize: 12,
                        color: bg.withValues(alpha: .7),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ],
        ),
      ),
      body: Column(
        children: [
          Expanded(
            child: ListView(
              controller: _scroll,
              padding: const EdgeInsets.all(12),
              children: [
                _bubble(
                  false,
                  'Merhaba! Bütçeni ve ne aradığını yaz, sana uygun ürünleri önereyim.',
                ),
                for (final m in _messages) ..._messageWidgets(m),
                if (!_loading &&
                    _messages.isNotEmpty &&
                    _messages.last.options.isNotEmpty)
                  _options(_messages.last.options),
                if (_loading) _typing(),
                if (_error != null) _bubble(false, '$_error', error: true),
              ],
            ),
          ),
          if (_messages.isEmpty)
            SizedBox(
              height: 44,
              child: ListView(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(horizontal: 12),
                children: [
                  for (final s in _suggestions)
                    Padding(
                      padding: const EdgeInsets.only(right: 8),
                      child: ActionChip(
                        label: Text(s, style: const TextStyle(color: muted)),
                        backgroundColor: surface,
                        side: const BorderSide(color: line),
                        shape: const StadiumBorder(),
                        onPressed: () => _send(s),
                      ),
                    ),
                ],
              ),
            ),
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
              child: Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _controller,
                      textInputAction: TextInputAction.send,
                      onSubmitted: (_) => _send(),
                      decoration: pillInput(hint: 'Mesajını yaz…'),
                    ),
                  ),
                  const SizedBox(width: 8),
                  IconButton.filled(
                    style: IconButton.styleFrom(
                      backgroundColor: lime,
                      foregroundColor: ink,
                      disabledBackgroundColor: line,
                      minimumSize: const Size(48, 48),
                    ),
                    onPressed: _loading ? null : _send,
                    icon: const Icon(Icons.send_rounded),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _options(List<String> options) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Wrap(
        spacing: 8,
        runSpacing: 8,
        children: [
          for (final o in options)
            ActionChip(
              label: Text(
                o,
                style: const TextStyle(color: ink, fontWeight: FontWeight.w600),
              ),
              backgroundColor: surface,
              side: const BorderSide(color: ink),
              shape: const StadiumBorder(),
              onPressed: () => _send(o),
            ),
        ],
      ),
    );
  }

  List<Widget> _messageWidgets(ChatMessage m) {
    final isUser = m.role == 'user';
    return [
      _bubble(isUser, m.content),
      for (final p in m.products) _recommendation(p),
    ];
  }

  Widget _bubble(bool isUser, String text, {bool error = false}) {
    return Align(
      alignment: isUser ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.only(bottom: 8),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        constraints: BoxConstraints(
          maxWidth: MediaQuery.of(context).size.width * 0.8,
        ),
        decoration: BoxDecoration(
          color: error ? const Color(0xFFF8E1DA) : (isUser ? ink : surface),
          border: isUser || error ? null : Border.all(color: line),
          borderRadius: BorderRadius.only(
            topLeft: const Radius.circular(18),
            topRight: const Radius.circular(18),
            bottomLeft: Radius.circular(isUser ? 18 : 5),
            bottomRight: Radius.circular(isUser ? 5 : 18),
          ),
        ),
        child: Text(
          text,
          style: TextStyle(
            height: 1.4,
            color: error ? bad : (isUser ? bg : ink),
          ),
        ),
      ),
    );
  }

  Widget _typing() => Align(
    alignment: Alignment.centerLeft,
    child: Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      decoration: BoxDecoration(
        color: surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: line),
      ),
      child: const SizedBox(
        width: 36,
        height: 8,
        child: LinearProgressIndicator(
          minHeight: 4,
          color: ink,
          backgroundColor: line,
          borderRadius: BorderRadius.all(Radius.circular(4)),
        ),
      ),
    ),
  );

  Widget _recommendation(Product p) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: InkWell(
        borderRadius: BorderRadius.circular(18),
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute(builder: (_) => ProductDetailPage(product: p)),
        ),
        child: Padding(
          padding: const EdgeInsets.all(10),
          child: Row(
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(12),
                child: SizedBox(
                  width: 56,
                  child: ProductVisual(product: p, height: 56, iconSize: 26),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      p.name,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 14,
                      ),
                    ),
                    Text(
                      '${tl(p.price)} · ${p.brand}',
                      style: const TextStyle(color: muted, fontSize: 12.5),
                    ),
                  ],
                ),
              ),
              OutlinedButton(
                onPressed: p.stock <= 0 ? null : () => addToCart(context, p),
                style: OutlinedButton.styleFrom(minimumSize: const Size(0, 34)),
                child: const Text('Ekle'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
