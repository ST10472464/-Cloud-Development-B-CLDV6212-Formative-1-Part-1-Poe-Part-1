using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.ApiGateway.Dtos;

/// <summary>Public shape of a <c>MenuItems</c> table row returned by the menu endpoints.</summary>
public sealed class MenuItemDto
{
    /// <summary>Category, stored as the table PartitionKey.</summary>
    public string Category { get; set; } = default!;

    /// <summary>Stock keeping unit, stored as the table RowKey.</summary>
    public string Sku { get; set; } = default!;

    /// <summary>Display name of the item.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Long description shown to students.</summary>
    public string Description { get; set; } = default!;

    /// <summary>Price in ZAR.</summary>
    public double Price { get; set; }

    /// <summary>Whether the item can currently be ordered.</summary>
    public bool IsAvailable { get; set; }
}

/// <summary>Payload for <c>POST /api/menu</c>.</summary>
public sealed class CreateMenuItemRequest
{
    /// <summary>Category, becomes the table PartitionKey.</summary>
    [Required(ErrorMessage = "Category is required.")]
    [MaxLength(100, ErrorMessage = "Category must be 100 characters or fewer.")]
    public string Category { get; set; } = default!;

    /// <summary>Stock keeping unit, becomes the table RowKey.</summary>
    [Required(ErrorMessage = "Sku is required.")]
    [MaxLength(100, ErrorMessage = "Sku must be 100 characters or fewer.")]
    public string Sku { get; set; } = default!;

    /// <summary>Display name of the item.</summary>
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name must be 200 characters or fewer.")]
    public string Name { get; set; } = default!;

    /// <summary>Long description shown to students.</summary>
    [MaxLength(2000, ErrorMessage = "Description must be 2000 characters or fewer.")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Price in ZAR. Must be greater than zero.</summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
    public double Price { get; set; }

    /// <summary>Whether the item can currently be ordered.</summary>
    public bool IsAvailable { get; set; } = true;
}

/// <summary>Payload for <c>PUT /api/menu/{category}/{sku}</c>. Only supplied fields are changed.</summary>
public sealed class UpdateMenuItemRequest
{
    /// <summary>New display name. Leave null to keep the current value.</summary>
    [MaxLength(200, ErrorMessage = "Name must be 200 characters or fewer.")]
    public string? Name { get; set; }

    /// <summary>New description. Leave null to keep the current value.</summary>
    [MaxLength(2000, ErrorMessage = "Description must be 2000 characters or fewer.")]
    public string? Description { get; set; }

    /// <summary>New price. Leave null to keep the current value.</summary>
    [Range(0.0, double.MaxValue, ErrorMessage = "Price cannot be negative.")]
    public double? Price { get; set; }

    /// <summary>New availability flag. Leave null to keep the current value.</summary>
    public bool? IsAvailable { get; set; }
}
