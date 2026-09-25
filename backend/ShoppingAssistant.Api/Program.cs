using Microsoft.EntityFrameworkCore;
using ShoppingAssistant.Api.Data;
using ShoppingAssistant.Api.Import;
using ShoppingAssistant.Api.Models;
using ShoppingAssistant.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Özellikler (Dictionary) jsonb olarak saklandığı için dinamik JSON açık olmalı.
var dataSource = new Npgsql.NpgsqlDataSourceBuilder(
        builder.Configuration.GetConnectionString("DefaultConnection"))
    .EnableDynamicJson()
    .Build();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dataSource));

builder.Services.AddHttpClient<ChatService>(c => c.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddHttpClient<DummyJsonSource>();
builder.Services.AddScoped<ProductImporter>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Kullanım: dotnet run --no-launch-profile -- import dummyjson [--purge]
if (args.Length >= 2 && args[0] == "import")
{
    using var scope = app.Services.CreateScope();
    IProductSource source = args[1] switch
    {
        "dummyjson" => scope.ServiceProvider.GetRequiredService<DummyJsonSource>(),
        _ => throw new ArgumentException($"Bilinmeyen kaynak: {args[1]}"),
    };
    var (added, updated, removed) = await scope.ServiceProvider.GetRequiredService<ProductImporter>()
        .RunAsync(source, purgeOtherSources: args.Contains("--purge"), CancellationToken.None);
    Console.WriteLine($"{source.Name}: {added} eklendi, {updated} güncellendi, {removed} silindi.");
    return;
}

app.UseCors();

var webRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "web"));
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
}));

app.MapPost("/api/chat", async (ChatRequest req, AppDbContext db, ChatService chat) =>
{
    if (req.Messages is not { Count: > 0 })
        return Results.BadRequest("messages boş olamaz.");

    try
    {
        return Results.Ok(await chat.AskAsync(req.Messages, db));
    }
    catch (Exception e)
    {
        app.Logger.LogError(e, "Chat isteği başarısız");
        return Results.Problem(app.Environment.IsDevelopment() ? e.Message : "Asistan şu an cevap veremiyor.", statusCode: 502);
    }
});

app.Run();
