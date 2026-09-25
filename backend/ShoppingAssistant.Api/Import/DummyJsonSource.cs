using System.Text.Json;
using System.Text.RegularExpressions;
using ShoppingAssistant.Api.Models;

namespace ShoppingAssistant.Api.Import;

/// <summary>
/// dummyjson.com — anahtarsız, hazır örnek katalog (yaklaşık 38 elektronik ürün, gerçek görselli).
/// Gerçek bir kaynak değildir; altyapıyı doğrulamak için kullanılır.
/// </summary>
public class DummyJsonSource(HttpClient http, IConfiguration config) : IProductSource
{
    public string Name => "dummyjson";

    private static readonly string[] Categories = ["smartphones", "laptops", "tablets", "mobile-accessories"];

    public async Task<List<ImportedProduct>> FetchAsync(CancellationToken ct)
    {
        var usdTry = config.GetValue("Import:UsdTry", 41m);
        var result = new List<ImportedProduct>();

        foreach (var cat in Categories)
        {
            using var stream = await http.GetStreamAsync($"https://dummyjson.com/products/category/{cat}?limit=100", ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            foreach (var p in doc.RootElement.GetProperty("products").EnumerateArray())
            {
                var title = p.GetProperty("title").GetString() ?? "";
                var usd = p.GetProperty("price").GetDecimal();
                var reviews = p.TryGetProperty("reviews", out var r) ? r.GetArrayLength() : 0;
                var specs = new Dictionary<string, string>();
                if (p.TryGetProperty("warrantyInformation", out var w) && w.GetString() is { } warranty)
                    specs[SpecKeys.Warranty] = warranty;

                result.Add(new ImportedProduct(
                    ExternalId: p.GetProperty("id").GetInt32().ToString(),
                    Name: title,
                    Description: p.GetProperty("description").GetString() ?? "",
                    Price: Math.Round(usd * usdTry / 10m) * 10m - 1m,
                    Category: MapCategory(cat, title),
                    Brand: p.TryGetProperty("brand", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString()! : "Diğer",
                    Stock: p.GetProperty("stock").GetInt32(),
                    ImageUrl: p.TryGetProperty("thumbnail", out var t) ? t.GetString() : null,
                    Rating: p.TryGetProperty("rating", out var rt) ? rt.GetDouble() : null,
                    RatingCount: reviews,
                    Specifications: specs,
                    SourceUrl: null,
                    OriginalPrice: usd,
                    OriginalCurrency: "USD"));
            }
        }
        return result;
    }

    private static string MapCategory(string source, string title) => source switch
    {
        "smartphones" => "Telefon",
        "laptops" => "Laptop",
        "tablets" => "Tablet",
        _ when Regex.IsMatch(title, "airpods|headphone|earbuds|beats|buds", RegexOptions.IgnoreCase) => "Kulaklık",
        _ when Regex.IsMatch(title, "watch", RegexOptions.IgnoreCase) => "Saat",
        _ => "Aksesuar",
    };
}
