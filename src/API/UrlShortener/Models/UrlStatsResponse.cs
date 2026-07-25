namespace UrlShortener.Models
{
    /// <summary>
    /// Analytics snapshot for a short URL.
    /// All time-derived fields are computed server-side at query time.
    /// </summary>
    /// <param name="ShortCode">The unique short code.</param>
    /// <param name="ShortUrl">The fully-qualified short URL.</param>
    /// <param name="LongUrl">The original destination URL.</param>
    /// <param name="TotalClicks">Total number of times the short URL has been followed.</param>
    /// <param name="CreatedAt">When the short URL was created (UTC).</param>
    /// <param name="LastAccessedAt">
    /// When the short URL was last followed (UTC), or <c>null</c> if it has never been used.
    /// </param>
    /// <param name="AgeInDays">Number of full days since the short URL was created.</param>
    /// <param name="AverageClicksPerDay">
    /// Mean clicks per day over the link's lifetime.
    /// Zero if the link was created less than a day ago.
    /// </param>
    /// <param name="IsActive">
    /// <c>true</c> if the link has been accessed at least once in the last 30 days.
    /// </param>
    public record UrlStatsResponse(
        string ShortCode,
        string ShortUrl,
        string LongUrl,
        int TotalClicks,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastAccessedAt,
        int AgeInDays,
        double AverageClicksPerDay,
        bool IsActive
    );
}
