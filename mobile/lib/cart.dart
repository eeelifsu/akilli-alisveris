import 'package:flutter/foundation.dart';

import 'models/product.dart';

class CartLine {
  CartLine(this.product, this.qty);
  final Product product;
  int qty;
}

class CartModel extends ChangeNotifier {
  final Map<int, CartLine> _lines = {};

  List<CartLine> get lines => _lines.values.toList();
  int get count => _lines.values.fold(0, (s, l) => s + l.qty);
  num get total => _lines.values.fold(0, (s, l) => s + l.qty * l.product.price);

  /// Stok sınırını aşmıyorsa ekler; eklenemediyse false döner.
  bool add(Product p) {
    final current = _lines[p.id]?.qty ?? 0;
    if (p.stock <= 0 || current >= p.stock) return false;
    _lines.putIfAbsent(p.id, () => CartLine(p, 0)).qty = current + 1;
    notifyListeners();
    return true;
  }

  void setQty(Product p, int qty) {
    if (qty <= 0) {
      _lines.remove(p.id);
    } else {
      _lines[p.id] = CartLine(p, qty.clamp(1, p.stock));
    }
    notifyListeners();
  }

  void clear() {
    _lines.clear();
    notifyListeners();
  }
}

final cart = CartModel();
