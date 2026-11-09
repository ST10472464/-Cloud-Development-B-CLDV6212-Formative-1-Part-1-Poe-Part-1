using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.ApiGateway.Dtos;

/// <summary>Payload for <c>POST /api/auth/register</c>.</summary>
public sealed class RegisterRequest
{
    /// <summary>Full display name of the account, becomes the <c>ClaimTypes.Name</c> claim.</summary>
    [Required(ErrorMessage = "FullName is required.")]
    [MinLength(2, ErrorMessage = "FullName must be at least 2 characters.")]
    [MaxLength(100, ErrorMessage = "FullName must be 100 characters or fewer.")]
    public string FullName { get; set; } = default!;

    /// <summary>Email / username, becomes the <c>ClaimTypes.Email</c> claim and the table RowKey.</summary>
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(254, ErrorMessage = "Email must be 254 characters or fewer.")]
    public string Email { get; set; } = default!;

    /// <summary>Plain-text password. Hashed with BCrypt before it ever reaches storage.</summary>
    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [MaxLength(128, ErrorMessage = "Password must be 128 characters or fewer.")]
    public string Password { get; set; } = default!;

    /// <summary>Access tier to provision: <c>Admin</c>, <c>Staff</c> or <c>Customer</c>.</summary>
    [Required(ErrorMessage = "Role is required.")]
    [RegularExpression("^(Admin|Staff|Customer)$",
        ErrorMessage = "Role must be one of: Admin, Staff, Customer.")]
    public string Role { get; set; } = "Customer";
}

/// <summary>Payload for <c>POST /api/auth/login</c>.</summary>
public sealed class LoginRequest
{
    /// <summary>Email / username of an existing account.</summary>
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string Email { get; set; } = default!;

    /// <summary>Plain-text password to verify against the stored BCrypt hash.</summary>
    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = default!;
}

/// <summary>
/// Response from <c>POST /api/auth/login</c>. <see cref="Token"/> is a signed JWT Bearer token
/// carrying <c>ClaimTypes.Name</c>, <c>ClaimTypes.Email</c> and <c>ClaimTypes.Role</c>.
/// </summary>
public sealed class AuthResponse
{
    /// <summary>Signed JWT. Send it as <c>Authorization: Bearer {token}</c>.</summary>
    public string Token { get; set; } = default!;

    /// <summary>Always <c>Bearer</c>.</summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>UTC expiry instant of <see cref="Token"/>.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Email the token was issued for.</summary>
    public string Email { get; set; } = default!;

    /// <summary>Full name the token was issued for.</summary>
    public string FullName { get; set; } = default!;

    /// <summary>Access tier carried in the <c>ClaimTypes.Role</c> claim.</summary>
    public string Role { get; set; } = default!;
}

/// <summary>
/// Public projection of a <c>Users</c> table row. <c>PasswordHash</c> is deliberately absent so a
/// hash can never leak through an API response.
/// </summary>
public sealed class UserProfileDto
{
    /// <summary>Email / username (table RowKey).</summary>
    public string Email { get; set; } = default!;

    /// <summary>Display name (table FullName).</summary>
    public string FullName { get; set; } = default!;

    /// <summary>Access tier (table PartitionKey).</summary>
    public string Role { get; set; } = default!;

    /// <summary>UTC account creation timestamp.</summary>
    public DateTime CreatedDate { get; set; }
}
