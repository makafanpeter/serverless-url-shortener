using UrlShortener.Infrastructure;
using UrlShortener.Persistence;
using UrlShortener.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Persistence (DynamoDB)
builder.Services.AddPersistence(builder.Configuration);

// URL Shortener business logic
builder.Services.AddScoped<IUrlShortenerService, UrlShortenerService>();

// Global exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add AWS Lambda support. When the application is run in Lambda, Kestrel is swapped out as the
// web server with Amazon.Lambda.AspNetCoreServer, which translates API Gateway events.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Root-level redirect: GET /{shortCode}
// This is the public-facing endpoint users follow from a short URL.
app.MapGet("/{shortCode}", async (string shortCode, IUrlShortenerService service) =>
{
    var longUrl = await service.ResolveAndTrackAsync(shortCode);

    return longUrl is not null
        ? Results.Redirect(longUrl, permanent: false)
        : Results.NotFound(new { error = $"Short code '{shortCode}' not found." });
});

app.Run();
