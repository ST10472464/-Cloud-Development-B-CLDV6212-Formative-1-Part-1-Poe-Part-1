using System.Security.Claims;
using CoffeeNChill.ApiGateway.Common;
using CoffeeNChill.ApiGateway.Dtos;
using CoffeeNChill.ApiGateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.ApiGateway.Controllers;

// Microsoft Learn - JwtBearer Class
// https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer?view=aspnetcore-10.0
// Microsoft Learn - ASP.NET Core authorization with roles
// https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[Tags("Authentication")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserService userService, ILogger<AuthController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new account. The password is hashed with BCrypt before it is written to the
    /// <c>Users</c> table, so the stored value is never the plain text supplied here.
    /// </summary>
    /// <param name="request">Account details, including the access tier to provision.</param>
    /// <response code="201">The account was created.</response>
    /// <response code="400">The payload failed validation.</response>
    /// <response code="409">An account with that email already exists.</response>
    /// <response code="500">The Users table could not be reached.</response>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserProfileDto>> Register(
        [FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var profile = await _userService.RegisterAsync(request, cancellationToken);

        _logger.LogInformation("Registered {Role} account {Email}.", profile.Role, profile.Email);
        return StatusCode(StatusCodes.Status201Created, profile);
    }

    /// <summary>
    /// Validates credentials against the <c>Users</c> table and returns a signed JWT Bearer token.
    /// The token carries <c>ClaimTypes.Name</c>, <c>ClaimTypes.Email</c> and
    /// <c>ClaimTypes.Role</c> and expires after <c>Jwt:ExpiryMinutes</c>.
    /// </summary>
    /// <param name="request">Email and plain-text password.</param>
    /// <response code="200">Credentials were valid and a token was issued.</response>
    /// <response code="400">The payload failed validation.</response>
    /// <response code="401">No account matched, or the password was wrong.</response>
    /// <response code="500">The Users table could not be reached.</response>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _userService.LoginAsync(request, cancellationToken);

        _logger.LogInformation("Login succeeded for {Role} account {Email}.", response.Role, response.Email);
        return Ok(response);
    }

    /// <summary>
    /// Echoes back the identity carried by the supplied token. Useful for confirming in Postman or
    /// Swagger that the three required claims round-trip correctly through
    /// <c>[Authorize]</c>.
    /// </summary>
    /// <response code="200">The token was accepted.</response>
    /// <response code="401">No valid Bearer token was supplied.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<UserProfileDto> Me(CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            throw new UnauthorizedException("The supplied token does not carry a ClaimTypes.Email claim.");

        return Ok(new UserProfileDto
        {
            Email = email,
            FullName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            Role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            CreatedDate = default
        });
    }
}
