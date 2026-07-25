using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Util;
using System.Reflection;

namespace UrlShortener.Persistence.Infrastructure
{
    /// <summary>
    /// Generic DynamoDB persistence context that wraps both the high-level
    /// <see cref="IDynamoDBContext"/> (object persistence model) and the low-level
    /// <see cref="IAmazonDynamoDB"/> client to support conditional writes.
    /// </summary>
    public class DynamoDbContext<T> : IDynamoDbContext<T>
        where T : class
    {
        private readonly IAmazonDynamoDB _client;
        private readonly IDynamoDBContext _context;

        /// <summary>Full DynamoDB table name including any configured prefix.</summary>
        private readonly string _fullTableName;

        /// <summary>
        /// DynamoDB attribute name of the hash key property on <typeparamref name="T"/>.
        /// Resolved once via reflection at construction time.
        /// </summary>
        private readonly string _hashKeyAttributeName;

        public DynamoDbContext(DynamoDbOptions amazonSettings)
        {
            var region = RegionEndpoint.GetBySystemName(amazonSettings.Region);
            var prefix = amazonSettings.TablePrefix ?? string.Empty;

            _fullTableName        = $"{prefix}{amazonSettings.TableName}";
            _hashKeyAttributeName = ResolveHashKeyAttributeName();

            if (!string.IsNullOrEmpty(amazonSettings.TableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(T)] =
                    new TypeMapping(typeof(T), amazonSettings.TableName);
            }

            // Create the low-level client once; share it with the high-level context.
            _client = new AmazonDynamoDBClient(region);

            _context = new DynamoDBContextBuilder()
                .WithDynamoDBClient(() => _client)
                .ConfigureContext(cfg =>
                {
                    cfg.TableNamePrefix = prefix;
                    cfg.ConsistentRead  = true;
                    cfg.SkipVersionCheck = false;
                    cfg.Conversion = DynamoDBEntryConversion.V2;
                })
                .Build();
        }

        /// <inheritdoc/>
        public async Task<T?> GetByIdAsync(string id)
            => await _context.LoadAsync<T>(id);

        /// <inheritdoc/>
        public async Task SaveAsync(T item)
            => await _context.SaveAsync(item);

        /// <inheritdoc/>
        /// <remarks>
        /// Issues a single <c>PutItem</c> with
        /// <c>ConditionExpression = "attribute_not_exists(#hk)"</c> — the existence
        /// check and write are one atomic DynamoDB operation, eliminating the TOCTOU
        /// race between a separate read and write.
        /// </remarks>
        public async Task<bool> SaveIfNotExistsAsync(T item)
        {
            var request = new PutItemRequest
            {
                TableName           = _fullTableName,
                Item                = BuildAttributeMap(item),
                ConditionExpression = "attribute_not_exists(#hk)",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#hk"] = _hashKeyAttributeName
                }
            };

            try
            {
                await _client.PutItemAsync(request);
                return true;
            }
            catch (ConditionalCheckFailedException)
            {
                // Hash key already exists — let the caller decide how to respond.
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task DeleteByIdAsync(T item)
            => await _context.DeleteAsync(item);

        /// <inheritdoc/>
        public void Dispose()
        {
            _context?.Dispose();
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Returns the DynamoDB attribute name of the property decorated with
        /// <see cref="DynamoDBHashKeyAttribute"/> on <typeparamref name="T"/>.
        /// </summary>
        private static string ResolveHashKeyAttributeName()
        {
            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<DynamoDBHashKeyAttribute>();
                if (attr is null) continue;

                return string.IsNullOrEmpty(attr.AttributeName) ? prop.Name : attr.AttributeName;
            }

            throw new InvalidOperationException(
                $"Type '{typeof(T).Name}' has no property decorated with [DynamoDBHashKey].");
        }

        /// <summary>
        /// Serialises <paramref name="item"/> to a DynamoDB attribute map via reflection,
        /// honouring <see cref="DynamoDBHashKeyAttribute"/>, <see cref="DynamoDBRangeKeyAttribute"/>,
        /// <see cref="DynamoDBPropertyAttribute"/>, and <see cref="DynamoDBIgnoreAttribute"/>.
        /// </summary>
        private static Dictionary<string, AttributeValue> BuildAttributeMap(T item)
        {
            var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);

            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetCustomAttribute<DynamoDBIgnoreAttribute>() is not null)
                    continue;

                var attrName =
                    NullIfEmpty(prop.GetCustomAttribute<DynamoDBHashKeyAttribute>()?.AttributeName)
                    ?? NullIfEmpty(prop.GetCustomAttribute<DynamoDBRangeKeyAttribute>()?.AttributeName)
                    ?? NullIfEmpty(prop.GetCustomAttribute<DynamoDBPropertyAttribute>()?.AttributeName)
                    ?? prop.Name;

                var value = prop.GetValue(item);
                if (value is null) continue;

                var attrValue = value switch
                {
                    string s   => new AttributeValue { S    = s },
                    bool b     => new AttributeValue { BOOL = b },
                    int i      => new AttributeValue { N    = i.ToString() },
                    long l     => new AttributeValue { N    = l.ToString() },
                    double d   => new AttributeValue { N    = d.ToString() },
                    float f    => new AttributeValue { N    = f.ToString() },
                    decimal dc => new AttributeValue { N    = dc.ToString() },
                    _          => new AttributeValue { S    = value.ToString() ?? string.Empty }
                };

                map[attrName] = attrValue;
            }

            return map;
        }

        private static string? NullIfEmpty(string? s) =>
            string.IsNullOrEmpty(s) ? null : s;
    }
}
