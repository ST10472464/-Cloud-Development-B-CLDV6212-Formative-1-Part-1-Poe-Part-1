using CoffeeNChill.ApiGateway.Dtos;
using CoffeeNChill.ApiGateway.Models;

namespace CoffeeNChill.ApiGateway.Mapping;

// Every storage entity is converted here rather than serialised directly, which is what keeps
// internal fields such as PasswordHash, ETag and Timestamp out of every API response.
public static class Mappers
{
    // Omits PasswordHash on purpose: the DTO has no such property, so a hash cannot leak.
    public static UserProfileDto ToDto(this UserProfile user) => new()
    {
        Email = user.RowKey,
        FullName = user.FullName,
        Role = user.PartitionKey,
        CreatedDate = user.CreatedDate
    };

    public static MenuItemDto ToDto(this MenuItem item) => new()
    {
        Category = item.PartitionKey,
        Sku = item.RowKey,
        Name = item.Name,
        Description = item.Description,
        Price = item.Price,
        IsAvailable = item.IsAvailable
    };

    public static OrderDto ToDto(this OrderEntity order) => new()
    {
        OrderDate = order.PartitionKey,
        OrderId = order.RowKey,
        CustomerName = order.CustomerName,
        SelectedItemSKUs = order.SelectedItemSKUs,
        TotalPrice = order.TotalPrice,
        Status = order.Status,
        OrderTimestamp = order.OrderTimestamp
    };

    public static MenuItem ToEntity(this CreateMenuItemRequest request) => new()
    {
        PartitionKey = request.Category.Trim(),
        RowKey = request.Sku.Trim(),
        Name = request.Name.Trim(),
        Description = request.Description?.Trim() ?? string.Empty,
        Price = request.Price,
        IsAvailable = request.IsAvailable
    };
}
