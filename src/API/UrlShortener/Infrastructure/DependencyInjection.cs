using UrlShortener.Infrastructure.Configurations;

namespace UrlShortener.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection(nameof(Cors))
            .Get<Cors>() ?? throw new ArgumentNullException(nameof(Cors));
        services.AddCors(options =>
        {
            options.AddPolicy("AllowedOrigins", builder => builder
                .WithOrigins(corsSettings.AllowedOrigins ?? [])
                .AllowAnyMethod()
                .AllowAnyHeader());

            options.AddPolicy("AllowAnyOrigin", builder => builder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            options.AddPolicy("CustomPolicy", builder => builder
                .AllowAnyOrigin()
                .WithMethods("Get")
                .WithHeaders("Content-Type"));
        });
      
      
        return services;

    }
    
    
    public static IApplicationBuilder UseCustomCors(this IApplicationBuilder app, IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection(nameof(Cors))
            .Get<Cors>() ?? throw new ArgumentNullException(nameof(Cors));
        app.UseCors(corsSettings.AllowAnyOrigin ? "AllowAnyOrigin" : "AllowedOrigins");

        return app;
    }
}