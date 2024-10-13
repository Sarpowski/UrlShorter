using Microsoft.EntityFrameworkCore;
using UrlShort.Models;

 
class ApiDbContext : DbContext
{
    public virtual DbSet<ShortUrl> Urls { get; set; }

    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) { }
}