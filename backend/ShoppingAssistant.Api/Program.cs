using Microsoft.EntityFrameworkCore;
using ShoppingAssistant.Api.Data;
using ShoppingAssistant.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

var app = builder.Build();

app.MapGet("/api/products", async (AppDbContext db) =>
{
    return await db.Products.ToListAsync();
});

app.Run();