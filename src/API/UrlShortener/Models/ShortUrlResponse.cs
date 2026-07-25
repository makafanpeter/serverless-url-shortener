namespace UrlShortener.Models
{
    /// <summary>
    /// Response payload describing a short URL and its metadata.
    /// </summary>
    public record ShortUrlResponse(
        /// <summary>The unique short code (e.g. "aB3xY7z").</summary>
        string ShortCode,

        /// <summary>The fully-qualified short URL (e.g. "https://host/aB3xY7z").</summary>
        string ShortUrl,

        /// <summary>The original long URL.</summary>
        string LongUrl,

        /// <summary>Total number of times this short URL has been followed.</summary>
        int Clicks,

        /// <summary>When this short URL was created (UTC).</summary>
        DateTimeOffset CreatedAt,

        /// <summary>When this short URL was last accessed (UTC). Zero if never accessed.</summary>
        DateTimeOffset LastAccessed
    );
}
