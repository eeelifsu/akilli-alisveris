using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ShoppingAssistant.Api.Data;
using ShoppingAssistant.Api.Models;

namespace ShoppingAssistant.Api.Services;

public record ChatMessage(string Role, string Content);

public record ChatRequest(List<ChatMessage> Messages);

public record ChatResponse(string Reply, List<Product> Products);

public class ChatService(HttpClient http, IConfiguration config)
{
    private const int MaxToolRounds = 4;

    private string? GeminiKey => config["Gemini:ApiKey"] ?? config["GEMINI_API_KEY"];
    private string GeminiModel => config["Gemini:Model"] ?? "gemini-3.8-flash";
    private string OllamaUrl => config["Ollama:Url"] ?? "http://localhost:11434";
    private string OllamaModel => config["Ollama:Model"] ?? "qwen2.5:7b";

    public string ProviderName => string.IsNullOrWhiteSpace(GeminiKey) ? "ollama" : "gemini";

    public async Task<ChatResponse> AskAsync(List<ChatMessage> messages, AppDbContext db)
    {
        var categories = await db.Products.Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync();
        return ProviderName == "gemini"
            ? await AskGeminiAsync(messages, db, categories)
            : await AskOllamaAsync(messages, db, categories);
    }

    private static string SystemPrompt(IEnumerable<string> categories) => $$"""
        Sen bir elektronik alışveriş asistanısın. Kullanıcıya Türkçe, kısa ve samimi şekilde ürün öner.

        KURALLAR:
        - Ürün önermeden önce MUTLAKA search_products aracıyla mağaza kataloğunda ara. Katalogda olmayan ürün uydurma.
        - Fiyatlar Türk Lirası (TL). Bütçe belirtilirse max_price ver.
        - Stokta olmayan ürünü önerme. Uygun ürün bulamazsan bunu dürüstçe söyle, gerekirse kriterleri gevşetip tekrar ara.
        - En fazla 3 ürün öner. Her ürün için neden uygun olduğunu bir cümleyle söyle (fiyat, puan, marka gibi verilere dayanarak).
        - Selamlaşma ya da genel sohbette arama yapman gerekmez.

        Kategoriler: {{string.Join(", ", categories)}}

        MAĞAZA POLİTİKALARI (demo mağaza):
        - Kargo: 1-3 iş gününde teslim, 500 TL üzeri siparişlerde kargo ücretsiz.
        - İade: Teslimden sonra 14 gün içinde ücretsiz iade.
        - Garanti: Tüm ürünlerde 2 yıl resmi distribütör garantisi.
        Kargo, iade ve garanti sorularını bu bilgilere göre cevapla. Sipariş takibi henüz yok, sorulursa bunu söyle.

        Nihai cevabını SADECE şu JSON formatında ver, başka hiçbir şey yazma:
        {"reply": "kullanıcıya gösterilecek mesaj", "productIds": [önerdiğin ürünlerin id numaraları]}
        """;

    // ---------- Gemini (araç çağırma ile) ----------

