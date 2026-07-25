using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Util;
using DynamoDBContextConfig = Amazon.DynamoDBv2.DataModel.DynamoDBContextConfig;
namespace UrlShortener.Persistence.Infrastructure
{
    public class DynamoDbContext<T> : IDynamoDbContext<T>
       where T : class
    {
        private readonly IDynamoDBContext _context;

        public DynamoDbContext(DynamoDbOptions amazonSettings)
        {
            var settings = amazonSettings;
            var region = RegionEndpoint.GetBySystemName(settings.Region);
            var requestTableName = settings.TableName;
            if (!string.IsNullOrEmpty(requestTableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(T)] = new TypeMapping(typeof(T), requestTableName);
            }
         

            _context = new DynamoDBContextBuilder()
                .WithDynamoDBClient(() => new AmazonDynamoDBClient(region))
                .ConfigureContext(cfg =>
                    {
                        cfg.TableNamePrefix = string.IsNullOrEmpty(settings.TablePrefix) ? string.Empty : settings.TablePrefix;           
                        cfg.ConsistentRead = true;
                        cfg.SkipVersionCheck = false;
                        cfg.Conversion = DynamoDBEntryConversion.V2;
                    })
                    .Build();

        }

        public async Task<T> GetByIdAsync(string id)
        {
            return await _context.LoadAsync<T>(id);
        }

        public async Task SaveAsync(T item)
        {
            await _context.SaveAsync(item);
        }

        public async Task DeleteByIdAsync(T item)
        {
            await _context.DeleteAsync(item);
        }



       

        public void Dispose()
        {
            _context?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

}
