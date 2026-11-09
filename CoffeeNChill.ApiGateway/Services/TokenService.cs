using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoffeeNChill.ApiGateway.Models;
using CoffeeNChill.ApiGateway.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoffeeNChill.ApiGateway.Services;

public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    IssuedToken Create(UserProfile user);
}

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(JwtOptions options)
    {
        _options = options;
    }

    public IssuedToken Create(UserProfile user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(_options.ExpiryMinutes);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        // Microsoft Learn - ClaimTypes Class
        // https://learn.microsoft.com/en-us/dotnet/api/system.security.claims.claimtypes?view=net-10.0
        // Written as the full ClaimTypes URIs rather than the short JWT names: these are the claim types JwtBearer maps onto ClaimsPrincipal.Name and IsInRole, which is what lets [Authorize(Roles = "Admin,Staff")] resolve against the token with no remapping.
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.RowKey),
            new(ClaimTypes.Role, user.PartitionKey),
            new(ClaimTypes.NameIdentifier, user.RowKey),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        // Microsoft Learn - JwtSecurityTokenHandler Class
        // https://learn.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytokenhandler?view=msal-14.x
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var handler = new JwtSecurityTokenHandler();
        return new IssuedToken(handler.WriteToken(token), expiresAtUtc);
    }
}
