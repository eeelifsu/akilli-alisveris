namespace ShoppingAssistant.Api.Models;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Türk Lirası cinsinden satış fiyatı.</summary>
    public decimal Price { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public int Stock { get; set; }

    public string? ImageUrl { get; set; }

    public double? Rating { get; set; }

    public int RatingCount { get; set; }

    /// <summary>Teknik özellikler, "Anahtar: değer; Anahtar: değer" biçiminde.</summary>
    public string? Specs { get; set; }

    /// <summary>Verinin geldiği kaynak: "seed", "dummyjson", "bestbuy" ...</summary>
    public string Source { get; set; } = "seed";

    /// <summary>Kaynaktaki ürün kimliği. (Source, ExternalId) tekildir.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Ürünün kaynak sitedeki sayfası.</summary>
    public string? SourceUrl { get; set; }
}
