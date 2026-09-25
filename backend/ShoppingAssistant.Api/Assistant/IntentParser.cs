using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ShoppingAssistant.Api.Assistant;

/// <summary>
/// Türkçe alışveriş cümlelerinden bütçe, kategori, marka ve kullanım amacını çıkarır.
/// Yapay zekâ kullanılmaz: kurallar sabittir, kota ya da internet gerektirmez.
/// </summary>
public static class IntentParser
{
    private static readonly (string Category, string[] Words)[] Categories =
    [
        ("Telefon", ["telefon", "iphone", "android", "smartphone", "cep tel"]),
        ("Laptop", ["laptop", "dizustu", "notebook", "bilgisayar", "macbook", "ultrabook"]),
        ("Tablet", ["tablet", "ipad"]),
        ("Kulaklık", ["kulaklik", "airpods", "earbuds", "headphone", "headset"]),
        ("Hoparlör", ["hoparlor", "speaker", "soundbar", "ses sistemi"]),
        ("Televizyon", ["televizyon", "oled", "qled", "smart tv"]),
        ("Saat", ["akilli saat", "smartwatch", "bileklik", " saat"]),
        ("Oyun Konsolu", ["konsol", "playstation", "xbox", "nintendo", " ps5", " ps4"]),
        ("Kamera", ["fotograf makinesi", "dslr", "aynasiz"]),
    ];

    private static readonly (string Case, string Pattern)[] UseCasePatterns =
    [
        ("gaming", @"\b(oyun|gaming|gamer|fps|valorant|pubg|fortnite)\b"),
        ("software", @"\b(yazilim|kod|kodlama|programlama|developer|gelistirici|programci|docker)\b"),
        ("student", @"\b(ogrenci|universite|okul|ders|lise)\b"),
        ("office", @"\b(ofis|excel|word|is icin|calisma)\b"),
        ("design", @"\b(tasarim|montaj|grafik|photoshop|render|video)\b"),
        ("camera", @"\b(kamera|kamerasi|kameralar|fotograf|selfie|cekim)\b"),
        ("battery", @"\b(pil|pili|batarya|bataryasi|sarj)\b"),
        ("light", @"\b(hafif|tasinabilir|ince)\b"),
        ("daily", @"\b(gunluk|sosyal medya|internet|film|dizi)\b"),
    ];

    private static readonly Regex Cheap = new(@"\b(ucuz|ekonomik|uygun fiyat\w*|butce dostu|daha ucuz|fiyat performans)\b", RegexOptions.Compiled);
    private static readonly Regex Cheaper = new(@"\bdaha (ucuz|uygun|dusuk)\b", RegexOptions.Compiled);
    private static readonly Regex Best = new(@"\b(en iyi|en yuksek puan\w*|en populer|en cok begenilen|en kaliteli)\b", RegexOptions.Compiled);
    private static readonly Regex Greeting = new(@"^\s*(merhaba|selam|hey|hi|hello|gunaydin|iyi gunler|iyi aksamlar|naber|nasilsin)\b", RegexOptions.Compiled);
    private static readonly Regex Policy = new(@"\b(kargo\w*|teslimat\w*|iade\w*|geri ver\w*|degisim|garanti\w*|odeme\w*|taksit\w*)\b", RegexOptions.Compiled);
    private static readonly Regex Order = new(@"\bsiparis\w*\b", RegexOptions.Compiled);

    private static readonly Regex Approx = new(@"(civar|yaklasik|dolay|around|~)", RegexOptions.Compiled);
    private static readonly Regex Below = new(@"(alti|altinda|en fazla|maksimum|max\b|kadar|gecmesin|gecmeyen|icinde|butce)", RegexOptions.Compiled);
    private static readonly Regex Above = new(@"(ustu|ustunde|uzeri|uzerinde|en az|minimum|min\b)", RegexOptions.Compiled);

    // "50 bin", "2,5 bin", "50k"
    private static readonly Regex Thousand = new(@"(\d+(?:[.,]\d+)?)\s*(?:bin|k)\b", RegexOptions.Compiled);
    // "50.000", "50000", "45000 tl" (GB/mAh gibi birimler hariç)
    private static readonly Regex Plain = new(@"\b(\d{1,3}(?:\.\d{3})+|\d{4,})\b(?!\s*(?:gb|tb|mah|mp|hz|inc|inch|w\b))", RegexOptions.Compiled);
    private static readonly Regex Range = new(@"(\d+(?:[.,]\d+)?)\s*(bin|k)?\s*(?:-|ile|ila)\s*(\d+(?:[.,]\d+)?)\s*(bin|k)?\s*(?:tl|lira)?\s*arasi", RegexOptions.Compiled);

