using Azure;
using Azure.Data.Tables;

namespace Cloud_Development_B_CLDV_6212__POE_Part_1
{
    /// <summary>
    /// Represents a CoffeeNChill menu item stored in the MenuItems Azure Table.
    /// PartitionKey = Category (e.g. "Hot Drinks", "Cold Drinks", "Pastries", "Sandwiches").
    /// RowKey = Unique item SKU / ID (e.g. "COF-001", "PAS-104").
    /// </summary>
    // Microsoft Learn - ITableEntity Interface
    // https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.itableentity
    public class MenuItem : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;

        public string RowKey { get; set; } = default!;

        public ETag ETag { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; } = default!;

        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public double Price { get; set; }
        public bool IsAvailable { get; set; }
    }
}