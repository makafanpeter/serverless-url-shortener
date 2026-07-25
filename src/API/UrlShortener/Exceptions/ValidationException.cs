namespace UrlShortener.Exceptions
{
    /// <summary>
    /// Thrown when input fails domain validation rules
    /// (e.g. an invalid custom alias format). Maps to HTTP 400.
    /// </summary>
    public sealed class ValidationException : AppException
    {
        public ValidationException(string message)
            : base(message, StatusCodes.Status400BadRequest, "VALIDATION_ERROR") { }
    }
}
