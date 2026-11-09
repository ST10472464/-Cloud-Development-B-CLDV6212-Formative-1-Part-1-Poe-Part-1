using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.ApiGateway.Models;

// Microsoft Learn - ITableEntity Interface
// https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.itableentity?view=azure-dotnet
// PartitionKey is the role and RowKey is the email, so one partition query covers a whole access tier while an email lookup stays unique across the table.
public class UserProfile : ITableEntity
{
    public string PartitionKey { get; set; } = default!;

    public string RowKey { get; set; } = default!;

    public ETag ETag { get; set; } = default!;

    public DateTimeOffset? Timestamp { get; set; }

    // BCrypt hash only — never the plain text, and never returned by any endpoint.
    public string PasswordHash { get; set; } = default!;

    public string FullName { get; set; } = default!;

    public DateTime CreatedDate { get; set; }
}
