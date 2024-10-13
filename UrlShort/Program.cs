using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using UrlShort.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApiDbContext>(options => options.UseSqlite(connStr));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();



app.MapPost("/shorturl", async (UrlDto url, ApiDbContext db, HttpContext ctx) =>
{
    // Validating the input URL
    if (!Uri.TryCreate(url.Url, UriKind.Absolute, out var inputUrl))
        return Results.BadRequest("Invalid URL has been provided");

   
    var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(url.Url + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")));
    var shortLink = Convert.ToBase64String(hashBytes).Substring(0, 8); // 8 char

    
    
    var sUrl = new ShortUrl()
    {
        OriginalUrl = url.Url,
        ShortenUrl = shortLink,
        ExpiryDate = DateTime.UtcNow.AddSeconds(30)
    };

    db.Urls.Add(sUrl);
    await db.SaveChangesAsync();

    var result = $"{ctx.Request.Scheme}://{ctx.Request.Host}/{sUrl.ShortenUrl}";
    
    var existingUrl = await db.Urls.FirstOrDefaultAsync(x => x.OriginalUrl == url.Url);
    if (existingUrl != null)
    {
        var existingResult = $"{ctx.Request.Scheme}://{ctx.Request.Host}/{existingUrl.ShortenUrl}";
        return Results.Ok(new UrlShortResponsDto() { Url = existingResult });
    }
    
    
    return Results.Ok(new UrlShortResponsDto()
    {
        Url = result
    });
});

app.MapFallback(async (ApiDbContext db, HttpContext ctx) =>
{
    var path = ctx.Request.Path.ToUriComponent().Trim('/');
    var urlMatch = await db.Urls.FirstOrDefaultAsync(x => x.ShortenUrl.Trim()==path.Trim());

    if (urlMatch == null || urlMatch.ExpiryDate < DateTime.UtcNow)
    {
        return Results.BadRequest("Invalid or expired short URL");
    }

    return Results.Redirect(urlMatch.OriginalUrl);
});


app.Run();

