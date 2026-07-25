using UrlShortener.Persistence.Entities;

namespace UrlShortener.Services
{
    /// <summary>
    /// Core business operations for the URL shortener.
    /// </summary>
    public interface IUrlShortenerService
    {
        /// <summary>
        /// Creates a new short URL entry.
        /// </summary>
        /// <param name="longUrl">The original URL to shorten.</param>
        /// <param name="customAlias">
        /// Optional custom short code. When <c>null</c> a random code is generated.
        /// </param>
        /// <returns>The persisted <see cref="UrlRecord"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="customAlias"/> is already taken.
        /// </exception>
        Task<UrlRecord> CreateAsync(string longUrl, string? customAlias = null);

        /// <summary>
        /// Retrieves the metadata for a short code without incrementing the click counter.
        /// </summary>
        /// <returns><c>null</c> when the code does not exist.</returns>
        Task<UrlRecord?> GetAsync(string shortCode);

        /// <summary>
        /// Resolves the long URL for a short code and increments its click counter.
        /// Returns <c>null</c> when the code does not exist.
        /// </summary>
        Task<string?> ResolveAndTrackAsync(string shortCode);

        /// <summary>
        /// Deletes a short URL entry.
        /// </summary>
        /// <returns><c>true</c> if the record existed and was deleted; <c>false</c> if not found.</returns>
        Task<bool> DeleteAsync(string shortCode);
    }
}
