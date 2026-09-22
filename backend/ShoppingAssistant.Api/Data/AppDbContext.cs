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
}