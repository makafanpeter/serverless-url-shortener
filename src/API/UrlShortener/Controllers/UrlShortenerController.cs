using Microsoft.AspNetCore.Mvc;
using UrlShortener.Exceptions;
using UrlShortener.Models;
using UrlShortener.Services;

namespace UrlShortener.Controllers
{
    /// <summary>
    /// Manages short URL records (create, read, delete).
    /// Redirects are handled at the application root via a minimal-API endpoint in Program.cs.
    /// All unhandled exceptions bubble up to <c>GlobalExceptionHandler</c>.
    /// </summary>
    [ApiController]
    [Route("api/url-shortener")]
    [Produces("application/json")]
    public class UrlShortenerController : ControllerBase
    {
        private readonly IUrlShortenerService _service;

        public UrlShortenerController(IUrlShortenerService service)
        {
            _service = service;
        }

        /// <summary>
        /// Creates a new short URL.
        /// </summary>
        /// <param name="request">Long URL and optional custom alias.</param>
        /// <response code="201">Short URL created successfully.</response>
        /// <response code="400">Validation failed (bad URL format or invalid alias).</response>
        /// <response code="409">The requested custom alias is already taken.</response>
        [HttpPost]
        [ProducesResponseType(typeof(ShortUrlResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateShortUrl([FromBody] CreateShortUrlRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var record   = await _service.CreateAsync(request.LongUrl, request.CustomAlias);
            var response = record.ToResponse(BuildBaseUrl());

            return CreatedAtAction(
                nameof(GetShortUrl),
                new { shortCode = record.ShortCode },
                response);
        }

        /// <summary>
        /// Retrieves metadata for a short code (does not count as a click).
        /// </summary>
        /// <param name="shortCode">The short code to look up.</param>
        /// <response code="200">Metadata returned.</response>
        /// <response code="404">Short code not found.</response>
        [HttpGet("{shortCode}")]
        [ProducesResponseType(typeof(ShortUrlResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetShortUrl(string shortCode)
        {
            var record = await _service.GetAsync(shortCode)
                ?? throw new NotFoundException($"Short code '{shortCode}' not found.");

            return Ok(record.ToResponse(BuildBaseUrl()));
        }

        /// <summary>
        /// Deletes a short URL entry.
        /// </summary>
        /// <param name="shortCode">The short code to delete.</param>
        /// <response code="204">Deleted successfully.</response>
        /// <response code="404">Short code not found.</response>
        [HttpDelete("{shortCode}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteShortUrl(string shortCode)
        {
            var deleted = await _service.DeleteAsync(shortCode);
            if (!deleted)
                throw new NotFoundException($"Short code '{shortCode}' not found.");

            return NoContent();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Derives the base URL from the incoming request's scheme and host.
        /// Works transparently across local dev, API Gateway stages, and custom domains.
        /// </summary>
        private string BuildBaseUrl()
        {
            var req = HttpContext.Request;
            return $"{req.Scheme}://{req.Host}{(req.PathBase.HasValue ? req.PathBase.Value : string.Empty)}";
        }
    }

    /// <summary>
    /// Extension helpers to convert persistence entities to API responses.
    /// </summary>
    internal static class UrlRecordExtensions
    {
        internal static ShortUrlResponse ToResponse(
            this Persistence.Entities.UrlRecord record, string baseUrl) =>
            new(
                ShortCode:    record.ShortCode,
                ShortUrl:     $"{baseUrl}/{record.ShortCode}",
                LongUrl:      record.LongUrl,
                Clicks:       record.Clicks,
                CreatedAt:    DateTimeOffset.FromUnixTimeSeconds(record.CreatedAt),
                LastAccessed: record.LastAccessed == 0
                    ? DateTimeOffset.MinValue
                    : DateTimeOffset.FromUnixTimeSeconds(record.LastAccessed)
            );
    }
}
