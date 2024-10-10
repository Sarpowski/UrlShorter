using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using UrlShort.Models;
using Microsoft.AspNetCore.Http;

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

    // Creating a short version of the provided URL
    var random = new Random();
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890@az";
    var randomStr = new string(Enumerable.Repeat(chars, 8)
        .Select(x => x[random.Next(x.Length)]).ToArray());

    var sUrl = new ShortUrl()
    {
        OriginalUrl = url.Url,
        ShortenUrl = randomStr 
    };

    db.Urls.Add(sUrl);
    await db.SaveChangesAsync();

    var result = $"{ctx.Request.Scheme}://{ctx.Request.Host}/{sUrl.ShortenUrl}";
    
    
    
    
    return Results.Ok(new UrlShortResponsDto()
    {
        Url = result
    });
});

app.MapFallback(async (ApiDbContext db, HttpContext ctx) =>
{
    var path = ctx.Request.Path.ToUriComponent().Trim('/');
    var urlMatch = await db.Urls.FirstOrDefaultAsync(x => x.ShortenUrl.Trim()==path.Trim());


    if (urlMatch == null)
    {
        return Results.BadRequest("invalid short url");
    }

    return Results.Redirect(urlMatch.OriginalUrl);
});


app.Run();


class ApiDbContext : DbContext
{
    public virtual DbSet<ShortUrl> Urls { get; set; }

    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) { }
}