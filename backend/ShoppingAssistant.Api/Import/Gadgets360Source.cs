using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using ShoppingAssistant.Api.Models;

namespace ShoppingAssistant.Api.Import;

/// <summary>
/// Kaggle "Gadgets360 Electronics Dataset" (Ocak 2021, Hindistan pazarı, ₹ fiyatlı).
/// Dosyalar: mobiles, laptops, tablets, headphones_and_speakers, wearables, televisions, cameras, gaming_consoles.
/// Veride açıklama ve stok yoktur: açıklama özelliklerden üretilir, stok "demo" olarak atanır.
/// </summary>
public class Gadgets360Source(IConfiguration config, IHostEnvironment env) : IProductSource
{
    public string Name => "gadgets360";

    /// <summary>CSV klasörü; komutta verilmezse Import:Gadgets360Dir, o da yoksa proje kökündeki data/gadgets360.</summary>
    public string? Directory { get; set; }

    private static readonly Regex Year = new(@"\b(19|20)\d{2}\b", RegexOptions.Compiled);

    // Bir özelliğin kaynak sütunundan standart ada ve okunabilir biçime çevrilmesi.
    private record Spec(string Key, string[] Columns, Func<string, string>? Format = null);

    private record FileMap(
        string File, string UrlColumn, string PictureColumn,
        Func<Row, string> Category, Spec[] Specs, string[] DescriptionParts);

    // gaming_consoles önce gelir: bazı konsollar laptops dosyasında da yanlışlıkla geçiyor, ilk görülen kalır.
    private static readonly FileMap[] Files =
    [
        new("gaming_consoles", "url", "Picture URL", _ => "Oyun Konsolu",
        [
            new("Tür", ["Console Type"]),
            new(SpecKeys.Storage, ["Hard Disk"]),
            new(SpecKeys.Ram, ["RAM"]),
            new(SpecKeys.Processor, ["Processor"]),
            new("Ekran Kartı", ["Graphics"]),
            new("Bağlantı", ["Wi-Fi"]),
        ], ["Tür", SpecKeys.Storage, SpecKeys.Ram]),
        new("mobiles", "url", "Picture URL", _ => "Telefon",
        [
            new(SpecKeys.Ram, ["RAM"], Gb),
            new(SpecKeys.Storage, ["Internal storage"], Gb),
            new(SpecKeys.Screen, ["Screen size (inches)"], Inch),
            new(SpecKeys.Processor, ["Processor"]),
            new(SpecKeys.Camera, ["Rear camera"], Camera),
            new("Ön Kamera", ["Front camera"], Camera),
            new(SpecKeys.Battery, ["Battery capacity (mAh)"], v => $"{v} mAh"),
            new(SpecKeys.OperatingSystem, ["Operating system"]),
            new("Çözünürlük", ["Resolution"]),
            new("SIM Sayısı", ["Number of SIMs"]),
            new("NFC", ["NFC"]),
        ], [SpecKeys.Ram, SpecKeys.Storage, SpecKeys.Screen, SpecKeys.Camera, SpecKeys.Battery]),

        new("laptops", "link", "picture", _ => "Laptop",
        [
            new(SpecKeys.Ram, ["RAM"], Gb),
            new(SpecKeys.Storage, ["SSD", "Hard disk"]), // özel birleştirme aşağıda
            new(SpecKeys.Screen, ["Size"], Inch),
            new(SpecKeys.Processor, ["Processor"]),
            new("Ekran Kartı", ["Graphics Processor"]),
            new(SpecKeys.OperatingSystem, ["Operating system"]),
            new(SpecKeys.Weight, ["Weight (kg)"], v => $"{v} kg"),
            new("Çözünürlük", ["Resolution"]),
            new(SpecKeys.Battery, ["Battery Life (up to hours)"], v => $"{v} saat"),
        ], [SpecKeys.Processor, SpecKeys.Ram, SpecKeys.Storage, SpecKeys.Screen, "Ekran Kartı"]),

        new("tablets", "url", "Picture URL", _ => "Tablet",
        [
            new(SpecKeys.Ram, ["RAM"], Gb),
            new(SpecKeys.Storage, ["Internal storage"], Gb),
            new(SpecKeys.Screen, ["Screen size (inches)"], Inch),
            new(SpecKeys.Processor, ["Processor"]),
            new(SpecKeys.Camera, ["Rear camera"], Camera),
            new("Ön Kamera", ["Front camera"], Camera),
            new(SpecKeys.Battery, ["Battery capacity (mAh)"], v => $"{v} mAh"),
            new(SpecKeys.OperatingSystem, ["Operating system"]),
        ], [SpecKeys.Ram, SpecKeys.Storage, SpecKeys.Screen, SpecKeys.Battery]),

        new("headphones_and_speakers", "url", "Picture URL", HeadphoneOrSpeaker,
        [
            new("Tür", ["Headphone Type", "Type"]),
            new(SpecKeys.Connectivity, ["Connectivity"]),
            new("Bluetooth", ["Bluetooth"]),
            new("Mikrofon", ["Microphone"]),
            new("Renk", ["Colour"]),
        ], ["Tür", SpecKeys.Connectivity]),

        new("wearables", "url", "Picture URL", _ => "Saat",
        [
            new(SpecKeys.Screen, ["Display Size"]),
            new("Ekran Tipi", ["Display Type"]),
            new(SpecKeys.Battery, ["Battery Life"]),
            new("Su Geçirmezlik", ["Water Resistant"]),
            new("Nabız Ölçer", ["Heart Rate Monitor"]),
            new("Bluetooth", ["Bluetooth Version", "Bluetooth"]),
            new("Kullanıcı", ["Ideal For"]),
        ], [SpecKeys.Screen, SpecKeys.Battery, "Su Geçirmezlik", "Nabız Ölçer"]),

        new("televisions", "url", "Picture URL", _ => "Televizyon",
        [
            new(SpecKeys.Screen, ["Display Size"], v => v.Replace("inch", "inç", StringComparison.OrdinalIgnoreCase)),
            new("Ekran Tipi", ["Screen Type"]),
            new("Çözünürlük", ["Resolution Standard"]),
            new("Akıllı TV", ["Smart TV"]),
            new("HDMI Portu", ["No of HDMI Port"]),
            new("USB Portu", ["No of USB Port"]),
        ], [SpecKeys.Screen, "Ekran Tipi", "Çözünürlük", "Akıllı TV"]),

        new("cameras", "url", "Picture URL", _ => "Kamera",
        [
            new("Tür", ["Type"]),
            new("Efektif Piksel", ["Effective Pixels"]),
            new("Sensör", ["Sensor Type"]),
            new(SpecKeys.Screen, ["Display Size"]),
            new(SpecKeys.Battery, ["Battery Type"]),
            new(SpecKeys.Weight, ["Weight"]),
        ], ["Tür", "Efektif Piksel", "Sensör"]),

    ];