    private async Task<ChatResponse> AskGeminiAsync(List<ChatMessage> messages, AppDbContext db, List<string> categories)
    {
        var contents = new JsonArray();
        foreach (var m in messages)
        {
            contents.Add(new JsonObject
            {
                ["role"] = m.Role == "assistant" ? "model" : "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = m.Content }),
            });
        }

        var tools = new JsonArray(new JsonObject
        {
            ["functionDeclarations"] = new JsonArray(new JsonObject
            {
                ["name"] = "search_products",
                ["description"] = "Mağaza kataloğunda ürün arar ve en uygun sonuçları döner.",
                ["parameters"] = new JsonObject
                {
                    ["type"] = "OBJECT",
                    ["properties"] = new JsonObject
                    {
                        ["query"] = new JsonObject { ["type"] = "STRING", ["description"] = "Aranacak kelimeler (ürün adı, marka, özellik). Boş bırakılabilir." },
                        ["category"] = new JsonObject
                        {
                            ["type"] = "STRING",
                            ["description"] = "Kategori filtresi.",
                            ["enum"] = new JsonArray(categories.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray()),
                        },
                        ["brand"] = new JsonObject { ["type"] = "STRING", ["description"] = "Marka filtresi, örn. Apple." },
                        ["min_price"] = new JsonObject { ["type"] = "NUMBER", ["description"] = "Asgari fiyat (TL)." },
                        ["max_price"] = new JsonObject { ["type"] = "NUMBER", ["description"] = "Azami fiyat (TL)." },
                        ["sort"] = new JsonObject
                        {
                            ["type"] = "STRING",
                            ["enum"] = new JsonArray("relevance", "price_asc", "price_desc", "rating"),
                        },
                    },
                },
            }),
        });

        var seen = new Dictionary<int, Product>();

        for (var round = 0; round <= MaxToolRounds; round++)
        {
            var body = new JsonObject
            {
                ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = SystemPrompt(categories) }) },
                ["contents"] = contents.DeepClone(),
                ["tools"] = tools.DeepClone(),
            };

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{GeminiModel}:generateContent")
            {
                Content = Json(body),
            };
            request.Headers.Add("x-goog-api-key", GeminiKey);

            using var doc = await SendAsync(request, "Gemini");
            var content = doc.RootElement.GetProperty("candidates")[0].GetProperty("content");
            var parts = content.GetProperty("parts").EnumerateArray().ToList();
            var calls = parts.Where(p => p.TryGetProperty("functionCall", out _)).ToList();

            if (calls.Count == 0 || round == MaxToolRounds)
            {
                var text = string.Concat(parts.Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString()));
                return Parse(text, seen);
            }

            // Modelin çağrısını (imzalarıyla birlikte) aynen geçmişe ekle, sonucu cevap olarak ver.
            contents.Add(JsonNode.Parse(content.GetRawText()));
            var responses = new JsonArray();
            foreach (var call in calls)
            {
                var fc = call.GetProperty("functionCall");
                var found = await SearchAsync(db, fc.TryGetProperty("args", out var a) ? a : default);
                foreach (var p in found) seen[p.Id] = p;
                responses.Add(new JsonObject
                {
                    ["functionResponse"] = new JsonObject
                    {
                        ["name"] = fc.GetProperty("name").GetString(),
                        ["response"] = new JsonObject { ["products"] = ToToolResult(found) },
                    },
                });
            }
            contents.Add(new JsonObject { ["role"] = "user", ["parts"] = responses });
        }

        return new ChatResponse("Şu an sana cevap veremiyorum, tekrar dener misin?", []);
    }

    private static async Task<List<Product>> SearchAsync(AppDbContext db, JsonElement args)
    {
        string? Str(string k) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        decimal? Num(string k) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

        var filter = new ProductFilter(Str("query"), Str("category"), Str("brand"), Num("min_price"), Num("max_price"),
            InStockOnly: true, Sort: Str("sort"));
        return await db.Products.AsNoTracking().Apply(filter).Take(8).ToListAsync();
    }

    private static JsonArray ToToolResult(List<Product> products) => new(products.Select(p => (JsonNode)new JsonObject
    {
        ["id"] = p.Id,
        ["name"] = p.Name,
        ["brand"] = p.Brand,
        ["category"] = p.Category,
        ["price_try"] = p.Price,
        ["stock"] = p.Stock,
        ["rating"] = p.Rating,
        ["description"] = p.Description,
        ["specs"] = JsonSerializer.SerializeToNode(p.Specifications),
    }).ToArray());

    // ---------- Ollama (araç çağırma yok: önce ara, sonuçları isteme ekle) ----------

    private async Task<ChatResponse> AskOllamaAsync(List<ChatMessage> messages, AppDbContext db, List<string> categories)
    {
        var lastUser = messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
        var candidates = await db.Products.AsNoTracking().Apply(new ProductFilter(lastUser, InStockOnly: true)).Take(15).ToListAsync();
        if (candidates.Count == 0)
            candidates = await db.Products.AsNoTracking().Apply(new ProductFilter(InStockOnly: true, Sort: "rating")).Take(15).ToListAsync();

        var catalog = string.Join("\n", candidates.Select(p =>
            $"- id={p.Id} | {p.Name} | {p.Brand} | {p.Category} | {p.Price} TL | puan={p.Rating} | {p.Description}"));
        var system = SystemPrompt(categories)
            .Replace("MUTLAKA search_products aracıyla mağaza kataloğunda ara", "aşağıdaki katalogdan seç")
            + $"\n\nKATALOG:\n{catalog}";

        var body = new
        {
            model = OllamaModel,
            stream = false,
            format = "json",
            messages = new[] { new { role = "system", content = system } }
                .Concat(messages.Select(m => new { role = m.Role, content = m.Content })),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{OllamaUrl}/api/chat") { Content = Json(body) };
        using var doc = await SendAsync(request, "Ollama");
        var text = doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
        return Parse(text, candidates.ToDictionary(p => p.Id));
    }

    // ---------- Ortak ----------

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private async Task<JsonDocument> SendAsync(HttpRequestMessage template, string provider)
    {
        // Geçici hatalarda (429/503) kısa beklemelerle 3 kez dene.
        for (var attempt = 1; ; attempt++)
        {
            using var request = await CloneAsync(template);
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request);
            }
            catch (HttpRequestException e)
            {
                throw new InvalidOperationException($"{provider}'a bağlanılamadı: {e.Message}", e);
            }

            using (response)
            {
                var raw = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return JsonDocument.Parse(raw);

                var transient = (int)response.StatusCode is 429 or 503;
                if (!transient || attempt == 3)
                    throw new InvalidOperationException($"{provider} hatası ({(int)response.StatusCode}): {raw}");
            }
            await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage src)
    {
        var copy = new HttpRequestMessage(src.Method, src.RequestUri);
        foreach (var h in src.Headers) copy.Headers.TryAddWithoutValidation(h.Key, h.Value);
        if (src.Content != null)
            copy.Content = new StringContent(await src.Content.ReadAsStringAsync(), Encoding.UTF8, "application/json");
        return copy;
    }

    /// <summary>Modelin JSON cevabını çözer; önerilen id'lerden yalnızca gerçekten aranıp bulunanları döner.</summary>
    private static ChatResponse Parse(string text, Dictionary<int, Product> seen)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                using var doc = JsonDocument.Parse(text[start..(end + 1)]);
                var reply = doc.RootElement.GetProperty("reply").GetString() ?? "";
                var products = doc.RootElement.TryGetProperty("productIds", out var arr)
                    ? arr.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.Number)
                        .Select(e => e.GetInt32())
                        .Distinct()
                        .Where(seen.ContainsKey)
                        .Select(id => seen[id])
                        .ToList()
                    : [];
                return new ChatResponse(reply, products);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException) { }
        }
        return new ChatResponse(text.Trim(), []);
    }
}
