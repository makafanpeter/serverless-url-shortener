namespace UrlShortener.Models
{
    /// <summary>
    /// Uniform error envelope returned by the global exception handler.
    /// </summary>
    /// <param name="Code">
    /// A stable, machine-readable code callers can switch on (e.g. "NOT_FOUND").
    /// </param>
    /// <param name="Message">Human-readable description of what went wrong.</param>
    /// <param name="TraceId">
    /// The <c>X-Trace-Id</c> / <c>traceparent</c> correlation identifier for this request,
    /// useful for log correlation.
    /// </param>
    public record ErrorResponse(
        string Code,
        string Message,
        string? TraceId = null
    );
}
