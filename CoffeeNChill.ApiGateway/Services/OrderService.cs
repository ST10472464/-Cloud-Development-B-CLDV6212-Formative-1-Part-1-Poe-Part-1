using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using CoffeeNChill.ApiGateway.Common;
using CoffeeNChill.ApiGateway.Dtos;
using CoffeeNChill.ApiGateway.Mapping;
using CoffeeNChill.ApiGateway.Models;
using CoffeeNChill.ApiGateway.Options;

namespace CoffeeNChill.ApiGateway.Services;

public interface IOrderService
{
    Task<QueueOrderResponse> QueueAsync(QueueOrderRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderDto>> GetByDateAsync(string orderDate, CancellationToken cancellationToken = default);

    Task<OrderDto> GetAsync(string orderDate, string orderId, CancellationToken cancellationToken = default);

    Task<OrderDto> UpdateStatusAsync(UpdateOrderStatusRequest request, CancellationToken cancellationToken = default);
}

public sealed class OrderService : IOrderService
{
    private const string QueueName = "order-processing-queue";
    private const string TableName = "Orders";

    // Microsoft Learn - JSON serialization and deserialization in .NET - overview
    // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly StorageOptions _storageOptions;
    private readonly TableClient _tableClient;
    private readonly ILogger<OrderService> _logger;

    public OrderService(StorageOptions storageOptions, ILogger<OrderService> logger)
    {
        _storageOptions = storageOptions;
        _tableClient = new TableClient(storageOptions.ConnectionString, TableName);
        _logger = logger;
    }

    // Microsoft Learn - Azure Queue storage trigger and bindings for Azure Functions
    // https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue?tabs=isolated-process%2Cextensionv5&pivots=programming-language-csharp
    public async Task<QueueOrderResponse> QueueAsync(
        QueueOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.OrderId))
            throw new AppValidationException("Invalid order data. OrderId is required.");
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new AppValidationException("Invalid order data. CustomerName is required.");
        if (request.SelectedItemSKUs is null || request.SelectedItemSKUs.Count == 0)
            throw new AppValidationException("At least one item SKU is required.");
        if (request.TotalPrice <= 0)
            throw new AppValidationException("TotalPrice must be greater than zero.");

        var order = new Order
        {
            OrderId = request.OrderId.Trim(),
            CustomerName = request.CustomerName.Trim(),
            SelectedItemSKUs = request.SelectedItemSKUs
                .Where(sku => !string.IsNullOrWhiteSpace(sku))
                .Select(sku => sku.Trim())
                .ToList(),
            TotalPrice = request.TotalPrice,
            OrderTimestamp = request.OrderTimestamp ?? DateTime.UtcNow
        };

        if (order.SelectedItemSKUs.Count == 0)
            throw new AppValidationException("At least one item SKU is required.");

        var queueClient = new QueueClient(_storageOptions.ConnectionString, QueueName);
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var payload = JsonSerializer.Serialize(order, SerializerOptions);

        // Microsoft Learn - Convert.ToBase64String Method
        // https://learn.microsoft.com/en-us/dotnet/api/system.convert.tobase64string?view=net-10.0
        // The queue trigger expects a Base64 encoded body, matching the Part 2 producer exactly.
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

        await queueClient.SendMessageAsync(base64, cancellationToken: cancellationToken);

        _logger.LogInformation("Order {OrderId} for {CustomerName} (R{TotalPrice}) queued on {QueueName}.",
            order.OrderId, order.CustomerName, order.TotalPrice, QueueName);

        return new QueueOrderResponse
        {
            Message = "Order queued successfully",
            OrderId = order.OrderId
        };
    }

    public async Task<IReadOnlyList<OrderDto>> GetByDateAsync(
        string orderDate, CancellationToken cancellationToken = default)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        var filter = $"PartitionKey eq '{Escape(orderDate)}'";
        var orders = new List<OrderDto>();

        await foreach (var order in _tableClient
                           .QueryAsync<OrderEntity>(filter: filter, cancellationToken: cancellationToken))
            orders.Add(order.ToDto());

        return orders;
    }

    public async Task<OrderDto> GetAsync(
        string orderDate, string orderId, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(orderDate, orderId, cancellationToken);
        return entity.ToDto();
    }

    public async Task<OrderDto> UpdateStatusAsync(
        UpdateOrderStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.OrderDate))
            throw new AppValidationException("OrderDate is required.");
        if (string.IsNullOrWhiteSpace(request.OrderId))
            throw new AppValidationException("OrderId is required.");
        if (request.Status is not ("Received" or "Preparing" or "Ready" or "Collected"))
            throw new AppValidationException("Status must be one of: Received, Preparing, Ready, Collected.");

        var existing = await GetEntityAsync(request.OrderDate, request.OrderId, cancellationToken);

        _logger.LogInformation("Order {OrderId} status {From} -> {To}.",
            existing.RowKey, existing.Status, request.Status);

        existing.Status = request.Status;

        try
        {
            await _tableClient.UpdateEntityAsync(
                existing, existing.ETag, TableUpdateMode.Replace, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            // 412 is Azure Tables' precondition failure: the ETag we read is no longer current.
            throw new ConflictException(
                $"Order '{request.OrderId}' was modified by another request. Retry the status update.");
        }

        return existing.ToDto();
    }

    private async Task<OrderEntity> GetEntityAsync(
        string orderDate, string orderId, CancellationToken cancellationToken)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        try
        {
            var response = await _tableClient.GetEntityAsync<OrderEntity>(
                orderDate, orderId, cancellationToken: cancellationToken);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new ResourceNotFoundException(
                $"Order with date '{orderDate}' and id '{orderId}' was not found.");
        }
    }

    // Doubles single quotes so a value cannot break out of its OData filter literal.
    private static string Escape(string value) => value.Trim().Replace("'", "''");
}
