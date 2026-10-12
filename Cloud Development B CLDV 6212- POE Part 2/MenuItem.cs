using Azure;
using Azure.Data.Tables;

namespace Cloud_Development_B_CLDV_6212__POE_Part_1
{
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