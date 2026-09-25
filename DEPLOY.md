# Yayına alma rehberi (kalıcı link)

Sonuç: `https://akilli-alisveris.onrender.com` gibi, Mac'in kapalıyken de açılan bir adres.
Hepsi **ücretsiz** ve kredi kartı istemez.

```
Mac'te kod ── GitHub ──► Render (API + web sitesi)
                              │
Mac'ten veri aktarımı ──► Neon (PostgreSQL)
```

## 1) GitHub'a yükle
1. https://github.com/new adresinde **Private** bir depo aç, adı `akilli-alisveris` olsun. "Add README" işaretleme.
2. Terminal'de (kendi kullanıcı adınla):
   ```bash
   cd ~/ai-shopping-assistant
   git remote add origin https://github.com/KULLANICI_ADIN/akilli-alisveris.git
   git branch -M main
   git push -u origin main
   ```
   Parola sorarsa GitHub şifresi değil, **Personal Access Token** iste: GitHub → Settings → Developer settings → Tokens (classic) → `repo` yetkisi.

## 2) Neon'da ücretsiz veritabanı
1. https://neon.tech adresinde giriş yap → **Create project** (bölge: Frankfurt).
2. Panelde **Connection string** kopyala. Şuna benzer:
   `postgresql://kullanici:sifre@ep-xxxx.eu-central-1.aws.neon.tech/neondb?sslmode=require`
3. Bu adres bir parola içerir: kimseyle paylaşma, GitHub'a koyma.

## 3) Ürün verisini Neon'a aktar (Mac'ten, bir kez)
Tabloları kurup 9.000 ürünü yükler. Adresi kendininkiyle değiştir:
```bash
cd ~/ai-shopping-assistant/backend/ShoppingAssistant.Api
export DATABASE_CONNECTION_STRING='postgresql://...neon.tech/neondb?sslmode=require'
dotnet run --no-launch-profile -- import gadgets360 --purge
```
Sonunda `gadgets360: 9155 eklendi` yazmalı.

## 4) Render'da servisi kur
1. https://render.com → GitHub ile giriş → **New → Blueprint** → `akilli-alisveris` deposunu seç.
   Depodaki `render.yaml` her şeyi kendisi ayarlar.
2. İstediği değişkenler:
   - `DATABASE_CONNECTION_STRING`: Neon adresi
   - `GEMINI_API_KEY`: (isteğe bağlı) boş bırakırsan asistan şablon cevaplarla çalışır, yine ürün önerir.
3. **Apply** → 5-10 dakika sonra adresin hazır. `/healthz` `{"status":"ok"}` dönmeli.

## 5) Mobil uygulamayı yeni adrese bağla
```bash
cd ~/ai-shopping-assistant/mobile
flutter build ios --release -d TELEFON_KIMLIGI --dart-define=API_URL=https://akilli-alisveris.onrender.com
```
Uygulama artık Wi-Fi'a bağlı olmadan, her yerde çalışır.

## Bilmen gerekenler
- **Uyku:** Render ücretsiz planında 15 dk kullanılmazsa uyur; ilk açılış ~50 sn sürer, sonra hızlıdır. Sunumdan önce sayfayı bir kez aç.
- **Kota:** Asistan IP başına dakikada 20 mesajla sınırlı (kötüye kullanıma karşı).
- **Gizli bilgiler** (Neon adresi, Gemini anahtarı) yalnızca Render panelinde durur; koda ya da GitHub'a girmez.
- **Veri:** Ürünler 2021 Hindistan verisidir (Gadgets360, Kaggle); demo amaçlıdır.
- **Güncelleme:** `git push` yapınca Render kendiliğinden yeniden yayınlar.
