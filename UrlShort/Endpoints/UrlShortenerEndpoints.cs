using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using UrlShort.Models;

namespace UrlShort.Endpoints;

public static class UrlShortenerEndpoints
{
    public static void MapShortenerEndPoints(this IEndpointRouteBuilder  app)
    {
        app.MapPost("/shorturl",CreateShortUrl);
        app.MapFallback(RedirectToOriginalUrl);
    }

    private static async Task<IResult> CreateShortUrl(UrlDto url, ApiDbContext db, HttpContext ctx)
    {

        // Validating the input URL
        if (!Uri.TryCreate(url.Url, UriKind.Absolute, out var inputUrl))
            return Results.BadRequest("Invalid URL has been provided");


        var hashBytes =
            SHA256.HashData(Encoding.UTF8.GetBytes(url.Url + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")));
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
    }

    private static async Task<IResult> RedirectToOriginalUrl(ApiDbContext db, HttpContext ctx)
    {
        var path = ctx.Request.Path.ToUriComponent().Trim('/');
        var urlMatch = await db.Urls.FirstOrDefaultAsync(x => x.ShortenUrl.Trim()==path.Trim());

        if (urlMatch == null || urlMatch.ExpiryDate < DateTime.UtcNow)
        {
            return Results.BadRequest("Invalid or expired short URL");
        }

        return Results.Redirect(urlMatch.OriginalUrl);
        
    }
}
