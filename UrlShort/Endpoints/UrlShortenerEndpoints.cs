using Microsoft.EntityFrameworkCore;
using UrlShort.Models;
using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;
using Microsoft.AspNetCore.Http;

namespace UrlShort.Endpoints;

public static class UrlShortenerEndpoints
{
    private const int MaxConcurrentLinks = 100;
    private const string ConcurrencyLockKey = "urlShortenerConcurrencyLock";
    private static ILogger _logger;

    public static void MapShortenerEndPoints(this IEndpointRouteBuilder app)
    {
        var loggerFactory = app.ServiceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger("UrlShortenerEndpoints");
        app.MapPost("/shorturl", CreateShortUrl);
        app.MapFallback(RedirectToOriginalUrl);
    }

    private static async Task<IResult> CreateShortUrl(UrlDto url, ApiDbContext db, HttpContext ctx, IConnectionMultiplexer redis)
    {
        var redisDb = redis.GetDatabase();
        var clientIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
        var userKey = $"ip:{clientIp}:linkcount";
        var lockKey = $"{ConcurrencyLockKey}:{Guid.NewGuid()}";
        var lockExpiry = TimeSpan.FromSeconds(10);

        try
        {
            // Rate limiting
            var currentCount = await redisDb.StringIncrementAsync(userKey);
            if (currentCount == 1)
            {
                await redisDb.KeyExpireAsync(userKey, TimeSpan.FromMinutes(1));
            }
            if (currentCount > 100)
            {
                _logger.LogWarning("Rate limit exceeded: {Count}", currentCount);
                return Results.BadRequest("Rate limit exceeded: Max 100 links per minute.");
            }

            // Distributed locking
            if (!await AcquireLock(redisDb, lockKey, lockExpiry))
            {
                _logger.LogWarning("Failed to acquire lock. Max concurrent links limit reached.");
                return Results.StatusCode(429); // Too Many Requests
            }

            _logger.LogInformation("Attempting to short Url for: {Url}", url.Url);

            // Validating
            if (!Uri.TryCreate(url.Url, UriKind.Absolute, out var inputUrl))
            {
                _logger.LogWarning("Invalid Url provided: {Url}", url.Url);
                return Results.BadRequest("Invalid URL has been provided");
            }

            // Check if exists in Redis
            var existingUrl = await redisDb.StringGetAsync(url.Url);
            if (!existingUrl.IsNullOrEmpty)
            {
                _logger.LogInformation("Existing short Url found in Redis for: {Url}", url.Url);
                return Results.Ok(new UrlShortResponsDto { Url = existingUrl });
            }

            
            var shortLink = await GenerateUniqueShortUrl(db);

            var sUrl = new ShortUrl
            {
                OriginalUrl = url.Url,
                ShortenUrl = shortLink,
                ExpiryDate = DateTime.UtcNow.AddDays(15)
            };

            db.Urls.Add(sUrl);
            await db.SaveChangesAsync();

            var result = $"{ctx.Request.Scheme}://{ctx.Request.Host}/{sUrl.ShortenUrl}";
            await redisDb.StringSetAsync(url.Url, result, TimeSpan.FromDays(15));

            _logger.LogInformation("Created a new short URL for: {Url}", url.Url);
            return Results.Ok(new UrlShortResponsDto { Url = result });
        }
        finally
        {
            // Release the lock
            await redisDb.LockReleaseAsync(ConcurrencyLockKey, lockKey);
        }
    }

    private static async Task<bool> AcquireLock(IDatabase database, string lockKey, TimeSpan lockExpiry)
    {
        long currentCount = await database.StringIncrementAsync(ConcurrencyLockKey);
        if (currentCount <= MaxConcurrentLinks)
        {
            await database.StringSetAsync(lockKey, "locked", lockExpiry);
            return true;
        }
        else
        {
            await database.StringDecrementAsync(ConcurrencyLockKey);
            return false;
        }
    }

    private static async Task<string> GenerateUniqueShortUrl(ApiDbContext db)
    {
        while (true)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString() + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")));
            var shortLink = Convert.ToBase64String(hashBytes).Substring(0, 8);
            var urlExists = await db.Urls.AnyAsync(u => u.ShortenUrl == shortLink);
            if (!urlExists)
            {
                return shortLink;
            }
        }
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

        var urlMatch = await db.Urls.FirstOrDefaultAsync(x => x.ShortenUrl.Trim() == path.Trim());

        if (urlMatch == null || urlMatch.ExpiryDate < DateTime.UtcNow)
        {
            _logger.LogWarning("Invalid or expired short URL: {Path}", path);
            return Results.BadRequest("Invalid or expired short URL");
        }

        await redisDb.StringSetAsync(path, urlMatch.OriginalUrl, TimeSpan.FromDays(15));
        return Results.Redirect(urlMatch.OriginalUrl);
    }
}