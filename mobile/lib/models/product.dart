class Product {
  final int id;
  final String name;
  final String description;
  final double price;
  final String category;
  final String brand;
  final int stock;
  final String? imageUrl;
  final double? rating;
  final int ratingCount;
  final String? specs;

  Product({
    required this.id,
    required this.name,
    required this.description,
    required this.price,
    required this.category,
    required this.brand,
    required this.stock,
    this.imageUrl,
    this.rating,
    this.ratingCount = 0,
    this.specs,
  });

  factory Product.fromJson(Map<String, dynamic> json) {
    return Product(
      id: json['id'],
      name: json['name'],
      description: json['description'],
      price: (json['price'] as num).toDouble(),
      category: json['category'],
      brand: json['brand'],
      stock: json['stock'],
      imageUrl: json['imageUrl'],
      rating: (json['rating'] as num?)?.toDouble(),
      ratingCount: (json['ratingCount'] as num?)?.toInt() ?? 0,
      specs: json['specs'],
    );
  }
}
