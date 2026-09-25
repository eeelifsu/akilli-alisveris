using Microsoft.EntityFrameworkCore;
using ShoppingAssistant.Api.Models;

namespace ShoppingAssistant.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Price).HasPrecision(12, 2);
            e.Property(p => p.OriginalPrice).HasPrecision(14, 2);
            e.Property(p => p.Currency).HasMaxLength(3);
            e.Property(p => p.OriginalCurrency).HasMaxLength(3);
            e.Property(p => p.Specifications).HasColumnType("jsonb");
            e.HasIndex(p => new { p.Source, p.ExternalId }).IsUnique();
            e.HasIndex(p => p.Category);
            e.HasIndex(p => p.Brand);
        });
    }
}