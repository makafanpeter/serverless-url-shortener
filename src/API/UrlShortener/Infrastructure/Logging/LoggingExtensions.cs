using System.Reflection;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using UrlShortener.Infrastructure.Configurations;

namespace UrlShortener.Infrastructure.Logging;

public static class LoggingExtensions
{
public static WebApplicationBuilder UseCustomSerilog(this WebApplicationBuilder builder, IWebHostEnvironment env)
{
    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        var logOptions = context.Configuration
            .GetSection(nameof(LoggingOptions))
            .Get<LoggingOptions>();
        
        logOptions = SetDefault(logOptions); // Now being used

        // Safe iteration with null check
        if (logOptions?.LogLevel != null)
        {
            foreach (var logLevel in logOptions.LogLevel)
            {
                var serilogLevel = ConvertToSerilogLevel(logLevel.Value);

                if (logLevel.Key == "Default")
                {
                    loggerConfiguration.MinimumLevel.Is(serilogLevel);
                }
                else
                {
                    loggerConfiguration.MinimumLevel.Override(logLevel.Key, serilogLevel);
                }
            }
        }
        
        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown";

        loggerConfiguration
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.WithProperty("Assembly", assemblyName)
            .Enrich.WithProperty("Application", env.ApplicationName)
            .Enrich.WithProperty("EnvironmentName", env.EnvironmentName)
            .Enrich.WithProperty("ContentRootPath", env.ContentRootPath)
            .Enrich.WithExceptionDetails()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{TraceId}] {Message:lj} ({SourceContext:l}){NewLine}{Exception}")
            .ReadFrom.Configuration(context.Configuration);
    });

    return builder;
}

private static LoggingOptions SetDefault(LoggingOptions? options)
{
    options ??= new LoggingOptions
    {
        LogLevel = new Dictionary<string, LogLevel>() // Initialize the dictionary
    };
    
    options.LogLevel ??= new Dictionary<string, LogLevel>();
    options.LogLevel.TryAdd("Default", LogLevel.Warning);

    return options;
}

    private static LogEventLevel ConvertToSerilogLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            LogLevel.None => LogEventLevel.Fatal,
            _ => LogEventLevel.Fatal
        };
    }
}