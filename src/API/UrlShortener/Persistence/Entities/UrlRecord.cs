using Amazon.DynamoDBv2.DataModel;

namespace UrlShortener.Persistence.Entities
{

    public class UrlRecord
    {
        [DynamoDBHashKey]
        public required string ShortCode { get; set; }

        public required string LongUrl { get; set; }
        public int Clicks { get; set; }
        public long CreatedAt { get; set; }
        public long LastAccessed { get; set; }
    }
}
