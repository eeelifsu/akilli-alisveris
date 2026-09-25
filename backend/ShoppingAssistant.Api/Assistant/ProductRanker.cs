using System.Text.RegularExpressions;
using ShoppingAssistant.Api.Models;

namespace ShoppingAssistant.Api.Assistant;

/// <summary>Aday ürünleri kullanıcının ihtiyacına göre puanlar. Hepsi veritabanındaki gerçek verilerden hesaplanır.</summary>
public static class ProductRanker
{
    public static List<Product> Top(IEnumerable<Product> candidates, ShoppingIntent intent, int count)
    {
        return candidates
            .DistinctBy(p => (p.Name, p.Price)) // veri setinde aynı ürünün kopyaları var
            .Select(p => (Product: p, Score: Score(p, intent)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Product.Id)
            .Take(count)
            .Select(x => x.Product)
            .ToList();
    }

    public static double Score(Product p, ShoppingIntent intent)
    {
        double score = 0;

        // Genel kalite: puan, değerlendirme sayısı, yenilik.
        score += (p.Rating ?? 3.4) * 2;
        score += Math.Log10(p.RatingCount + 1);
        if (p.ReleaseYear is { } y) score += Math.Clamp((y - 2014) * 0.35, 0, 3);

        // Fiyat: bütçe varsa bütçeyi iyi kullanan (daha güçlü) ürün; "ucuz" istenirse en ucuz.
        var price = (double)p.Price;
        if (intent.WantsCheap)
            score -= Math.Min(price / 5000, 6);
        else if (intent.MaxPrice is { } max && max > 0)
            score += Math.Min(price / (double)max, 1) * 2.5;

        if (intent.WantsBest) score += (p.Rating ?? 0) * 1.5;

        var ram = Number(Spec(p, SpecKeys.Ram));
        var cpu = Spec(p, SpecKeys.Processor);
        var gpu = Spec(p, "Ekran Kartı");
        var storage = Spec(p, SpecKeys.Storage);
        var weight = Number(Spec(p, SpecKeys.Weight));
        var text = $"{p.Name} {p.Description}";

        foreach (var use in intent.UseCases)
        {
            switch (use)
            {
                case "gaming":
                    if (Regex.IsMatch(gpu, "nvidia|geforce|gtx|rtx|radeon rx", RegexOptions.IgnoreCase)) score += 5;
                    if (Regex.IsMatch(text, "gaming|oyun", RegexOptions.IgnoreCase)) score += 3;
                    score += RamBonus(ram, 8, 16);
                    break;
                case "software":
                    score += RamBonus(ram, 8, 16) * 1.5;
                    if (Regex.IsMatch(cpu, @"i5|i7|i9|ryzen [579]|m[1-4]|core ultra", RegexOptions.IgnoreCase)) score += 2;
                    if (storage.Contains("SSD", StringComparison.OrdinalIgnoreCase)) score += 2;
                    break;
                case "design":
                    score += RamBonus(ram, 8, 16);
                    if (Regex.IsMatch(cpu, @"i7|i9|ryzen [79]|m[1-4]", RegexOptions.IgnoreCase)) score += 2;
                    if (gpu.Length > 0 && !gpu.Contains("Integrated", StringComparison.OrdinalIgnoreCase)) score += 2;
                    break;
                case "student":
                case "daily":
                case "office":
                    if (storage.Contains("SSD", StringComparison.OrdinalIgnoreCase)) score += 1.5;
                    if (weight is > 0 and <= 2.0) score += 1.5;
                    score -= Math.Min(price / 20000, 2); // bu kullanımlarda pahalı olmak avantaj değil
                    break;
                case "camera":
                    score += Math.Min(Number(Spec(p, SpecKeys.Camera)) / 12, 4);
                    break;
                case "battery":
                    var bat = Number(Spec(p, SpecKeys.Battery));
                    score += bat > 100 ? Math.Min(bat / 1500, 4) : Math.Min(bat / 4, 4); // mAh ya da saat
                    break;
                case "light":
                    if (weight is > 0 and <= 1.5) score += 3;
                    else if (weight is > 0 and <= 1.9) score += 1.5;
                    break;
            }
        }
        return score;
    }

    /// <summary>"16 GB RAM" -> kısa neden cümlesi için öne çıkan 3-4 özellik.</summary>
    public static string KeySpecs(Product p)
    {
        var keys = p.Category switch
        {
            "Laptop" => new[] { SpecKeys.Processor, SpecKeys.Ram, SpecKeys.Storage, "Ekran Kartı" },
            "Telefon" or "Tablet" => new[] { SpecKeys.Ram, SpecKeys.Storage, SpecKeys.Camera, SpecKeys.Battery },
            "Televizyon" => new[] { SpecKeys.Screen, "Ekran Tipi", "Çözünürlük" },
            _ => new[] { "Tür", SpecKeys.Connectivity, SpecKeys.Battery },
        };
        return string.Join(", ", keys.Where(p.Specifications.ContainsKey).Take(4)
            .Select(k => k is SpecKeys.Ram or SpecKeys.Storage or SpecKeys.Battery ? $"{p.Specifications[k]} {k.ToLowerInvariant().Replace("ram", "RAM")}" : p.Specifications[k]));
    }

    private static string Spec(Product p, string key) => p.Specifications.TryGetValue(key, out var v) ? v : "";

    private static double Number(string s)
    {
        var m = Regex.Match(s, @"\d+(?:[.,]\d+)?");
        return m.Success ? double.Parse(m.Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture) : 0;
    }

    private static double RamBonus(double ram, double good, double great) => ram >= great ? 3 : ram >= good ? 1.5 : 0;
}
