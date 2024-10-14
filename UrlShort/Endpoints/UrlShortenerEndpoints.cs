using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using UrlShort.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace UrlShort.Endpoints;

public static class UrlShortenerEndpoints
{
    private static ILogger _logger;
    
    public static void MapShortenerEndPoints(this IEndpointRouteBuilder  app)
    {
        var loggerFactory = app.ServiceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger("UrlShortenerEndpoints");
        app.MapPost("/shorturl",CreateShortUrl);
        app.MapFallback(RedirectToOriginalUrl);
    }

    private static async Task<IResult> CreateShortUrl(UrlDto url, ApiDbContext db, HttpContext ctx,IConnectionMultiplexer redis)
    {
        _logger.LogInformation("Attempting to short Url for:",url.Url);
        // Validating the input URL
        if (!Uri.TryCreate(url.Url, UriKind.Absolute, out var inputUrl))
        {
            _logger.LogWarning("Invalid Url provided",url.Url);
            return Results.BadRequest("Invalid URL has been provided");
        }


        var hashBytes =
            SHA256.HashData(Encoding.UTF8.GetBytes(url.Url + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")));
        var shortLink = Convert.ToBase64String(hashBytes).Substring(0, 8); // 8 char

        var redisDb = redis.GetDatabase();
        var existingUrl = await redisDb.StringGetAsync(url.Url);

        if (!existingUrl.IsNullOrEmpty)
        {
            _logger.LogInformation("Existing short Url found in Redis for, ",url.Url);
            return Results.Ok(new UrlShortResponsDto() { Url = existingUrl });
        }
        
        var sUrl = new ShortUrl()
        {
            OriginalUrl = url.Url,
            ShortenUrl = shortLink,
            ExpiryDate = DateTime.UtcNow.AddSeconds(30)
        };
        
        db.Urls.Add(sUrl);
        await db.SaveChangesAsync();
        
        var result = $"{ctx.Request.Scheme}://{ctx.Request.Host}/{sUrl.ShortenUrl}";
        await redisDb.StringSetAsync(url.Url, result, TimeSpan.FromSeconds(30));
       
        // var existingUrl = await db.Urls.FirstOrDefaultAsync(x => x.OriginalUrl == url.Url);
        // if (existingUrl != null)
        // {
        //     var existingResult = $"{ctx.Request.Scheme}://{ctx.Request.Host}/{existingUrl.ShortenUrl}";
        //     _logger.LogInformation("Existing short URL found for ", url.Url);
        //     return Results.Ok(new UrlShortResponsDto() { Url = existingResult });
        // }
        //
        // _logger.LogInformation("Created a new short URL for ", url.Url);
        return Results.Ok(new UrlShortResponsDto()
        {
            Url = result
        });
    }

    private static async Task<IResult> RedirectToOriginalUrl(ApiDbContext db, HttpContext ctx, IConnectionMultiplexer redis)
    {
         var path = ctx.Request.Path.ToUriComponent().Trim('/');
         var redisDb = redis.GetDatabase();
         var cachedUrl = await redisDb.StringGetAsync(path);
         if (!cachedUrl.IsNullOrEmpty)
         {
             return Results.Redirect(cachedUrl);
         }
         // _logger.LogInformation("Attempting to read short URL from DataBase: ",path);
         var urlMatch = await db.Urls.FirstOrDefaultAsync(x => x.ShortenUrl.Trim()==path.Trim());
         
         if (urlMatch == null || urlMatch.ExpiryDate < DateTime.UtcNow)
         {
             _logger.LogWarning("Invalid or expired short URL: ",path);
             return Results.BadRequest("Invalid or expired short URL");
         }
         // _logger.LogInformation("Redirecting : ",path, "to ",urlMatch.OriginalUrl);
         await redisDb.StringSetAsync(path , urlMatch.OriginalUrl, TimeSpan.FromSeconds(30));
         return Results.Redirect(urlMatch.OriginalUrl);

    }
}
