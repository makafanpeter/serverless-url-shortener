namespace UrlShortener.Exceptions
{
    /// <summary>
    /// Base application exception that carries an HTTP status code and a
    /// machine-readable error code. All domain exceptions should derive from this.
    /// </summary>
    public class AppException : Exception
    {
        /// <summary>HTTP status code to return to the caller.</summary>
        public int StatusCode { get; }

        /// <summary>
        /// A stable, uppercase_snake_case string callers can pattern-match on
        /// (e.g. "NOT_FOUND", "CONFLICT", "VALIDATION_ERROR").
        /// </summary>
        public string ErrorCode { get; }

        public AppException(string message, int statusCode, string errorCode)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode  = errorCode;
        }
    }
}
