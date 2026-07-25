namespace UrlShortener.Infrastructure.Configurations;

public class Cors
{
    public bool AllowAnyOrigin { get; set; }

    public string[]? AllowedOrigins { get; set; }
}