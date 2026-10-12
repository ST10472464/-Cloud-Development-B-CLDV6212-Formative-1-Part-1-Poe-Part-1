using Azure;
using Azure.Data.Tables;

namespace Cloud_Development_B_CLDV_6212__POE_Part_1
{
    // Orders table record: PartitionKey = OrderDate (yyyy-MM-dd), RowKey = OrderId.
    public class OrderEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public ETag ETag { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; } = default!;

        public string CustomerName { get; set; } = default!;
        public string SelectedItemSKUs { get; set; } = default!;
        public double TotalPrice { get; set; }
        public string Status { get; set; } = "Received";
        public DateTime OrderTimestamp { get; set; }
    }
}
