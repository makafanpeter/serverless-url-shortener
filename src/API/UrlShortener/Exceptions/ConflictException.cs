namespace UrlShortener.Exceptions
{
    /// <summary>
    /// Thrown when an operation conflicts with existing state
    /// (e.g. a custom alias is already taken). Maps to HTTP 409.
    /// </summary>
    public sealed class ConflictException : AppException
    {
        public ConflictException(string message)
            : base(message, StatusCodes.Status409Conflict, "CONFLICT") { }
    }
}
