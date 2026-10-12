using Azure.Data.Tables;
using Cloud_Development_B_CLDV_6212__POE_Part_1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace CoffeeNChill.Functions
{
    public class OrderProcessor
    {
        private readonly ILogger<OrderProcessor> _logger;
        private readonly string _connectionString;
        private const string OrdersTableName = "Orders";

        public OrderProcessor(ILogger<OrderProcessor> logger)
        {
            _logger = logger;
            _connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "UseDevelopmentStorage=true";
        }

        // https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue
        [Function("ProcessOrderQueue")]
        public async Task ProcessOrderQueue(
            [QueueTrigger("order-processing-queue", Connection = "AzureWebJobsStorage")] string queueMessage)
        {
            Order? order = ParseOrder(queueMessage);
            if (order == null || string.IsNullOrWhiteSpace(order.OrderId))
            {
                _logger.LogError("Invalid order payload in queue message (length {Length}).", queueMessage?.Length ?? 0);
                throw new InvalidOperationException("Invalid order payload; sent to poison queue after retries.");
            }

            _logger.LogInformation("Processing order {OrderId} for {CustomerName}.", order.OrderId, order.CustomerName);

            var tableClient = new TableClient(_connectionString, OrdersTableName);
            await tableClient.CreateIfNotExistsAsync();

            var entity = new OrderEntity
            {
                PartitionKey = order.OrderTimestamp.ToString("yyyy-MM-dd"),
                RowKey = order.OrderId,
                CustomerName = order.CustomerName,
                SelectedItemSKUs = string.Join(",", order.SelectedItemSKUs ?? new List<string>()),
                TotalPrice = order.TotalPrice,
                Status = "Received",
                OrderTimestamp = order.OrderTimestamp
            };

            await tableClient.AddEntityAsync(entity);
            _logger.LogInformation("Order {OrderId} status: Received.", order.OrderId);
        }

        internal static Order? ParseOrder(string queueMessage)
        {
            if (string.IsNullOrWhiteSpace(queueMessage))
                return null;

            string json = queueMessage.Trim();

            // Producer sends Base64(JSON); runtime usually decodes first, so accept both.
            if (!json.StartsWith("{"))
            {
                try
                {
                    json = Encoding.UTF8.GetString(Convert.FromBase64String(json));
                }
                catch (FormatException)
                {
                    return null;
                }
            }

            try
            {
                return JsonSerializer.Deserialize<Order>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
