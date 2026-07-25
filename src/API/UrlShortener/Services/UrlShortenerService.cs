using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UrlShortener.Exceptions;
using UrlShortener.Persistence.Entities;
using UrlShortener.Persistence.Infrastructure;

namespace UrlShortener.Services
{
    /// <summary>
    /// Implements URL shortening logic backed by DynamoDB.
    /// Short-code uniqueness is enforced with a single atomic
    /// <c>PutItem(ConditionExpression = "attribute_not_exists(ShortCode)")</c>
    /// call — no pre-read required, no TOCTOU race condition.
    /// </summary>
    public sealed class UrlShortenerService : IUrlShortenerService
    {
        // Base62 alphabet — URL-safe, unambiguous characters only.
        private const string Alphabet =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const int ShortCodeLength = 7;
        private const int MaxCollisionRetries = 5;

        // Valid alias: 3-50 chars, alphanumeric + hyphens, no leading/trailing hyphen.
        private static readonly Regex AliasRegex =
            new(@"^[a-zA-Z0-9][a-zA-Z0-9\-]{1,48}[a-zA-Z0-9]$", RegexOptions.Compiled);

        private readonly IDynamoDbContext<UrlRecord> _db;
        private readonly ILogger<UrlShortenerService> _logger;

        public UrlShortenerService(
            IDynamoDbContext<UrlRecord> db,
            ILogger<UrlShortenerService> logger)
        {
            _db     = db;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<UrlRecord> CreateAsync(string longUrl, string? customAlias = null)
        {
            bool isCustom = customAlias is not null;
            string shortCode = isCustom ? customAlias!.Trim() : GenerateRandomCode();

            if (isCustom)
                ValidateAlias(shortCode);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var record = new UrlRecord
            {
                ShortCode    = shortCode,
                LongUrl      = longUrl,
                Clicks       = 0,
                CreatedAt    = now,
                LastAccessed = 0
            };

            for (int attempt = 1; attempt <= MaxCollisionRetries; attempt++)
            {
                // Single atomic write — DynamoDB rejects if the hash key already exists.
                bool saved = await _db.SaveIfNotExistsAsync(record);

                if (saved)
                {
                    _logger.LogInformation(
                        "Created short URL: {ShortCode} → {LongUrl}", shortCode, longUrl);
                    return record;
                }

                // Hash key collision.
                if (isCustom)
                    throw new ConflictException(
                        $"The alias '{shortCode}' is already taken.");

                _logger.LogDebug(
                    "Short code collision on '{Code}' (attempt {Attempt}/{Max}), regenerating.",
                    shortCode, attempt, MaxCollisionRetries);

                // Regenerate and update the record for the next attempt.
                shortCode        = GenerateRandomCode();
                record.ShortCode = shortCode;
            }

            throw new AppException(
                "Failed to generate a unique short code after multiple attempts. Please try again.",
                StatusCodes.Status503ServiceUnavailable,
                "SHORT_CODE_EXHAUSTED");
        }

        /// <inheritdoc/>
        public async Task<UrlRecord?> GetAsync(string shortCode) =>
            await _db.GetByIdAsync(shortCode.Trim());

        /// <inheritdoc/>
        public async Task<string?> ResolveAndTrackAsync(string shortCode)
        {
            var record = await _db.GetByIdAsync(shortCode.Trim());
            if (record is null)
                return null;

            // Update analytics without blocking the redirect response.
            record.Clicks++;
            record.LastAccessed = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _ = _db.SaveAsync(record).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.LogWarning(
                        t.Exception,
                        "Failed to update click counter for {ShortCode}", shortCode);
            }, TaskScheduler.Default);

            return record.LongUrl;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string shortCode)
        {
            var record = await _db.GetByIdAsync(shortCode.Trim());
            if (record is null)
                return false;

            await _db.DeleteByIdAsync(record);
            _logger.LogInformation("Deleted short URL: {ShortCode}", shortCode);
            return true;
        }

        // ------------------------------------------------------------------ helpers

        private static string GenerateRandomCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(ShortCodeLength);
            return new string(bytes.Select(b => Alphabet[b % Alphabet.Length]).ToArray());
        }

        private static void ValidateAlias(string alias)
        {
            if (alias.Length < 3 || alias.Length > 50)
                throw new ValidationException(
                    "Custom alias must be between 3 and 50 characters.");

            if (!AliasRegex.IsMatch(alias))
                throw new ValidationException(
                    "Custom alias may only contain letters, digits, and hyphens, " +
                    "and must not start or end with a hyphen.");
        }
    }
}
