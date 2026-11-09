namespace CoffeeNChill.ApiGateway.Common;

// Thrown from the service layer and turned into an RFC 7807 problem+json body by ExceptionHandlingMiddleware, so each type below corresponds to one HTTP status.
public abstract class ApiException : Exception
{
    protected ApiException(string message) : base(message) { }

    public abstract int StatusCode { get; }
}

public sealed class AppValidationException : ApiException
{
    public AppValidationException(string message) : base(message) { }

    public override int StatusCode => StatusCodes.Status400BadRequest;
}

public sealed class ResourceNotFoundException : ApiException
{
    public ResourceNotFoundException(string message) : base(message) { }

    public override int StatusCode => StatusCodes.Status404NotFound;
}

public sealed class ConflictException : ApiException
{
    public ConflictException(string message) : base(message) { }

    public override int StatusCode => StatusCodes.Status409Conflict;
}

// Deliberately reports the same message for an unknown account and a wrong password, so a caller cannot use login to discover which addresses are registered.
public sealed class UnauthorizedException : ApiException
{
    public UnauthorizedException(string message) : base(message) { }

    public override int StatusCode => StatusCodes.Status401Unauthorized;
}
