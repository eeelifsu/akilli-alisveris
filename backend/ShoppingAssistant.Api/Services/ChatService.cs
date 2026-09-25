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

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, string provider)
    {
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
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"{provider} hatası ({(int)response.StatusCode}): {raw}");
            return JsonDocument.Parse(raw);
        }
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
