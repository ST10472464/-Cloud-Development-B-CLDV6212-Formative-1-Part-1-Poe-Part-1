using CoffeeNChill.ApiGateway.Dtos;
using CoffeeNChill.ApiGateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.ApiGateway.Controllers;

// Microsoft Learn - ASP.NET Core authorization with roles
// https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0
// Microsoft Learn - ITableEntity Interface
// https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.itableentity?view=azure-dotnet
[ApiController]
[Route("api/menu")]
[Produces("application/json")]
[Tags("Menu")]
public class MenuController : ControllerBase
{
    private readonly IMenuService _menuService;
    private readonly ILogger<MenuController> _logger;

    public MenuController(IMenuService menuService, ILogger<MenuController> logger)
    {
        _menuService = menuService;
        _logger = logger;
    }

    /// <summary>Returns every menu item.</summary>
    /// <response code="200">The menu was returned.</response>
    /// <response code="500">The MenuItems table could not be reached.</response>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<MenuItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _menuService.GetAllAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>Returns the items in a single category.</summary>
    /// <param name="category">Category (table PartitionKey), for example <c>Coffee</c>.</param>
    /// <response code="200">The category was returned, possibly empty.</response>
    /// <response code="400">The category was empty.</response>
    /// <response code="500">The MenuItems table could not be reached.</response>
    [AllowAnonymous]
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(IReadOnlyList<MenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<MenuItemDto>>> GetByCategory(
        [FromRoute] string category, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(category))
            return BadRequest(new { message = "A category is required." });

        var items = await _menuService.GetByCategoryAsync(category, cancellationToken);
        return Ok(items);
    }

    /// <summary>Creates a menu item. Restricted to <c>Admin</c> and <c>Staff</c>.</summary>
    /// <param name="request">Category, SKU, name, price and availability.</param>
    /// <response code="201">The item was created.</response>
    /// <response code="400">The payload failed validation.</response>
    /// <response code="401">No valid Bearer token was supplied.</response>
    /// <response code="403">The caller is not an Admin or Staff member.</response>
    /// <response code="409">That SKU already exists in the category.</response>
    /// <response code="500">The MenuItems table could not be reached.</response>
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MenuItemDto>> Create(
        [FromBody] CreateMenuItemRequest request, CancellationToken cancellationToken)
    {
        var created = await _menuService.CreateAsync(request, cancellationToken);
        _logger.LogInformation("Created menu item {Sku} in {Category} as {User}.",
            created.Sku, created.Category, User.Identity?.Name);
        return CreatedAtAction(nameof(GetByCategory), new { category = created.Category }, created);
    }

    /// <summary>Updates an existing item. Restricted to <c>Admin</c> and <c>Staff</c>.</summary>
    /// <param name="category">Category (table PartitionKey).</param>
    /// <param name="sku">Stock keeping unit (table RowKey).</param>
    /// <param name="request">Only the fields supplied are changed.</param>
    /// <response code="200">The item was updated.</response>
    /// <response code="400">The payload failed validation.</response>
    /// <response code="401">No valid Bearer token was supplied.</response>
    /// <response code="403">The caller is not an Admin or Staff member.</response>
    /// <response code="404">No such item.</response>
    /// <response code="409">The item changed while the update was in flight.</response>
    /// <response code="500">The MenuItems table could not be reached.</response>
    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("{category}/{sku}")]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MenuItemDto>> Update(
        [FromRoute] string category,
        [FromRoute] string sku,
        [FromBody] UpdateMenuItemRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _menuService.UpdateAsync(category, sku, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>Deletes an item. Restricted to <c>Admin</c> and <c>Staff</c>.</summary>
    /// <param name="category">Category (table PartitionKey).</param>
    /// <param name="sku">Stock keeping unit (table RowKey).</param>
    /// <response code="204">The item was deleted.</response>
    /// <response code="401">No valid Bearer token was supplied.</response>
    /// <response code="403">The caller is not an Admin or Staff member.</response>
    /// <response code="404">No such item.</response>
    /// <response code="500">The MenuItems table could not be reached.</response>
    [Authorize(Roles = "Admin,Staff")]
    [HttpDelete("{category}/{sku}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
        [FromRoute] string category, [FromRoute] string sku, CancellationToken cancellationToken)
    {
        await _menuService.DeleteAsync(category, sku, cancellationToken);
        return NoContent();
    }
}
