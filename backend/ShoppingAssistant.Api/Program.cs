using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using ShoppingAssistant.Api.Data;
using ShoppingAssistant.Api.Import;
using ShoppingAssistant.Api.Models;
using ShoppingAssistant.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Özellikler (Dictionary) jsonb olarak saklandığı için dinamik JSON açık olmalı.
// Bağlantı: DATABASE_CONNECTION_STRING ortam değişkeni (Neon/Render "postgresql://..." adresi de olur)
// yoksa appsettings.json içindeki yerel geliştirme bağlantısı kullanılır.
var connectionString = NormalizeConnectionString(
    builder.Configuration["DATABASE_CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Veritabanı bağlantısı yok: DATABASE_CONNECTION_STRING ayarla."));
var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString)
    .EnableDynamicJson()
    .Build();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dataSource));

builder.Services.AddHttpClient<ChatService>(c => c.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddHttpClient<DummyJsonSource>();
builder.Services.AddScoped<Gadgets360Source>();
builder.Services.AddScoped<ProductImporter>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Asistan Gemini kotasını ve sunucuyu korumak için IP başına dakikada 20 mesaj.
    o.AddPolicy("chat", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Render gibi ters vekil (proxy) arkasında gerçek istemci IP'sini al.
var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

// Kullanım: dotnet run --no-launch-profile -- import <dummyjson|gadgets360> [klasör] [--purge]
if (args.Length >= 2 && args[0] == "import")
{
    using var scope = app.Services.CreateScope();
    // Boş (bulut) veritabanında önce tabloları kur.
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    IProductSource source = args[1] switch
    {
        "dummyjson" => scope.ServiceProvider.GetRequiredService<DummyJsonSource>(),
        "gadgets360" => Gadgets360(scope.ServiceProvider, args),
        _ => throw new ArgumentException($"Bilinmeyen kaynak: {args[1]}"),
    };
    var (added, updated, removed) = await scope.ServiceProvider.GetRequiredService<ProductImporter>()
        .RunAsync(source, purgeOtherSources: args.Contains("--purge"), CancellationToken.None);
    Console.WriteLine($"{source.Name}: {added} eklendi, {updated} güncellendi, {removed} silindi.");
    return;
}

// Bulutta veritabanı boşsa tabloları kendisi kurar (AUTO_MIGRATE=true).
if (app.Configuration.GetValue<bool>("AUTO_MIGRATE"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseCors();
app.UseRateLimiter();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Site dosyaları: WebRoot ayarı (Docker'da /app/web) ya da geliştirmede proje kökündeki web/.
var webRoot = Path.GetFullPath(app.Configuration["WebRoot"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "web"));
if (Directory.Exists(webRoot))
{
    var files = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}

// Arama + filtre + sayfalama. Örn: /api/products?q=laptop&category=Laptop&maxPrice=30000&sort=price_asc&page=1
app.MapGet("/api/products", async (
    AppDbContext db, string? q, string? category, string? brand,
    decimal? minPrice, decimal? maxPrice, bool? inStock, string? sort,
    int? page, int? pageSize) =>
{
    // Girdi doğrulama: aşırı uzun değerleri kırp, sıralamayı bilinenlerle sınırla.
    q = q?.Length > 100 ? q[..100] : q;
    category = category?.Length > 40 ? null : category;
    brand = brand?.Length > 40 ? null : brand;
    sort = sort is "price_asc" or "price_desc" or "rating" ? sort : null;
    var size = Math.Clamp(pageSize ?? 24, 1, 100);
    var pageNo = Math.Max(page ?? 1, 1);
    var query = db.Products.AsNoTracking()
        .Apply(new ProductFilter(q, category, brand, minPrice, maxPrice, inStock ?? false, sort));

    var total = await query.CountAsync();
    var items = await query.Skip((pageNo - 1) * size).Take(size).ToListAsync();
    return Results.Ok(new { items, total, page = pageNo, pageSize = size });
});

app.MapGet("/api/products/{id:int}", async (AppDbContext db, int id) =>
    await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id) is { } p
        ? Results.Ok(p)
        : Results.NotFound());

// Filtre çipleri için kategori ve marka listesi.
app.MapGet("/api/facets", async (AppDbContext db) => Results.Ok(new
{
    categories = await db.Products.GroupBy(p => p.Category)
        .Select(g => new { name = g.Key, count = g.Count() }).OrderByDescending(x => x.count).ToListAsync(),
    brands = await db.Products.GroupBy(p => p.Brand)
        .Select(g => new { name = g.Key, count = g.Count() }).OrderByDescending(x => x.count).Take(50).ToListAsync(),
    total = await db.Products.CountAsync(),
    brandCount = await db.Products.Select(p => p.Brand).Distinct().CountAsync(),
}));

app.MapPost("/api/chat", async (ChatRequest req, AppDbContext db, ChatService chat) =>
{
    if (req.Messages is not { Count: > 0 })
        return Results.BadRequest("messages boş olamaz.");
    if (req.Messages.Any(m => m.Content is null || m.Content.Length > 1000 || (m.Role != "user" && m.Role != "assistant")))
        return Results.BadRequest("Geçersiz mesaj.");
    if (req.Messages[^1].Role != "user")
        return Results.BadRequest("Son mesaj kullanıcıdan olmalı.");
    req = req with { Messages = req.Messages.TakeLast(12).ToList() };

    try
    {
        return Results.Ok(await chat.AskAsync(req.Messages, db));
    }
    catch (ProviderBusyException e)
    {
        app.Logger.LogWarning("Asistan yoğun: {Msg}", e.Message);
        return Results.Problem(
            app.Environment.IsDevelopment() ? e.Message : "Asistan şu an yoğun, birkaç saniye sonra tekrar dene.",
            statusCode: 503);
    }
    catch (Exception e)
    {
        app.Logger.LogError(e, "Chat isteği başarısız");
        return Results.Problem(app.Environment.IsDevelopment() ? e.Message : "Asistan şu an cevap veremiyor.", statusCode: 502);
    }
}).RequireRateLimiting("chat");

app.Run();

static Gadgets360Source Gadgets360(IServiceProvider sp, string[] args)
{
    var source = sp.GetRequiredService<Gadgets360Source>();
    source.Directory = args.Skip(2).FirstOrDefault(a => !a.StartsWith("--"));
    return source;
}

/// <summary>"postgresql://kullanici:sifre@host/db?sslmode=require" adresini Npgsql biçimine çevirir.</summary>
static string NormalizeConnectionString(string value)
{
    if (!value.StartsWith("postgres://") && !value.StartsWith("postgresql://"))
        return value;
    var uri = new Uri(value);
    var user = uri.UserInfo.Split(':', 2);
    return new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(user[0]),
        Password = user.Length > 1 ? Uri.UnescapeDataString(user[1]) : "",
        // Bulut veritabanları SSL ister; yerel denemede ?sslmode=disable verilebilir.
        SslMode = uri.Query.Contains("sslmode=disable") ? Npgsql.SslMode.Disable : Npgsql.SslMode.Require,
    }.ConnectionString;
}
