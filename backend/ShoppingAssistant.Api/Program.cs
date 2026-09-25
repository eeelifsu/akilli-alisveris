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

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

var webRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "web"));
if (Directory.Exists(webRoot))
{
    var files = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}

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