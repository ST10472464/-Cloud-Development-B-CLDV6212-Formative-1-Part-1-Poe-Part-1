using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.ApiGateway.Dtos;

/// <summary>
/// Payload for <c>POST /api/orders/queue</c>. Mirrors the Part 2 queue producer contract so a
/// message produced by the gateway is indistinguishable from one produced by the function app.
/// </summary>
public sealed class QueueOrderRequest
{
    /// <summary>Unique order identifier, for example <c>ORD-2026-8801</c>.</summary>
    [Required(ErrorMessage = "OrderId is required.")]
    [MaxLength(100, ErrorMessage = "OrderId must be 100 characters or fewer.")]
    public string OrderId { get; set; } = default!;

    /// <summary>Student or staff member placing the order.</summary>
    [Required(ErrorMessage = "CustomerName is required.")]
    [MaxLength(200, ErrorMessage = "CustomerName must be 200 characters or fewer.")]
    public string CustomerName { get; set; } = default!;

    /// <summary>At least one SKU must be selected.</summary>
    [MinLength(1, ErrorMessage = "At least one item SKU is required.")]
    public List<string> SelectedItemSKUs { get; set; } = new();

    /// <summary>Order total in ZAR. Must be greater than zero.</summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "TotalPrice must be greater than zero.")]
    public double TotalPrice { get; set; }

    /// <summary>Order timestamp. Defaults to UTC now when omitted.</summary>
    public DateTime? OrderTimestamp { get; set; }
}

/// <summary>Public shape of an <c>Orders</c> table row.</summary>
public sealed class OrderDto
{
    /// <summary>Order date (<c>yyyy-MM-dd</c>), the table PartitionKey.</summary>
    public string OrderDate { get; set; } = default!;

    /// <summary>Order identifier, the table RowKey.</summary>
    public string OrderId { get; set; } = default!;

    /// <summary>Customer placing the order.</summary>
    public string CustomerName { get; set; } = default!;

    /// <summary>Comma separated SKU list as stored by the queue trigger.</summary>
    public string SelectedItemSKUs { get; set; } = default!;

    /// <summary>Order total in ZAR.</summary>
    public double TotalPrice { get; set; }

    /// <summary>Lifecycle status: Received, Preparing, Ready or Collected.</summary>
    public string Status { get; set; } = default!;

    /// <summary>Original order timestamp.</summary>
    public DateTime OrderTimestamp { get; set; }
}

/// <summary>Payload for <c>PUT /api/orders/status</c>.</summary>
public sealed class UpdateOrderStatusRequest
{
    /// <summary>Order date (<c>yyyy-MM-dd</c>) identifying the partition.</summary>
    [Required(ErrorMessage = "OrderDate is required.")]
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "OrderDate must use the yyyy-MM-dd format.")]
    public string OrderDate { get; set; } = default!;

    /// <summary>Order identifier identifying the row.</summary>
    [Required(ErrorMessage = "OrderId is required.")]
    public string OrderId { get; set; } = default!;

    /// <summary>Target lifecycle status.</summary>
    [Required(ErrorMessage = "Status is required.")]
    [RegularExpression("^(Received|Preparing|Ready|Collected)$",
        ErrorMessage = "Status must be one of: Received, Preparing, Ready, Collected.")]
    public string Status { get; set; } = default!;
}

/// <summary>Response returned after an order is accepted onto the queue.</summary>
public sealed class QueueOrderResponse
{
    /// <summary>Human readable confirmation.</summary>
    public string Message { get; set; } = default!;

    /// <summary>Identifier of the order that was queued.</summary>
    public string OrderId { get; set; } = default!;
}
