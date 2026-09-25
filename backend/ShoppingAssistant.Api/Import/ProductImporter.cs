using Microsoft.EntityFrameworkCore;
using ShoppingAssistant.Api.Data;
using ShoppingAssistant.Api.Models;

namespace ShoppingAssistant.Api.Import;

/// <summary>Bir kaynaktan çekilen ürün. Fiyat Türk Lirası olmalı.</summary>
public record ImportedProduct(
    string ExternalId,
    string Name,
    string Description,
    decimal Price,
    string Category,
    string Brand,
    int Stock,
    string? ImageUrl,
    double? Rating,
    int RatingCount,
    Dictionary<string, string> Specifications,
    string? SourceUrl,
    decimal? OriginalPrice = null,
    string? OriginalCurrency = null,
    int? ReleaseYear = null);

public interface IProductSource
{
    string Name { get; }
    Task<List<ImportedProduct>> FetchAsync(CancellationToken ct);
}

public class ProductImporter(AppDbContext db)
{
    /// <summary>Ürünleri (Source, ExternalId) üzerinden ekler ya da günceller.</summary>
    public async Task<(int added, int updated, int removed)> RunAsync(
        IProductSource source, bool purgeOtherSources, CancellationToken ct)
    {
        var items = await source.FetchAsync(ct);
        var existing = await db.Products
            .Where(p => p.Source == source.Name)
            .ToDictionaryAsync(p => p.ExternalId ?? "", ct);

        int added = 0, updated = 0;
        foreach (var i in items)
        {
            if (!existing.TryGetValue(i.ExternalId, out var p))
            {
                p = new Product { Source = source.Name, ExternalId = i.ExternalId };
                db.Products.Add(p);
                added++;
            }
            else updated++;

            p.Name = i.Name;
            p.Description = i.Description;
            p.Price = i.Price;
            p.Category = i.Category;
            p.Brand = i.Brand;
            p.Stock = i.Stock;
            p.ImageUrl = i.ImageUrl;
            p.Rating = i.Rating;
            p.RatingCount = i.RatingCount;
            p.Specifications = i.Specifications;
            p.Currency = "TRY";
            p.OriginalPrice = i.OriginalPrice;
            p.OriginalCurrency = i.OriginalCurrency;
            p.ReleaseYear = i.ReleaseYear;
            p.SourceUrl = i.SourceUrl;
        }

        var removed = 0;
        if (purgeOtherSources)
            removed = await db.Products.Where(p => p.Source != source.Name).ExecuteDeleteAsync(ct);

        await db.SaveChangesAsync(ct);
        return (added, updated, removed);
    }
}
