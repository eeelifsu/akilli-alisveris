using Microsoft.EntityFrameworkCore;
using ShoppingAssistant.Api.Models;

namespace ShoppingAssistant.Api.Services;

public record ProductFilter(
    string? Query = null,
    string? Category = null,
    string? Brand = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool InStockOnly = false,
    string? Sort = null,
    IReadOnlyList<string>? ExcludeBrands = null);

public static class ProductQuery
{
    public static IQueryable<Product> Apply(this IQueryable<Product> q, ProductFilter f)
    {
        if (!string.IsNullOrWhiteSpace(f.Category))
            q = q.Where(p => p.Category == f.Category);
        if (!string.IsNullOrWhiteSpace(f.Brand))
            q = q.Where(p => EF.Functions.ILike(p.Brand, f.Brand));
        foreach (var excluded in f.ExcludeBrands ?? [])
        {
            var b = excluded;
            q = q.Where(p => !EF.Functions.ILike(p.Brand, b));
        }
        if (f.MinPrice is { } min) q = q.Where(p => p.Price >= min);
        if (f.MaxPrice is { } max) q = q.Where(p => p.Price <= max);
        if (f.InStockOnly) q = q.Where(p => p.Stock > 0);

        // Her kelime ürünün adında, markasında, kategorisinde ya da açıklamasında geçmeli.
        foreach (var word in (f.Query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6))
        {
            var pattern = $"%{word.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            q = q.Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                EF.Functions.ILike(p.Brand, pattern) ||
                EF.Functions.ILike(p.Category, pattern) ||
                EF.Functions.ILike(p.Description, pattern));
        }

        return f.Sort switch
        {
            "price_asc" => q.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "price_desc" => q.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            // PostgreSQL DESC sıralamada NULL'ları başa koyar; puansız ürünler sona gitsin.
            "rating" => q.OrderByDescending(p => p.Rating.HasValue).ThenByDescending(p => p.Rating)
                .ThenByDescending(p => p.RatingCount).ThenBy(p => p.Id),
            // Varsayılan: yeni ürünler önce, sonra çok değerlendirilenler.
            _ => q.OrderByDescending(p => p.ReleaseYear.HasValue).ThenByDescending(p => p.ReleaseYear)
                .ThenByDescending(p => p.RatingCount).ThenBy(p => p.Id),
        };
    }
}
