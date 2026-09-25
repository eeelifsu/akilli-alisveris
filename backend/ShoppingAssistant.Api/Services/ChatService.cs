using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ShoppingAssistant.Api.Assistant;
using ShoppingAssistant.Api.Data;
using ShoppingAssistant.Api.Models;

namespace ShoppingAssistant.Api.Services;

public record ChatMessage(string Role, string Content);

public record ChatRequest(List<ChatMessage> Messages);

/// <param name="Options">Kullanıcının tek dokunuşla gönderebileceği hızlı seçenekler.</param>
public record ChatResponse(string Reply, List<Product> Products, List<string> Options);

/// <summary>Sağlayıcı geçici olarak yoğun ya da kotası dolu.</summary>
public class ProviderBusyException(string message) : InvalidOperationException(message);

/// <summary>
/// Asistan akışı:
///  1) IntentParser cümleden bütçe/kategori/marka/amacı çıkarır (kural tabanlı, kotasız).
///  2) Veritabanında aday ürünler aranır, ProductRanker puanlar. Ürünleri kod seçer, yapay zekâ değil.
///  3) Gemini yalnızca seçilen gerçek ürünleri anlatan kısa bir metin yazar (mesaj başına 1 istek).
///     Gemini yanıt vermezse hazır şablonla cevap verilir, asistan hiç susmaz.
/// </summary>
public class ChatService(HttpClient http, IConfiguration config, ILogger<ChatService> logger)
{
    private static DateTime _geminiBlockedUntil = DateTime.MinValue;
    private static List<string>? _brands;
    private static DateTime _brandsLoadedAt;
    private static List<string>? _fallbacks;

    private string? GeminiKey => config["Gemini:ApiKey"] ?? config["GEMINI_API_KEY"];
    private string GeminiModel => config["Gemini:Model"] ?? "gemini-3.8-flash";

    private static readonly string[] Greetings =
        ["50 bin TL altı laptop", "Kablosuz kulaklık öner", "10 bin TL altı telefon", "Kargo nasıl işliyor?"];

    public async Task<ChatResponse> AskAsync(List<ChatMessage> messages, AppDbContext db)
    {
        var brands = await BrandsAsync(db);
        var userMessages = messages.Where(m => m.Role == "user").Select(m => m.Content).TakeLast(6).ToList();
        var last = userMessages.Last();
        var intent = IntentParser.Parse(userMessages, brands);

        if (intent.AsksOrderStatus)
            return new("Sipariş takibi henüz demo mağazada yok. Ürün önerisi, kargo, iade ve garanti konularında yardımcı olabilirim.",
                [], ["Kargo nasıl işliyor?", "50 bin TL altı laptop"]);

        if (intent.PolicyTopic != null)
            return new(StorePolicies.Answer(intent.PolicyTopic), [], ["İade süresi kaç gün?", "Garanti var mı?", "Ürün öner"]);

        if (intent.IsGreeting && !intent.HasSignal)
            return new("Merhaba! Ben alışveriş asistanınım. Ne aradığını ve bütçeni yaz, sana uygun ürünleri bulayım.",
                [], [.. Greetings]);

        if (!intent.HasSignal)
            return await ClarifyAsync(db);

        var (picks, note) = await SearchAsync(intent, db);
        if (picks.Count == 0 && intent.Category == null && intent.Brand == null && !intent.HasBudget)
            return await ClarifyAsync(db);
        if (picks.Count == 0)
            return new($"\"{intent.Describe()}\" kriterlerine uyan stokta ürün bulamadım. Bütçeni ya da markayı biraz esnetmeyi dener misin?",
                [], ["Daha yüksek bütçeyle göster", "En yüksek puanlıları göster"]);

        var onlyCategory = intent.Category != null && !intent.HasBudget && intent.UseCases.Count == 0
                           && intent.Brand == null && userMessages.Count == 1;

        var text = await WriteWithLlmAsync(last, intent, picks, note)
                   ?? Template(intent, picks, note, onlyCategory);
        return new(text, picks, FollowUps(intent, picks, onlyCategory));
    }

    private static async Task<ChatResponse> ClarifyAsync(AppDbContext db)
    {
        var cats = await db.Products.GroupBy(p => p.Category).OrderByDescending(g => g.Count()).Select(g => g.Key).Take(5).ToListAsync();
        return new("Tam anlayamadım. Hangi tür ürün arıyorsun? Örneğin laptop, telefon ya da kulaklık yazabilirsin; bütçeni de eklersen daha iyi seçenekler bulurum.",
            [], cats);
    }

    // ---------- Arama ----------

