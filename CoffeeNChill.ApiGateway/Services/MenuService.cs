using Azure;
using Azure.Data.Tables;
using CoffeeNChill.ApiGateway.Common;
using CoffeeNChill.ApiGateway.Dtos;
using CoffeeNChill.ApiGateway.Mapping;
using CoffeeNChill.ApiGateway.Models;
using CoffeeNChill.ApiGateway.Options;

namespace CoffeeNChill.ApiGateway.Services;

public interface IMenuService
{
    Task<IReadOnlyList<MenuItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuItemDto>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);

    Task<MenuItemDto> CreateAsync(CreateMenuItemRequest request, CancellationToken cancellationToken = default);

    Task<MenuItemDto> UpdateAsync(
        string category, string sku, UpdateMenuItemRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string category, string sku, CancellationToken cancellationToken = default);
}

// Microsoft Learn - ITableEntity Interface
// https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.itableentity?view=azure-dotnet
public sealed class MenuService : IMenuService
{
    private const string TableName = "MenuItems";

    private readonly TableClient _tableClient;
    private readonly ILogger<MenuService> _logger;

    public MenuService(StorageOptions storageOptions, ILogger<MenuService> logger)
    {
        _tableClient = new TableClient(storageOptions.ConnectionString, TableName);
        _logger = logger;
    }

    // Microsoft Learn - TableClient.QueryAsync Method
    // https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.tableclient.queryasync?view=azure-dotnet
    public async Task<IReadOnlyList<MenuItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        var items = new List<MenuItemDto>();
        await foreach (var item in _tableClient.QueryAsync<MenuItem>(cancellationToken: cancellationToken))
            items.Add(item.ToDto());

        return items;
    }

    public async Task<IReadOnlyList<MenuItemDto>> GetByCategoryAsync(
        string category, CancellationToken cancellationToken = default)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        var filter = $"PartitionKey eq '{Escape(category)}'";
        var items = new List<MenuItemDto>();

        await foreach (var item in _tableClient
                           .QueryAsync<MenuItem>(filter: filter, cancellationToken: cancellationToken))
            items.Add(item.ToDto());

        return items;
    }

    public async Task<MenuItemDto> CreateAsync(
        CreateMenuItemRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Category))
            throw new AppValidationException("Category is required.");
        if (string.IsNullOrWhiteSpace(request.Sku))
            throw new AppValidationException("Sku is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppValidationException("Name is required.");
        if (request.Price <= 0)
            throw new AppValidationException("Price must be greater than zero.");

        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        var entity = request.ToEntity();
        try
        {
            await _tableClient.AddEntityAsync(entity, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new ConflictException(
                $"A menu item with SKU '{entity.RowKey}' already exists in category '{entity.PartitionKey}'.");
        }

        _logger.LogInformation("Created menu item {Sku} in category {Category}.",
            entity.RowKey, entity.PartitionKey);
        return entity.ToDto();
    }

    public async Task<MenuItemDto> UpdateAsync(
        string category,
        string sku,
        UpdateMenuItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await GetEntityAsync(category, sku, cancellationToken);

        // A null field means "leave it alone", so only supplied values overwrite the stored row.
        if (!string.IsNullOrWhiteSpace(request.Name))
            existing.Name = request.Name.Trim();
        if (request.Description is not null)
            existing.Description = request.Description.Trim();
        if (request.Price is not null)
        {
            if (request.Price < 0)
                throw new AppValidationException("Price cannot be negative.");
            existing.Price = request.Price.Value;
        }
        if (request.IsAvailable is not null)
            existing.IsAvailable = request.IsAvailable.Value;

        try
        {
            await _tableClient.UpdateEntityAsync(
                existing, existing.ETag, TableUpdateMode.Replace, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            // 412 is Azure Tables' precondition failure: the ETag we read is no longer current.
            throw new ConflictException(
                $"Menu item '{sku}' in category '{category}' was modified by another request. Retry the update.");
        }

        _logger.LogInformation("Updated menu item {Sku} in category {Category}.", sku, category);
        return existing.ToDto();
    }

    // Microsoft Learn - TableClient.DeleteEntityAsync Method
    // https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.tableclient.deleteentityasync?view=azure-dotnet
    public async Task DeleteAsync(string category, string sku, CancellationToken cancellationToken = default)
    {
        var existing = await GetEntityAsync(category, sku, cancellationToken);

        await _tableClient.DeleteEntityAsync(
            existing.PartitionKey, existing.RowKey, existing.ETag, cancellationToken);

        _logger.LogInformation("Deleted menu item {Sku} in category {Category}.", sku, category);
    }

    private async Task<MenuItem> GetEntityAsync(string category, string sku, CancellationToken cancellationToken)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        try
        {
            var response = await _tableClient.GetEntityAsync<MenuItem>(
                category, sku, cancellationToken: cancellationToken);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new ResourceNotFoundException(
                $"Menu item with category '{category}' and SKU '{sku}' was not found.");
        }
    }

    // Doubles single quotes so a value cannot break out of its OData filter literal.
    private static string Escape(string value) => value.Trim().Replace("'", "''");
}
