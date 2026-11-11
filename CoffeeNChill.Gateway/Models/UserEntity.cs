
using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.Gateway.Models;

public class UserEntity : ITableEntity
{
    // Admin, Staff or Customer
    public string PartitionKey { get; set; } = "Customer";

    // User's email address
    public string RowKey { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedDate { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}
