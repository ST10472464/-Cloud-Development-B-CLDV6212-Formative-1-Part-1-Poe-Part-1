using Azure;
using Azure.Data.Tables;
using CoffeeNChill.ApiGateway.Common;
using CoffeeNChill.ApiGateway.Dtos;
using CoffeeNChill.ApiGateway.Mapping;
using CoffeeNChill.ApiGateway.Models;
using CoffeeNChill.ApiGateway.Options;

namespace CoffeeNChill.ApiGateway.Services;

public interface IUserService
{
    Task<UserProfileDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<UserProfile?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
}

public sealed class UserService : IUserService
{
    private const string TableName = "Users";

    private readonly TableClient _tableClient;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        StorageOptions storageOptions,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<UserService> logger)
    {
        _tableClient = new TableClient(storageOptions.ConnectionString, TableName);
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<UserProfileDto> RegisterAsync(
        RegisterRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fullName = request.FullName?.Trim() ?? string.Empty;
        var email = NormaliseEmail(request.Email);
        var role = request.Role?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fullName))
            throw new AppValidationException("FullName is required.");
        if (string.IsNullOrWhiteSpace(email))
            throw new AppValidationException("Email is required.");
        if (password.Length < 8)
            throw new AppValidationException("Password must be at least 8 characters.");
        if (role is not ("Admin" or "Staff" or "Customer"))
            throw new AppValidationException("Role must be one of: Admin, Staff, Customer.");

        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        // Checked up front so registration can answer 409 with a clear message instead of depending on how the storage layer reports a duplicate RowKey.
        if (await FindByEmailAsync(email, cancellationToken) is not null)
            throw new ConflictException($"An account with email '{email}' already exists.");

        var user = new UserProfile
        {
            PartitionKey = role,
            RowKey = email,
            PasswordHash = _passwordHasher.Hash(password),
            FullName = fullName,
            CreatedDate = DateTime.UtcNow
        };

        try
        {
            await _tableClient.AddEntityAsync(user, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new ConflictException($"An account with email '{email}' already exists.");
        }

        _logger.LogInformation("Registered {Role} account {Email}.", role, email);
        return user.ToDto();
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = NormaliseEmail(request.Email);
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new UnauthorizedException("Invalid email or password.");

        var user = await FindByEmailAsync(email, cancellationToken);

        // Identical message for an unknown account and a wrong password, so login cannot be used to discover which addresses are registered.
        if (user is null)
        {
            _logger.LogWarning("Login failed for {Email}: no such account.", email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!_passwordHasher.Verify(password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed for {Email}: password mismatch.", email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        var issued = _tokenService.Create(user);
        _logger.LogInformation("Issued token for {Role} account {Email}.", user.PartitionKey, email);

        return new AuthResponse
        {
            Token = issued.Token,
            TokenType = "Bearer",
            ExpiresAtUtc = issued.ExpiresAtUtc,
            Email = user.RowKey,
            FullName = user.FullName,
            Role = user.PartitionKey
        };
    }

    // Microsoft Learn - TableClient.QueryAsync Method
    // https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.tableclient.queryasync?view=azure-dotnet
    public async Task<UserProfile?> FindByEmailAsync(
        string email, CancellationToken cancellationToken = default)
    {
        var normalised = NormaliseEmail(email);
        if (string.IsNullOrWhiteSpace(normalised))
            return null;

        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        // The email is the RowKey, but it sits in whichever role partition the account belongs to, and login does not know the role yet, so this has to scan every partition. Single quotes are doubled to keep the OData filter well formed.
        var filter = $"RowKey eq '{normalised.Replace("'", "''")}'";

        await foreach (var user in _tableClient
                           .QueryAsync<UserProfile>(filter: filter, cancellationToken: cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            return user;
        }

        return null;
    }

    private static string NormaliseEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
