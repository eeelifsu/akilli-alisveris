# Akıllı Alışveriş

Yapay zekâ destekli elektronik alışveriş asistanı. Kullanıcı ihtiyacını doğal dille anlatır
("50 bin TL altı yazılım için laptop"), asistan gerçek ürün kataloğunda arayıp stoktaki en uygun
ürünleri önerir. Web sitesi ve iOS (Flutter) uygulaması aynı sunucuya bağlanır.

```
web/      düz HTML/CSS/JS site (API tarafından sunulur)
mobile/   Flutter iOS uygulaması
backend/  ASP.NET Core (.NET 8) API + PostgreSQL (EF Core)
data/     ham veri seti (git'e girmez)
```

## Nasıl çalışır?
1. `IntentParser` cümleden bütçe, kategori, marka ve kullanım amacını çıkarır (kural tabanlı).
2. Veritabanında aday ürünler aranır, `ProductRanker` puanlar. **Ürünleri kod seçer**, model uydurmaz.
3. Gemini (isteğe bağlı) seçilen gerçek ürünleri anlatan kısa metin yazar; yoksa hazır şablon kullanılır.

## Yerelde çalıştırma
```bash
docker start shopping-postgres            # PostgreSQL (port 5433)
cd backend/ShoppingAssistant.Api
dotnet run --launch-profile http          # http://localhost:5065  (isteğe bağlı: GEMINI_API_KEY=...)
```
Ürün verisi: `dotnet run --no-launch-profile -- import gadgets360 --purge` (CSV'ler `data/gadgets360/` içinde).

## Ortam değişkenleri
| Ad | Açıklama |
|---|---|
| `DATABASE_CONNECTION_STRING` | PostgreSQL adresi (`Host=...` ya da `postgresql://...`) |
| `GEMINI_API_KEY` | İsteğe bağlı, asistan metinlerini doğallaştırır |
| `AUTO_MIGRATE` | `true` ise açılışta tabloları kurar |
| `WebRoot` | Site dosyaları klasörü (Docker'da `/app/web`) |

Gizli bilgileri koda ya da git'e koyma. Yayına alma: [DEPLOY.md](DEPLOY.md).

## Veri hakkında
Ürünler Kaggle'daki **Gadgets360 Electronics Dataset**'ten alınmıştır (Ocak 2021, Hindistan pazarı).
Fiyatlar ₹'dan ₺'ye çevrilmiş, stok ve mağaza politikaları **demo** amaçlıdır.
