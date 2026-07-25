using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Models
{
    /// <summary>
    /// Request payload for creating a new short URL.
    /// </summary>
    public record CreateShortUrlRequest(
        /// <summary>The original long URL to shorten.</summary>
        [Required, Url] string LongUrl,

        /// <summary>
        /// Optional custom short code alias. If omitted a random 7-character
        /// Base62 code is generated automatically.
        /// </summary>
        string? CustomAlias
    );
}
