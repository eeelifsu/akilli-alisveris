using Microsoft.EntityFrameworkCore;
using ShoppingAssistant.Api.Data;
using ShoppingAssistant.Api.Models;
using ShoppingAssistant.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

builder.Services.AddHttpClient<ChatService>(c => c.Timeout = TimeSpan.FromSeconds(120));

var app = builder.Build();

app.MapGet("/api/products", async (AppDbContext db) =>
{
    return await db.Products.ToListAsync();
});

app.MapPost("/api/chat", async (ChatRequest req, AppDbContext db, ChatService chat) =>
{
    if (req.Messages is not { Count: > 0 })
        return Results.BadRequest("messages boş olamaz.");

    var products = await db.Products.ToListAsync();
    try
    {
        return Results.Ok(await chat.AskAsync(req.Messages, products));
    }
    catch (Exception e)
    {
        app.Logger.LogError(e, "Chat isteği başarısız");
        return Results.Problem(app.Environment.IsDevelopment() ? e.Message : "Asistan şu an cevap veremiyor.", statusCode: 502);
    }
});

app.Run();