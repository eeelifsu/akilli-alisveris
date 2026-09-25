using System.Text;
using System.Text.Json;
using ShoppingAssistant.Api.Models;

namespace ShoppingAssistant.Api.Services;

public record ChatMessage(string Role, string Content);

public record ChatRequest(List<ChatMessage> Messages);

public record ChatResponse(string Reply, List<int> ProductIds);

public class ChatService(HttpClient http, IConfiguration config)
{
    private string? GeminiKey => config["Gemini:ApiKey"] ?? config["GEMINI_API_KEY"];
    private string GeminiModel => config["Gemini:Model"] ?? "gemini-3.8-flash";
    private string OllamaUrl => config["Ollama:Url"] ?? "http://localhost:11434";
    private string OllamaModel => config["Ollama:Model"] ?? "qwen2.5:7b";

    public string ProviderName => string.IsNullOrWhiteSpace(GeminiKey) ? "ollama" : "gemini";

    public async Task<ChatResponse> AskAsync(List<ChatMessage> messages, List<Product> products)
    {
        var system = BuildSystemPrompt(products);
        var text = ProviderName == "gemini"
            ? await AskGeminiAsync(system, messages)
            : await AskOllamaAsync(system, messages);
        return Parse(text, products);
    }

    private static string BuildSystemPrompt(List<Product> products)
    {
        var catalog = string.Join("\n", products.Select(p =>
            $"- id={p.Id} | {p.Name} | {p.Brand} | {p.Category} | {p.Price} TL | stok={p.Stock} | {p.Description}"));

        return $$"""
            Sen bir alışveriş asistanısın. Kullanıcıya Türkçe, kısa ve samimi şekilde ürün öner.
            SADECE aşağıdaki katalogdaki ürünleri öner, katalogda olmayan ürün uydurma.
            Stokta olmayan (stok=0) ürünü önerme. Uygun ürün yoksa bunu dürüstçe söyle.

            MAĞAZA POLİTİKALARI (demo mağaza):
            - Kargo: 1-3 iş gününde teslim, 500 TL üzeri siparişlerde kargo ücretsiz.
            - İade: Teslimden sonra 14 gün içinde ücretsiz iade.
            - Garanti: Tüm ürünlerde 2 yıl resmi distribütör garantisi.
            Kargo, iade ve garanti sorularını bu bilgilere göre cevapla. Sipariş takibi henüz yok, sorulursa bunu söyle.

            KATALOG:
            {{catalog}}

            Cevabını SADECE şu JSON formatında ver, başka hiçbir şey yazma:
            {"reply": "kullanıcıya gösterilecek mesaj", "productIds": [önerdiğin ürünlerin id numaraları]}
            """;
    }

    private async Task<string> AskGeminiAsync(string system, List<ChatMessage> messages)
    {
        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = system } } },
            contents = messages.Select(m => new
            {
                role = m.Role == "assistant" ? "model" : "user",
                parts = new[] { new { text = m.Content } }
            }),
            generationConfig = new { responseMimeType = "application/json" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{GeminiModel}:generateContent")
        {
            Content = Json(body)
        };
        request.Headers.Add("x-goog-api-key", GeminiKey);

        using var doc = await SendAsync(request, "Gemini");
        return doc.RootElement.GetProperty("candidates")[0]
            .GetProperty("content").GetProperty("parts")[0]
            .GetProperty("text").GetString() ?? "";
    }

    private async Task<string> AskOllamaAsync(string system, List<ChatMessage> messages)
    {
        var body = new
        {
            model = OllamaModel,
            stream = false,
            format = "json",
            messages = new[] { new { role = "system", content = system } }
                .Concat(messages.Select(m => new { role = m.Role, content = m.Content }))
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{OllamaUrl}/api/chat")
        {
            Content = Json(body)
        };

        using var doc = await SendAsync(request, "Ollama");
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
    }

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

    private static ChatResponse Parse(string text, List<Product> products)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                using var doc = JsonDocument.Parse(text[start..(end + 1)]);
                var reply = doc.RootElement.GetProperty("reply").GetString() ?? "";
                var valid = products.Select(p => p.Id).ToHashSet();
                var ids = doc.RootElement.TryGetProperty("productIds", out var arr)
                    ? arr.EnumerateArray().Select(e => e.GetInt32()).Where(valid.Contains).Distinct().ToList()
                    : [];
                return new ChatResponse(reply, ids);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException) { }
        }
        return new ChatResponse(text.Trim(), []);
    }
}
