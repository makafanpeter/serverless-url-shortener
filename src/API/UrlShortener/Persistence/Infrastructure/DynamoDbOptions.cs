namespace UrlShortener.Persistence.Infrastructure
{
    public class DynamoDbOptions
    {
        public required string TableName { get; set; }

        public  string? TablePrefix { get; set; }

        public required string Region { get; set; }
    }

}