    private static readonly HashSet<string> Stop =
    [
        "bir", "ve", "ile", "icin", "gibi", "bana", "benim", "var", "mi", "mu", "misin", "mısın", "onerir", "oner", "onerin",
        "istiyorum", "ariyorum", "lazim", "olan", "olsun", "daha", "cok", "bu", "su", "o", "de", "da", "ki", "ne", "en", "ama",
    ];

    /// <summary>Sohbetteki kullanıcı mesajlarını sırayla işler; yeni bilgi eskisinin üstüne yazar.</summary>
    public static ShoppingIntent Parse(IEnumerable<string> userMessages, IReadOnlyCollection<string> knownBrands)
    {
        var intent = new ShoppingIntent();
        foreach (var raw in userMessages)
            Apply(intent, raw, knownBrands);
        return intent;
    }

    private static void Apply(ShoppingIntent intent, string raw, IReadOnlyCollection<string> knownBrands)
    {
        var text = " " + Fold(raw) + " ";

        intent.IsGreeting = Greeting.IsMatch(text.Trim()) && text.Trim().Split(' ').Length <= 4;
        intent.AsksOrderStatus = Order.IsMatch(text);

        var range = Range.Match(text);
        var hasNumberPrice = ParseBudget(intent, text, range);

        // Fiyat sözü + kategori yok + öner/arıyorum yok => politika sorusu
        intent.PolicyTopic = Policy.IsMatch(text) && !hasNumberPrice
            ? Policy.Match(text).Value : null;

        // Kategori: değişirse önceki bütçe/marka/amaç eski konuya ait sayılır.
        var category = DetectCategory(text);
        if (category != null && category != intent.Category)
        {
            var carriesBudget = hasNumberPrice;
            intent.Category = category;
            intent.Brand = null;
            intent.ExcludedBrands.Clear();
            intent.UseCases.Clear();
            intent.WantsCheap = false;
            intent.WantsBest = false;
            intent.ModelQuery = null;
            if (!carriesBudget) { intent.MinPrice = null; intent.MaxPrice = null; }
        }

        DetectBrand(intent, raw, text, knownBrands);

        foreach (var (useCase, pattern) in UseCasePatterns)
        {
            if (!Regex.IsMatch(text, pattern)) continue;
            // "kamera" tek başına kategori olabilir; telefon/tablet/laptop ile birlikte özellik sayılır.
            if (useCase == "camera" && intent.Category == "Kamera") continue;
            intent.UseCases.Add(useCase);
        }

        if (Cheaper.IsMatch(text) && intent.MaxPrice.HasValue && !hasNumberPrice)
            intent.MaxPrice = Math.Round(intent.MaxPrice.Value * 0.75m, 0);
        if (Cheap.IsMatch(text)) intent.WantsCheap = true;
        if (Best.IsMatch(text)) intent.WantsBest = true;

        // Kategori/marka bulunamadıysa model adı olabilir: "Galaxy S20", "Redmi Note".
        if (category == null && intent.Category == null && !hasNumberPrice)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !Stop.Contains(w)).Take(4).ToList();
            intent.ModelQuery = words.Count > 0 && intent.PolicyTopic == null && !intent.IsGreeting && !intent.AsksOrderStatus
                ? string.Join(' ', words) : intent.ModelQuery;
        }
        else if (category != null) intent.ModelQuery = null;
    }

    private static bool ParseBudget(ShoppingIntent intent, string text, Match range)
    {
        if (range.Success)
        {
            var a = Amount(range.Groups[1].Value, range.Groups[2].Value.Length > 0 || range.Groups[4].Value.Length > 0 ? "bin" : "");
            var b = Amount(range.Groups[3].Value, range.Groups[4].Value.Length > 0 || range.Groups[2].Value.Length > 0 ? "bin" : "");
            intent.MinPrice = Math.Min(a, b);
            intent.MaxPrice = Math.Max(a, b);
            return true;
        }

        var found = new List<(decimal Value, int Index, int Length)>();
        foreach (Match m in Thousand.Matches(text)) found.Add((Amount(m.Groups[1].Value, "bin"), m.Index, m.Length));
        foreach (Match m in Plain.Matches(text))
        {
            if (found.Any(f => m.Index >= f.Index && m.Index < f.Index + f.Length)) continue;
            found.Add((decimal.Parse(m.Value.Replace(".", ""), CultureInfo.InvariantCulture), m.Index, m.Length));
        }
        if (found.Count == 0) return false;

        var first = found.OrderBy(f => f.Index).First();
        var start = Math.Max(0, first.Index - 20);
        var window = text.Substring(start, Math.Min(text.Length - start, first.Length + 50));

        if (Approx.IsMatch(window))
        {
            intent.MinPrice = Math.Round(first.Value * 0.8m);
            intent.MaxPrice = Math.Round(first.Value * 1.2m);
        }
        else if (Above.IsMatch(window) && !Below.IsMatch(window))
        {
            intent.MinPrice = first.Value;
            intent.MaxPrice = null;
        }
        else
        {
            intent.MaxPrice = first.Value; // "50 bin TL altı" ya da sade "50 bin TL": üst sınır
            intent.MinPrice = null;
        }
        return true;
    }

    private static decimal Amount(string number, string unit)
    {
        var n = decimal.Parse(number.Replace(',', '.'), CultureInfo.InvariantCulture);
        return unit == "bin" ? n * 1000 : n;
    }

    private static string? DetectCategory(string text)
    {
        foreach (var (category, words) in Categories)
            if (words.Any(w => text.Contains(w))) return category;
        return Regex.IsMatch(text, @"\btv\b") ? "Televizyon"
             : Regex.IsMatch(text, @"\bkamera\b") ? "Kamera"
             : null;
    }

    private static void DetectBrand(ShoppingIntent intent, string raw, string text, IReadOnlyCollection<string> knownBrands)
    {
        var matches = new List<string>();
        foreach (var brand in knownBrands)
        {
            var b = Fold(brand);
            if (b.Length < 2) continue;
            // 2-3 harfli markalar ("Mi", "Vu"...) Türkçe kelimelerle (mı, mu) karışır:
            // ancak TAM büyük harfli olanlar (LG, HP) ya da yazıldığı gibi geçenler kabul edilir.
            if (b.Length <= 3 && brand != brand.ToUpperInvariant())
            {
                if (!Regex.IsMatch(raw, $@"\b{Regex.Escape(brand)}\b")) continue;
            }
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(b)}\b")) matches.Add(brand);
        }
        // Ürün ailesinden marka: iPhone/iPad/MacBook -> Apple, Galaxy -> Samsung
        if (Regex.IsMatch(text, @"\b(iphone|ipad|macbook|airpods)\b")) AddIfKnown("Apple");
        if (Regex.IsMatch(text, @"\bgalaxy\b")) AddIfKnown("Samsung");
        if (Regex.IsMatch(text, @"\b(redmi|poco)\b")) AddIfKnown("Xiaomi");

        void AddIfKnown(string name)
        {
            var hit = knownBrands.FirstOrDefault(k => k.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (hit != null && !matches.Contains(hit)) matches.Add(hit);
        }

        foreach (var brand in matches.OrderByDescending(m => m.Length))
        {
            var b = Regex.Escape(Fold(brand));
            var excluded = Regex.IsMatch(text, $@"\b{b}\b\s+(?:markasi\s+|marka\s+)?(?:olmasin|haric|disinda|istemiyorum|olmayan)")
                        || Regex.IsMatch(text, $@"(?:haric|disinda)\s+\b{b}\b");
            if (excluded)
            {
                if (!intent.ExcludedBrands.Contains(brand)) intent.ExcludedBrands.Add(brand);
                if (intent.Brand == brand) intent.Brand = null;
            }
            else
            {
                intent.Brand = brand;
                intent.ExcludedBrands.Remove(brand);
            }
        }
    }

    /// <summary>Küçük harfe çevirir ve Türkçe karakterleri sadeleştirir: "Kulaklık" -> "kulaklik".</summary>
    public static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.ToLower(new CultureInfo("tr-TR")))
            sb.Append(c switch { 'ı' => 'i', 'ş' => 's', 'ğ' => 'g', 'ü' => 'u', 'ö' => 'o', 'ç' => 'c', '’' => '\'', _ => c });
        return sb.ToString();
    }
}
