using CoffeeNChill.ApiGateway.Dtos;
using CoffeeNChill.ApiGateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.ApiGateway.Controllers;

// Microsoft Learn - Azure Queue storage trigger and bindings for Azure Functions
// https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue?tabs=isolated-process%2Cextensionv5&pivots=programming-language-csharp
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
[Tags("Orders")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrderController> _logger;

    public OrderController(IOrderService orderService, ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Validates an order and pushes it onto <c>order-processing-queue</c> as a Base64 encoded
    /// JSON message. Open to anonymous students, matching the brief.
    /// </summary>
    /// <param name="request">Order id, customer, SKUs, total and timestamp.</param>
    /// <response code="200">The order was queued.</response>
    /// <response code="400">The payload failed validation.</response>
    /// <response code="500">The queue could not be reached.</response>
    [AllowAnonymous]
    [HttpPost("queue")]
    [ProducesResponseType(typeof(QueueOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<QueueOrderResponse>> Queue(
        [FromBody] QueueOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orderService.QueueAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns every order placed on the given date.</summary>
    /// <param name="date">Order date in <c>yyyy-MM-dd</c> format (table PartitionKey).</param>
    /// <response code="200">The orders were returned, possibly empty.</response>
    /// <response code="400">The date was malformed.</response>
    /// <response code="500">The Orders table could not be reached.</response>
    [AllowAnonymous]
    [HttpGet("{date:regex(^\\d{{4}}-\\d{{2}}-\\d{{2}}$)}")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetByDate(
        [FromRoute] string date, CancellationToken cancellationToken)
    {
        var orders = await _orderService.GetByDateAsync(date, cancellationToken);
        return Ok(orders);
    }

    /// <summary>Returns a single order and its current lifecycle status.</summary>
    /// <param name="date">Order date in <c>yyyy-MM-dd</c> format (table PartitionKey).</param>
    /// <param name="orderId">Order identifier (table RowKey).</param>
    /// <response code="200">The order was returned.</response>
    /// <response code="404">No such order.</response>
    /// <response code="500">The Orders table could not be reached.</response>
    [AllowAnonymous]
    [HttpGet("{date:regex(^\\d{{4}}-\\d{{2}}-\\d{{2}}$)}/{orderId}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderDto>> Get(
        [FromRoute] string date, [FromRoute] string orderId, CancellationToken cancellationToken)
    {
        var order = await _orderService.GetAsync(date, orderId, cancellationToken);
        return Ok(order);
    }

    /// <summary>
    /// Advances an order through Received, Preparing, Ready or Collected. Restricted to
    /// <c>Admin</c> and <c>Staff</c>, because only baristas and managers move an order along.
    /// </summary>
    /// <param name="request">Order date, order id and target status.</param>
    /// <response code="200">The status was updated.</response>
    /// <response code="400">The payload failed validation.</response>
    /// <response code="401">No valid Bearer token was supplied.</response>
    /// <response code="403">The caller is not an Admin or Staff member.</response>
    /// <response code="404">No such order.</response>
    /// <response code="409">The order changed while the update was in flight.</response>
    /// <response code="500">The Orders table could not be reached.</response>
    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("status")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        [FromBody] UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var order = await _orderService.UpdateStatusAsync(request, cancellationToken);

        _logger.LogInformation("Order {OrderId} moved to {Status} by {User}.",
            order.OrderId, order.Status, User.Identity?.Name);
        return Ok(order);
    }
}