    public Task<List<ImportedProduct>> FetchAsync(CancellationToken ct)
    {
        var dir = Directory ?? config["Import:Gadgets360Dir"]
                  ?? Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "data", "gadgets360"));
        if (!System.IO.Directory.Exists(dir))
            throw new DirectoryNotFoundException($"Gadgets360 klasörü bulunamadı: {dir}");

        var inrTry = config.GetValue("Import:InrTry", 0.47m);
        var result = new List<ImportedProduct>();
        var seen = new HashSet<string>();
        foreach (var map in Files)
        {
            var path = Path.Combine(dir, map.File + ".csv");
            if (!File.Exists(path)) { Console.WriteLine($"Atlandı (dosya yok): {path}"); continue; }
            var items = ReadFile(path, map, inrTry).Where(i => seen.Add(i.ExternalId)).ToList();
            Console.WriteLine($"{map.File}: {items.Count} ürün");
            result.AddRange(items);
        }
        return Task.FromResult(result);
    }

    private static List<ImportedProduct> ReadFile(string path, FileMap map, decimal inrTry)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null, MissingFieldFound = null, HeaderValidated = null,
        });
        csv.Read();
        csv.ReadHeader();

        var byUrl = new Dictionary<string, Row>();
        while (csv.Read())
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < csv.HeaderRecord!.Length; i++)
                values.TryAdd(csv.HeaderRecord[i], (csv.GetField(i) ?? "").Trim());
            var row = new Row(values);
            var url = row.Get(map.UrlColumn);
            if (url.Length == 0) continue;
            // Aynı ürün birden çok satırda geçiyor: fiyatlı ve en dolu satırı tut.
            if (!byUrl.TryGetValue(url, out var best) || row.Score() > best.Score())
                byUrl[url] = row;
        }

        var list = new List<ImportedProduct>();
        foreach (var (url, row) in byUrl)
        {
            var inr = ParsePrice(row.Get("Price in India"));
            if (inr is null or <= 0) continue; // fiyatsız ürün alışverişte işe yaramaz

            var brand = row.Get("Brand");
            var model = row.Get("Model");
            var productName = row.Get("Product Name");
            var name = productName.Length > 0 ? productName : (model.StartsWith(brand, StringComparison.OrdinalIgnoreCase) ? model : $"{brand} {model}").Trim();
            if (name.Length == 0) continue;

            var specs = BuildSpecs(map, row);
            var (rating, count) = Rating(row);
            var year = Year.Match(row.Get("Launched")) is { Success: true } m ? int.Parse(m.Value) : (int?)null;

            list.Add(new ImportedProduct(
                ExternalId: url,
                Name: name,
                Description: Describe(name, map, specs),
                Price: ToTry(inr.Value, inrTry),
                Category: map.Category(row),
                Brand: brand.Length > 0 ? brand : "Diğer",
                Stock: DemoStock(url),
                ImageUrl: row.Get(map.PictureColumn) is { Length: > 0 } pic ? pic : null,
                Rating: rating,
                RatingCount: count,
                Specifications: specs,
                SourceUrl: url,
                OriginalPrice: inr,
                OriginalCurrency: "INR",
                ReleaseYear: year));
        }
        return list;
    }

    private static Dictionary<string, string> BuildSpecs(FileMap map, Row row)
    {
        var specs = new Dictionary<string, string>();
        foreach (var spec in map.Specs)
        {
            string? value;
            if (map.File == "laptops" && spec.Key == SpecKeys.Storage)
            {
                // SSD ve HDD birlikte olabilir: "512 GB SSD + 1 TB HDD"
                var parts = new List<string>();
                if (Real(row.Get("SSD")) is { } ssd) parts.Add($"{Gb(ssd)} SSD");
                if (Real(row.Get("Hard disk")) is { } hdd) parts.Add($"{Gb(hdd)} HDD");
                value = parts.Count > 0 ? string.Join(" + ", parts) : null;
            }
            else
            {
                value = spec.Columns.Select(c => Real(row.Get(c))).FirstOrDefault(v => v != null);
                if (value != null && spec.Format != null) value = spec.Format(value);
            }
            if (!string.IsNullOrWhiteSpace(value))
                specs[spec.Key] = value.Length > 90 ? value[..90] : value;
        }
        return specs;
    }

    /// <summary>Özelliklerden kısa, okunabilir bir Türkçe özet üretir (veride açıklama yok).</summary>
    private static string Describe(string name, FileMap map, Dictionary<string, string> specs)
    {
        var parts = map.DescriptionParts
            .Where(specs.ContainsKey)
            .Select(k => k is SpecKeys.Ram or SpecKeys.Storage or SpecKeys.Battery or SpecKeys.Screen
                ? $"{specs[k]} {(k == SpecKeys.Ram ? k : k.ToLowerInvariant())}"
                : specs[k]);
        var text = string.Join(", ", parts);
        return text.Length > 0 ? $"{name}: {text}." : name;
    }

    private static (double? rating, int count) Rating(Row row)
    {
        int total = 0, weighted = 0;
        for (var star = 1; star <= 5; star++)
        {
            var n = int.TryParse(row.Get($"{star} Stars").Replace(",", ""), out var v) ? v : 0;
            total += n;
            weighted += n * star;
        }
        return total > 0 ? (Math.Round((double)weighted / total, 1), total) : (null, 0);
    }

    private static string HeadphoneOrSpeaker(Row row)
    {
        var text = $"{row.Get("Product Name")} {row.Get("Model")} {row.Get("Type")} {row.Get("Headphone Type")}";
        if (Regex.IsMatch(text, "speaker|soundbar|home theat|woofer|boombox|subwoofer", RegexOptions.IgnoreCase))
            return "Hoparlör";
        return "Kulaklık";
    }

    private static decimal? ParsePrice(string s)
    {
        s = s.Replace("₹", "").Replace(",", "").Trim();
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static decimal ToTry(decimal inr, decimal rate)
    {
        var x = inr * rate;
        return x >= 100 ? Math.Round(x / 10m) * 10m - 1m : Math.Max(Math.Round(x), 1m);
    }

    /// <summary>
    /// Veri setinde stok yok. Demo mağaza envanteri: üründen türetilen sabit bir sayı
    /// (yaklaşık %8'i tükendi). Aynı ürün her içe aktarmada aynı stoğu alır.
    /// </summary>
    private static int DemoStock(string key)
    {
        uint h = 2166136261;
        foreach (var c in key) h = (h ^ c) * 16777619;
        var v = (int)(h % 100);
        return v < 8 ? 0 : 3 + (v * 7) % 58;
    }

    private static string? Real(string v) =>
        string.IsNullOrWhiteSpace(v) || v.Equals("No", StringComparison.OrdinalIgnoreCase) || v.Equals("NaN", StringComparison.OrdinalIgnoreCase) ? null : v.Trim();

    private static string Gb(string v) => Regex.Replace(v, @"(\d)\s*(GB|TB|MB)", "$1 $2", RegexOptions.IgnoreCase);

    private static string Inch(string v)
    {
        var m = Regex.Match(v, @"\d+(\.\d+)?");
        return m.Success ? $"{decimal.Parse(m.Value, CultureInfo.InvariantCulture):0.##} inç" : v;
    }

    private static string Camera(string v) =>
        Regex.Replace(v, @"-?\s*megapixel", " MP", RegexOptions.IgnoreCase);

    /// <summary>Büyük/küçük harfe duyarsız sütun erişimi olan bir CSV satırı.</summary>
    private class Row(Dictionary<string, string> values)
    {
        public string Get(string column) => values.TryGetValue(column, out var v) ? v : "";

        /// <summary>Satırın doluluğu: fiyatlı satır her zaman öndedir.</summary>
        public int Score() => (Get("Price in India").Length > 0 ? 1000 : 0) + values.Values.Count(v => v.Length > 0);
    }
}
