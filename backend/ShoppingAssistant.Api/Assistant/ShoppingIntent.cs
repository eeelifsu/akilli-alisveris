namespace ShoppingAssistant.Api.Assistant;

/// <summary>Kullanıcının cümlelerinden çıkarılan alışveriş ihtiyacı.</summary>
public class ShoppingIntent
{
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public List<string> ExcludedBrands { get; } = [];
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public HashSet<string> UseCases { get; } = [];
    public bool WantsCheap { get; set; }
    public bool WantsBest { get; set; }
    public string? PolicyTopic { get; set; }
    public bool AsksOrderStatus { get; set; }
    public bool IsGreeting { get; set; }
    public string? ModelQuery { get; set; }

    public bool HasBudget => MinPrice.HasValue || MaxPrice.HasValue;
    public bool HasSignal => Category != null || Brand != null || HasBudget || ModelQuery != null;

    /// <summary>"Laptop, 50.000 ₺ altı, yazılım geliştirme" gibi kısa özet.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Brand != null) parts.Add(Brand);
        if (Category != null) parts.Add(Category);
        if (MinPrice.HasValue && MaxPrice.HasValue)
            parts.Add($"{Tl(MinPrice.Value)} - {Tl(MaxPrice.Value)} arası");
        else if (MaxPrice.HasValue) parts.Add($"{Tl(MaxPrice.Value)} altı");
        else if (MinPrice.HasValue) parts.Add($"{Tl(MinPrice.Value)} üstü");
        foreach (var u in UseCases)
            parts.Add(UseCaseNames.TryGetValue(u, out var n) ? n : u);
        if (ExcludedBrands.Count > 0) parts.Add($"{string.Join(", ", ExcludedBrands)} hariç");
        return string.Join(", ", parts);
    }

    public static string Tl(decimal v) => $"{v:N0} ₺".Replace(",", ".");

    public static readonly Dictionary<string, string> UseCaseNames = new()
    {
        ["gaming"] = "oyun",
        ["software"] = "yazılım geliştirme",
        ["student"] = "öğrenci kullanımı",
        ["office"] = "ofis işleri",
        ["design"] = "tasarım/video",
        ["camera"] = "iyi kamera",
        ["battery"] = "uzun pil ömrü",
        ["light"] = "hafiflik",
        ["daily"] = "günlük kullanım",
    };
}
