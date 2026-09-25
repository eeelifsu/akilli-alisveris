namespace ShoppingAssistant.Api.Assistant;

/// <summary>Demo mağaza politikaları. Asistan bu bilgileri kendi bilgisinden üretmez, buradan okur.</summary>
public static class StorePolicies
{
    public const string Note = "(Bu bir demo mağazadır, politikalar örnek amaçlıdır.)";

    public static string Answer(string? topic)
    {
        topic = topic == null ? "" : IntentParser.Fold(topic);
        var text = topic switch
        {
            var t when t.StartsWith("iade") || t.StartsWith("geri") || t.StartsWith("degisim") =>
                "İade: Teslimden sonra 14 gün içinde, kullanılmamış ürünleri ücretsiz iade edebilirsin.",
            var t when t.StartsWith("garanti") =>
                "Garanti: Tüm ürünlerde 2 yıl resmi distribütör garantisi var.",
            var t when t.StartsWith("kargo") || t.StartsWith("teslimat") =>
                "Kargo: Siparişler 1-3 iş gününde teslim edilir. 500 ₺ üzeri siparişlerde kargo ücretsiz.",
            var t when t.StartsWith("odeme") || t.StartsWith("taksit") =>
                "Ödeme: Şu an demo mağazada gerçek ödeme yok, sipariş akışı deneme amaçlı.",
            _ => "Kargo: 1-3 iş gününde teslim, 500 ₺ üzeri ücretsiz.\nİade: Teslimden sonra 14 gün içinde ücretsiz.\nGaranti: Tüm ürünlerde 2 yıl.",
        };
        return $"{text}\n{Note}";
    }
}
