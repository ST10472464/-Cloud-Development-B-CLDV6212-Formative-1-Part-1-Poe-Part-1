using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.ApiGateway.Models;

// Microsoft Learn - ITableEntity Interface
// https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.itableentity?view=azure-dotnet
// Written by the Part 2 queue trigger: PartitionKey is the order date (yyyy-MM-dd) and RowKey is the order id. The gateway only reads this table and changes Status.
public class OrderEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;

    public string RowKey { get; set; } = default!;

    public ETag ETag { get; set; } = default!;

    public DateTimeOffset? Timestamp { get; set; }

    public string CustomerName { get; set; } = default!;

    public string SelectedItemSKUs { get; set; } = default!;

    public double TotalPrice { get; set; }

    public string Status { get; set; } = "Received";

    public DateTime OrderTimestamp { get; set; }
}
