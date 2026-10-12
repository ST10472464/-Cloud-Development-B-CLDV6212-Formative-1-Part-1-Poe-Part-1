using Azure.Data.Tables;
using Cloud_Development_B_CLDV_6212__POE_Part_1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CoffeeNChill.Functions
{
    public class OrderProcessor
    {
        private readonly ILogger<OrderProcessor> _logger;
        private readonly string _connectionString;
        private const string OrdersTableName = "Orders";
        private const string PoisonQueueName = "order-processing-queue-poison";

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
                await SendToPoisonQueueAsync(queueMessage, "unparsable-or-missing-OrderId");
                throw new InvalidOperationException("Invalid order payload; sent to poison queue after retries.");
            }

            _logger.LogInformation("Processing order {OrderId} for {CustomerName}.", order.OrderId, order.CustomerName);

            try
            {
                var tableClient = new TableClient(_connectionString, OrdersTableName);
                await tableClient.CreateIfNotExistsAsync();

                var entity = new OrderEntity
                {
                    PartitionKey = order.OrderTimestamp == default ? DateTime.UtcNow.ToString("yyyy-MM-dd") : order.OrderTimestamp.ToString("yyyy-MM-dd"),
                    RowKey = order.OrderId,
                    CustomerName = order.CustomerName,
                    SelectedItemSKUs = string.Join(",", order.SelectedItemSKUs ?? new List<string>()),
                    TotalPrice = order.TotalPrice,
                    Status = "Received",
                    OrderTimestamp = order.OrderTimestamp == default ? DateTime.UtcNow : order.OrderTimestamp
                };

                await tableClient.AddEntityAsync(entity);
                _logger.LogInformation("Order {OrderId} status: Received.", order.OrderId);

                await AdvanceStatusAsync(tableClient, entity, "Preparing", 2000, order.OrderId);
                await AdvanceStatusAsync(tableClient, entity, "Ready", 3000, order.OrderId);
                await AdvanceStatusAsync(tableClient, entity, "Collected", 2000, order.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order {OrderId}; forwarding to poison queue.", order.OrderId);
                await SendToPoisonQueueAsync(queueMessage, $"processing-failed-{order.OrderId}");
                throw;
            }
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

        // Re-fetch for ETag, then move to the next barista stage.
        private async Task AdvanceStatusAsync(TableClient tableClient, OrderEntity entity, string status, int delayMs, string orderId)
        {
            await Task.Delay(delayMs);
            var current = (await tableClient.GetEntityAsync<OrderEntity>(entity.PartitionKey, entity.RowKey)).Value;
            current.Status = status;
            await tableClient.UpdateEntityAsync(current, current.ETag, TableUpdateMode.Replace);
            entity.ETag = current.ETag;
            _logger.LogInformation("Order {OrderId} status: {Status}.", orderId, status);
        }

        private async Task SendToPoisonQueueAsync(string? originalMessage, string reason)
        {
            try
            {
                var poisonClient = new Azure.Storage.Queues.QueueClient(_connectionString, PoisonQueueName);
                await poisonClient.CreateIfNotExistsAsync();
                await poisonClient.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(originalMessage ?? string.Empty)));
                _logger.LogWarning("Forwarded failing queue message to {Poison} ({Reason}).", PoisonQueueName, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to forward message to {Poison}.", PoisonQueueName);
            }
        }

        // Lets Postman verify lifecycle: GET /api/orders/{date}/{orderId}.
        [Function("GetOrderStatus")]
        public async Task<HttpResponseData> GetOrderStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{date}/{orderId}")] HttpRequestData req,
            string date, string orderId)
        {
            var tableClient = new TableClient(_connectionString, OrdersTableName);
            await tableClient.CreateIfNotExistsAsync();

            try
            {
                var entity = (await tableClient.GetEntityAsync<OrderEntity>(date, orderId)).Value;
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(entity);
                return response;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"Order '{orderId}' on '{date}' not found.");
                return notFound;
            }
        }

        // Lists one day of orders: GET /api/orders/{date}.
        [Function("GetOrdersByDate")]
        public async Task<HttpResponseData> GetOrdersByDate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{date}")] HttpRequestData req,
            string date)
        {
            var tableClient = new TableClient(_connectionString, OrdersTableName);
            await tableClient.CreateIfNotExistsAsync();

            var orders = new List<OrderEntity>();
            await foreach (var entity in tableClient.QueryAsync<OrderEntity>(filter: $"PartitionKey eq '{date}'"))
            {
                orders.Add(entity);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(orders);
            return response;
        }
    }
}
