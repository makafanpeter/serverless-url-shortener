using Microsoft.AspNetCore.Diagnostics;
using UrlShortener.Exceptions;
using UrlShortener.Models;

namespace UrlShortener.Infrastructure
{
    /// <summary>
    /// Global exception handler registered with ASP.NET Core's built-in
    /// <see cref="IExceptionHandler"/> pipeline (available from .NET 8).
    /// Maps every exception to a consistent <see cref="ErrorResponse"/> JSON body.
    ///
    /// Error classification:
    /// <list type="bullet">
    ///   <item><see cref="AppException"/> subclasses → their own status code + error code</item>
    ///   <item>Anything else → 500 Internal Server Error (detail hidden from caller)</item>
    /// </list>
    /// </summary>
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var traceId = httpContext.TraceIdentifier;

            var (statusCode, errorCode, message) = exception switch
            {
                AppException ex => (ex.StatusCode, ex.ErrorCode, ex.Message),
                _ => (
                    StatusCodes.Status500InternalServerError,
                    "INTERNAL_ERROR",
                    "An unexpected error occurred. Please try again later."
                )
            };

            // Log unhandled exceptions at Error; domain exceptions at Warning.
            if (exception is AppException)
                _logger.LogWarning(
                    exception,
                    "Domain exception [{ErrorCode}] on {Method} {Path}: {Message}",
                    errorCode, httpContext.Request.Method, httpContext.Request.Path, message);
            else
                _logger.LogError(
                    exception,
                    "Unhandled exception on {Method} {Path}",
                    httpContext.Request.Method, httpContext.Request.Path);

            httpContext.Response.StatusCode  = statusCode;
            httpContext.Response.ContentType = "application/json";

            await httpContext.Response.WriteAsJsonAsync(
                new ErrorResponse(errorCode, message, traceId),
                cancellationToken);

            // Return true to signal that the exception has been handled.
            return true;
        }
    }
}
