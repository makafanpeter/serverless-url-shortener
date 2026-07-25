using Serilog;
using UrlShortener.Infrastructure;
using UrlShortener.Infrastructure.Logging;
using UrlShortener.Infrastructure.Web;
using UrlShortener.Persistence;
using UrlShortener.Services;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var env = builder.Environment;

try
{
    builder.UseCustomSerilog(env);
    
    Log.Information("Configuring web host ({ApplicationContext})...", env.ApplicationName);
    
    // Add services to the container.
    builder.Services.AddControllers();

// Persistence (DynamoDB)
    builder.Services.AddPersistence(configuration);

// URL Shortener business logic
    builder.Services.AddScoped<IUrlShortenerService, UrlShortenerService>();

// Global exception handler
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();
    
    //Add Services
    builder.Services
        .AddCustomCors(configuration);

// Add AWS Lambda support. When the application is run in Lambda, Kestrel is swapped out as the
// web server with Amazon.Lambda.AspNetCoreServer, which translates API Gateway events.
    builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

    var app = builder.Build();
    
    
    app.UseCustomCors(configuration);

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
    
    Log.Information("Starting web host ({ApplicationContext})...", env.ApplicationName);
    await app.RunAsync();

    Log.Information("Stopping web host ({ApplicationContext})...", env.ApplicationName);

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Program terminated unexpectedly ({ApplicationContext})!", env.ApplicationName);
    return 1;
}
finally
{
    Log.CloseAndFlush();
}


