using Microsoft.AspNetCore.Mvc;
using UrlShortener.Exceptions;
using UrlShortener.Models;
using UrlShortener.Persistence.Entities;
using UrlShortener.Services;

namespace UrlShortener.Controllers
{
    /// <summary>
    /// Provides analytics and monitoring statistics for short URLs.
    /// </summary>
    [ApiController]
    [Route("stats")]
    [Produces("application/json")]
    public class StatsController : ControllerBase
    {
        private readonly IUrlShortenerService _service;

        public StatsController(IUrlShortenerService service)
        {
            _service = service;
        }

        /// <summary>
        /// Returns analytics for a short URL: click counts, creation date,
        /// last access time, average clicks per day, and activity status.
        /// </summary>
        /// <param name="code">The short code to query.</param>
        /// <response code="200">Analytics returned successfully.</response>
        /// <response code="404">Short code not found.</response>
        [HttpGet("{code}")]
        [ProducesResponseType(typeof(UrlStatsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStats(string code)
        {
            var record = await _service.GetAsync(code)
                ?? throw new NotFoundException($"Short code '{code}' not found.");

            return Ok(BuildStats(record, BuildBaseUrl()));
        }

        // ------------------------------------------------------------------ helpers

        private string BuildBaseUrl()
        {
            var req = HttpContext.Request;
            return $"{req.Scheme}://{req.Host}{(req.PathBase.HasValue ? req.PathBase.Value : string.Empty)}";
        }

        private static UrlStatsResponse BuildStats(UrlRecord record, string baseUrl)
        {
            var now       = DateTimeOffset.UtcNow;
            var createdAt = DateTimeOffset.FromUnixTimeSeconds(record.CreatedAt);

            var lastAccessedAt = record.LastAccessed == 0
                ? (DateTimeOffset?)null
                : DateTimeOffset.FromUnixTimeSeconds(record.LastAccessed);

            var ageInDays = (int)(now - createdAt).TotalDays;

            // Avoid division-by-zero for links created less than a day ago.
            var avgClicksPerDay = ageInDays > 0
                ? Math.Round((double)record.Clicks / ageInDays, 2)
                : 0.0;

            // Active = accessed at least once within the last 30 days.
            var isActive = lastAccessedAt.HasValue
                && (now - lastAccessedAt.Value).TotalDays <= 30;

            return new UrlStatsResponse(
                ShortCode:         record.ShortCode,
                ShortUrl:          $"{baseUrl}/{record.ShortCode}",
                LongUrl:           record.LongUrl,
                TotalClicks:       record.Clicks,
                CreatedAt:         createdAt,
                LastAccessedAt:    lastAccessedAt,
                AgeInDays:         ageInDays,
                AverageClicksPerDay: avgClicksPerDay,
                IsActive:          isActive
            );
        }
    }
}
