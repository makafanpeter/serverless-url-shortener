using UrlShortener.Persistence.Entities;
using UrlShortener.Persistence.Infrastructure;

namespace UrlShortener.Persistence
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
        {

            var options = new DynamoDbOptions()
            {
                Region = configuration.GetValue<string>("AmazonOptions:RegionEndpoint")?? string.Empty,
                TableName = nameof(UrlShortener),
                TablePrefix = configuration.GetValue<string?>("AmazonOptions:TablePrefix")
            };

            services.AddScoped<IDynamoDbContext<UrlRecord>>(provider => new DynamoDbContext<UrlRecord>(options));
            return services;


        }
    }
}
