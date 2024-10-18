using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using UrlShort.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using UrlShort.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(7158, listenOptions =>
    {
        listenOptions.UseHttps();
    });
});

// builder.WebHost.ConfigureKestrel(serverOptions =>
// {
//     serverOptions.ListenAnyIP(443, listenOptions =>
//     {
//         listenOptions.UseHttps("/app/cert.crt", "/app/cert.key");
//     });
// });


//Logging conf
builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventSourceLogger();



// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



//PostgresSql
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApiDbContext>(options => options.UseNpgsql(connStr));

//Redis
// builder.Services.AddSingleton<IConnectionMultiplexer>(
//     ConnectionMultiplexer.Connect(
//         builder.Configuration.GetValue<string>("Redis:ConnectionString"))
//     );
var redisConnectionString = builder.Configuration.GetValue<string>("Redis:ConnectionString");

var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
redisOptions.AbortOnConnectFail = false;

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisOptions));

builder.WebHost.UseUrls("http://*:80");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapShortenerEndPoints();


app.Run();