    private static async Task<(List<Product> picks, string? note)> SearchAsync(ShoppingIntent intent, AppDbContext db)
    {
        static async Task<List<Product>> Pool(AppDbContext db, ProductFilter f) =>
            await db.Products.AsNoTracking().Apply(f).Take(300).ToListAsync();

        var filter = new ProductFilter(intent.ModelQuery, intent.Category, intent.Brand, intent.MinPrice, intent.MaxPrice,
            InStockOnly: true, ExcludeBrands: intent.ExcludedBrands);
        var pool = await Pool(db, filter);
        string? note = null;

        if (pool.Count < 3 && intent.Brand != null)
        {
            var brand = intent.Brand;
            filter = filter with { Brand = null };
            pool = await Pool(db, filter);
            note = $"{brand} markasında yeterli ürün bulamadım, diğer markalara da baktım.";
        }
        if (pool.Count < 3 && intent.HasBudget)
        {
            filter = filter with { MaxPrice = intent.MaxPrice * 1.3m, MinPrice = intent.MinPrice * 0.75m };
            pool = await Pool(db, filter);
            note = "Bütçene tam uyan az ürün vardı, sınırı biraz esnettim.";
        }

        return (ProductRanker.Top(pool, intent, 3), note);
    }

    private static async Task<List<string>> BrandsAsync(AppDbContext db)
    {
        if (_brands == null || DateTime.UtcNow - _brandsLoadedAt > TimeSpan.FromMinutes(10))
        {
            _brands = await db.Products.Select(p => p.Brand).Distinct().Where(b => b != "Diğer").ToListAsync();
            _brandsLoadedAt = DateTime.UtcNow;
        }
        return _brands;
    }

    // ---------- Cevap metni ----------

    private static string Template(ShoppingIntent intent, List<Product> picks, string? note, bool onlyCategory)
    {
        var sb = new StringBuilder();
        var summary = intent.Describe();
        if (onlyCategory)
            sb.Append($"Tabii! Ne amaçla kullanacağını ve bütçeni de söylersen seçenekleri daraltırım. Şimdilik {intent.Category} kategorisinde en beğenilenlerden bazıları:");
        else
            sb.Append($"İstediğin kriterlere göre ({summary}) stoktaki en uygun {picks.Count} ürünü seçtim.");
        if (note != null) sb.Append(' ').Append(note);

        var top = picks[0];
        var specs = ProductRanker.KeySpecs(top);
        sb.Append($"\nİlk sırada {top.Name}: {ShoppingIntent.Tl(top.Price)}");
        if (top.Rating != null) sb.Append($", {top.Rating:0.0} puan");
        if (specs.Length > 0) sb.Append($", {specs}");
        sb.Append('.');
        return sb.ToString();
    }

    private static List<string> FollowUps(ShoppingIntent intent, List<Product> picks, bool onlyCategory)
    {
        var options = new List<string>();
        if (onlyCategory || intent.UseCases.Count == 0)
        {
            options.AddRange(intent.Category switch
            {
                "Laptop" => ["Yazılım geliştirmek için", "Oyun oynamak için", "Üniversite için", "Günlük kullanım için"],
                "Telefon" => ["Kamerası iyi olsun", "Pili uzun sürsün", "Uygun fiyatlı olsun"],
                "Televizyon" or "Kulaklık" or "Tablet" => ["Uygun fiyatlı olsun", "En yüksek puanlıları göster"],
                _ => [],
            });
        }
        if (!onlyCategory)
        {
            options.Add("Daha ucuz seçenekler göster");
            if (!intent.WantsBest) options.Add("En yüksek puanlıları göster");
            if (picks.Count > 0 && intent.ExcludedBrands.Count == 0) options.Add($"{picks[0].Brand} olmasın");
        }
        return options.Distinct().Take(4).ToList();
    }

    // ---------- Gemini: yalnızca metin yazar ----------

