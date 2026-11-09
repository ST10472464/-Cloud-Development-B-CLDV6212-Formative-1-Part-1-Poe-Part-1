namespace CoffeeNChill.ApiGateway.Services;

// OWASP - Password Storage Cheat Sheet
// https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    // BCrypt.Net - BCrypt password hashing for .NET
    // https://github.com/BcryptNet/bcrypt.net
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        // BCrypt picks a fresh random salt per call and encodes cost + salt + hash together, so a stored value is self-describing and two identical passwords never share a hash.
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (FormatException)
        {
            // A stored value that is not a valid hash can never match, so this is a failed login rather than a server error.
            return false;
        }
    }
}
