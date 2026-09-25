namespace ShoppingAssistant.Api.Models;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Gösterilen fiyat. <see cref="Currency"/> cinsindendir (varsayılan TRY).</summary>
    public decimal Price { get; set; }

    /// <summary>Fiyatın para birimi, ISO 4217 kodu (örn. "TRY").</summary>
    public string Currency { get; set; } = "TRY";

    /// <summary>Kaynaktaki özgün fiyat (örn. 45999 INR). Çeviri yapılmadıysa boş.</summary>
    public decimal? OriginalPrice { get; set; }

    public string? OriginalCurrency { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public int Stock { get; set; }

    public string? ImageUrl { get; set; }

    public double? Rating { get; set; }

    public int RatingCount { get; set; }

    /// <summary>
    /// Teknik özellikler (anahtar → değer). Karşılaştırma için önemli anahtarlar
    /// <see cref="SpecKeys"/> içindeki standart adlarla yazılmalıdır.
    /// </summary>
    public Dictionary<string, string> Specifications { get; set; } = new();

    /// <summary>Ürünün çıkış yılı (biliniyorsa). Sıralamada yeni ürünler öne alınır.</summary>
    public int? ReleaseYear { get; set; }

    /// <summary>Verinin geldiği kaynak: "seed", "dummyjson", "bestbuy" ...</summary>
    public string Source { get; set; } = "seed";

    /// <summary>Kaynaktaki ürün kimliği. (Source, ExternalId) tekildir.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Ürünün kaynak sitedeki sayfası.</summary>
    public string? SourceUrl { get; set; }
}


/// <summary>
/// Farklı kaynaklardan gelen özelliklerin aynı adla saklanması için standart anahtarlar.
/// Karşılaştırma ve filtreleme bu adlara güvenir.
/// </summary>
public static class SpecKeys
{
    public const string Ram = "RAM";
    public const string Storage = "Depolama";
    public const string Screen = "Ekran";
    public const string Processor = "İşlemci";
    public const string Camera = "Kamera";
    public const string Battery = "Batarya";
    public const string Weight = "Ağırlık";
    public const string OperatingSystem = "İşletim Sistemi";
    public const string Warranty = "Garanti";
    public const string Connectivity = "Bağlantı";
}