    private async Task<string?> WriteWithLlmAsync(string userMessage, ShoppingIntent intent, List<Product> picks, string? note)
    {
        if (string.IsNullOrWhiteSpace(GeminiKey) || DateTime.UtcNow < _geminiBlockedUntil)
            return null;

        var system = """
            Sen samimi bir elektronik alışveriş asistanısın. Türkçe yaz, en fazla 4 kısa cümle.
            KURALLAR:
            - Yalnızca verilen "urunler" listesindeki ürünlerden bahset. Ürün adı, fiyat, puan ve özellik uydurma.
            - Ürünleri neden uygun olduğunu söyleyerek sırayla öner (fiyat, puan, özellik gibi verilen verilere dayan).
            - "not" alanı varsa kullanıcıya bunu da kısaca belirt.
            - Fiyatlar Türk Lirası (₺). Kullanıcıya JSON, id ya da teknik alan adı gösterme.
            Cevabı SADECE şu JSON ile ver: {"reply": "..."}
            """;

        var payload = new JsonObject
        {
            ["istek"] = userMessage,
            ["kriterler"] = intent.Describe(),
            ["not"] = note,
            ["urunler"] = new JsonArray(picks.Select(p => (JsonNode)new JsonObject
            {
                ["ad"] = p.Name,
                ["marka"] = p.Brand,
                ["fiyat_tl"] = p.Price,
                ["puan"] = p.Rating,
                ["ozellikler"] = JsonSerializer.SerializeToNode(p.Specifications),
            }).ToArray()),
        };

        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = system }) },
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = payload.ToJsonString() }),
            }),
            ["generationConfig"] = new JsonObject { ["responseMimeType"] = "application/json" },
        };

        try
        {
            var models = new List<string> { GeminiModel };
            models.AddRange(await FallbackModelsAsync());
            foreach (var model in models.Take(4))
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post,
                        $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent")
                    {
                        Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
                    };
                    request.Headers.Add("x-goog-api-key", GeminiKey);
                    using var doc = await SendAsync(request);
                    var raw = string.Concat(doc.RootElement.GetProperty("candidates")[0].GetProperty("content")
                        .GetProperty("parts").EnumerateArray().Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString()));
                    var reply = ExtractReply(raw);
                    if (!string.IsNullOrWhiteSpace(reply)) return reply;
                }
                catch (ProviderBusyException e)
                {
                    // Kota modele özeldir: sıradaki modeli dene.
                    logger.LogWarning("{Model} yoğun/kota dolu: {Msg}", model, Short(e.Message));
                }
            }
        }
        catch (Exception e)
        {
            logger.LogWarning("Gemini kullanılamadı, şablon cevap verilecek: {Msg}", Short(e.Message));
        }

        // Kısa süre Gemini'yi bırak; kullanıcı beklemesin.
        _geminiBlockedUntil = DateTime.UtcNow.AddSeconds(90);
        logger.LogWarning("Gemini 90 sn boyunca atlanacak, şablon cevap kullanılıyor.");
        return null;
    }

    private static string? ExtractReply(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
                return doc.RootElement.GetProperty("reply").GetString()?.Trim();
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { }
        }
        return raw.Contains('{') ? null : raw.Trim();
    }

    private static string Short(string s) => s.Length <= 220 ? s : s[..220];

    private async Task<List<string>> FallbackModelsAsync()
    {
        if (_fallbacks != null) return _fallbacks;
        var list = new List<string>();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://generativelanguage.googleapis.com/v1beta/models?pageSize=200");
            request.Headers.Add("x-goog-api-key", GeminiKey);
            using var response = await http.SendAsync(request);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            list = doc.RootElement.GetProperty("models").EnumerateArray()
                .Where(m => m.GetProperty("supportedGenerationMethods").EnumerateArray().Any(x => x.GetString() == "generateContent"))
                .Select(m => m.GetProperty("name").GetString()!.Replace("models/", ""))
                .Where(n => n.Contains("flash") && !System.Text.RegularExpressions.Regex.IsMatch(n, "image|tts|live|audio|embed|exp|preview|thinking|native"))
                .OrderByDescending(n => n, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception e)
        {
            logger.LogWarning("Model listesi alınamadı: {Msg}", Short(e.Message));
        }
        foreach (var alias in new[] { "gemini-flash-latest", "gemini-flash-lite-latest" })
            if (!list.Contains(alias)) list.Add(alias);
        _fallbacks = list.Where(n => n != GeminiModel).Take(3).ToList();
        logger.LogInformation("Yedek modeller: {Models}", string.Join(", ", _fallbacks));
        return _fallbacks;
    }

    /// <summary>Tek deneme; 503'te bir kez daha dener, kota (429) hatasında beklemeden pes eder.</summary>
    private async Task<JsonDocument> SendAsync(HttpRequestMessage template)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(template.Method, template.RequestUri);
            foreach (var h in template.Headers) request.Headers.TryAddWithoutValidation(h.Key, h.Value);
            if (template.Content != null)
                request.Content = new StringContent(await template.Content.ReadAsStringAsync(), Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try { response = await http.SendAsync(request); }
            catch (HttpRequestException e) { throw new InvalidOperationException($"Gemini'ye bağlanılamadı: {e.Message}", e); }

            using (response)
            {
                var raw = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode) return JsonDocument.Parse(raw);

                var code = (int)response.StatusCode;
                if (code == 503 && attempt == 1) { await Task.Delay(2000); continue; }
                if (code is 429 or 503) throw new ProviderBusyException($"Gemini yoğun ({code}): {raw}");
                throw new InvalidOperationException($"Gemini hatası ({code}): {raw}");
            }
        }
    }
}
