using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.ApiGateway.Models;

// Microsoft Learn - ITableEntity Interface
// https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.itableentity?view=azure-dotnet
// PartitionKey is the category and RowKey is the SKU, matching the Part 1 contract.
public class MenuItem : ITableEntity
{
    public string PartitionKey { get; set; } = default!;

    public string RowKey { get; set; } = default!;

    public ETag ETag { get; set; } = default!;

    public DateTimeOffset? Timestamp { get; set; }

    public string Name { get; set; } = default!;

    public string Description { get; set; } = default!;

    public double Price { get; set; }

    public bool IsAvailable { get; set; }
}
