namespace UrlShortener.Persistence.Infrastructure
{
    public interface IDynamoDbContext<T> : IDisposable where T : class
    {
        Task<T?> GetByIdAsync(string id);
        Task SaveAsync(T item);

        /// <summary>
        /// Writes the item only if no record with the same hash key already exists.
        /// Uses a DynamoDB <c>ConditionExpression = "attribute_not_exists(#hk)"</c>
        /// so the check and write are a single atomic operation — no separate read needed.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the item was written; <c>false</c> if the key already existed.
        /// </returns>
        Task<bool> SaveIfNotExistsAsync(T item);

        Task DeleteByIdAsync(T item);
    }
}
