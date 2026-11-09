namespace CoffeeNChill.ApiGateway.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public const string DevelopmentSecretKey =
        "CoffeeNChill-Local-Development-Secret-Key-Do-Not-Use-In-Production-0123456789";

    public string Issuer { get; set; } = "CoffeeNChill";

    public string Audience { get; set; } = "CoffeeNChill Clients";

    public string SecretKey { get; set; } = DevelopmentSecretKey;

    public int ExpiryMinutes { get; set; } = 60;

    // Validated once at startup so a weak or missing key fails immediately rather than on the first login attempt, and so a deployment cannot ship with the committed development key.
    public void EnsureValid(string environmentName)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Issuer))
            errors.Add("Jwt:Issuer must be set.");
        if (string.IsNullOrWhiteSpace(Audience))
            errors.Add("Jwt:Audience must be set.");
        if (string.IsNullOrWhiteSpace(SecretKey) || SecretKey.Length < 32)
            errors.Add("Jwt:SecretKey must be at least 32 characters (HMAC-SHA256 minimum).");
        if (ExpiryMinutes <= 0)
            errors.Add("Jwt:ExpiryMinutes must be greater than zero.");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "JWT configuration is invalid:" + Environment.NewLine + " - " +
                string.Join(Environment.NewLine + " - ", errors));

        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(SecretKey, DevelopmentSecretKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Jwt:SecretKey is still the committed development key. Override it with the " +
                "Jwt__SecretKey environment variable before running outside Development.");
        }
    }
}
