namespace UrlShortener.Exceptions
{
    /// <summary>Thrown when a requested resource does not exist. Maps to HTTP 404.</summary>
    public sealed class NotFoundException : AppException
    {
        public NotFoundException(string message)
            : base(message, StatusCodes.Status404NotFound, "NOT_FOUND") { }
    }
}
