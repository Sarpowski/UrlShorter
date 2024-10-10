namespace UrlShort.Models;

public class ShortUrl
{
    public int Id { get; set; }
    public string OriginalUrl { get; set; } = "";
    public string ShortenUrl { get; set; } = "";
}