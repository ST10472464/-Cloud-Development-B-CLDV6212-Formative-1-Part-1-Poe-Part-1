using System.Net.Http.Headers;
using CoffeeNChill.ApiGateway.Options;

namespace CoffeeNChill.ApiGateway.Services;

public sealed record ForwardedRequest(
    string Method,
    string Path,
    Stream? Body,
    string? ContentType,
    string? Authorization);

public interface IFunctionsProxyService
{
    Task<HttpResponseMessage?> ForwardAsync(ForwardedRequest request, CancellationToken cancellationToken = default);
}

// Microsoft Learn - Guide for running C# Azure Functions in the isolated worker model
// https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=ihostapplicationbuilder%2Cconfig%2Cwindows
public sealed class FunctionsProxyService : IFunctionsProxyService
{
    public const string HttpClientName = "functions";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FunctionsProxyService> _logger;

    public FunctionsProxyService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<FunctionsProxyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HttpResponseMessage?> ForwardAsync(
        ForwardedRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseUrl = _configuration["AzureFunctions:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning(
                "AzureFunctions:BaseUrl is not configured; refusing to proxy {Path}.", request.Path);
            return null;
        }

        var path = (request.Path ?? string.Empty).TrimStart('/');
        var uri = new Uri($"{baseUrl.TrimEnd('/')}/{path}", UriKind.Absolute);

        using var message = new HttpRequestMessage(new HttpMethod(request.Method.ToUpperInvariant()), uri);

        if (request.Body is not null)
        {
            var content = new StreamContent(request.Body);
            if (!string.IsNullOrWhiteSpace(request.ContentType))
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
            message.Content = content;
        }

        if (!string.IsNullOrWhiteSpace(request.Authorization))
            message.Headers.TryAddWithoutValidation("Authorization", request.Authorization);

        var client = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            // ResponseHeadersRead hands the stream back instead of buffering it, and the message is deliberately not disposed here: the caller still has to read the body.
            return await client.SendAsync(
                message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach the Azure Functions host at {BaseUrl}.", baseUrl);
            throw new InvalidOperationException(
                $"The Azure Functions host at '{baseUrl}' could not be reached.", ex);
        }
    }
}
