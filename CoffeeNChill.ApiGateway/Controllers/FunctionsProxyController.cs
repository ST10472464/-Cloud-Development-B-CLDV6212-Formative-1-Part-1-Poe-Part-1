using System.Text;
using CoffeeNChill.ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.ApiGateway.Controllers;

// Microsoft Learn - Azure Functions HTTP triggers and bindings overview
// https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook?tabs=isolated-process&pivots=programming-language-csharp
// Microsoft Learn - Guide for running C# Azure Functions in the isolated worker model
// https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=ihostapplicationbuilder%2Cconfig%2Cwindows
[ApiController]
[Route("api/functions")]
[Tags("Functions Proxy")]
public class FunctionsProxyController : ControllerBase
{
    private readonly IFunctionsProxyService _proxyService;
    private readonly ILogger<FunctionsProxyController> _logger;

    public FunctionsProxyController(
        IFunctionsProxyService proxyService, ILogger<FunctionsProxyController> logger)
    {
        _proxyService = proxyService;
        _logger = logger;
    }

    /// <summary>Forwards a GET to the Azure Functions host.</summary>
    /// <param name="path">Function route, for example <c>menu</c>. Any query string is forwarded as-is.</param>
    /// <response code="200">The upstream returned a successful response.</response>
    /// <response code="503">No Functions base URL is configured, or the host is unreachable.</response>
    [HttpGet("{**path}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> Get([FromRoute] string? path, CancellationToken cancellationToken)
        => ForwardAsync("GET", path, Request.QueryString.Value, null, null, cancellationToken);

    /// <summary>Forwards a POST to the Azure Functions host.</summary>
    /// <param name="path">Function route, for example <c>orders/queue</c>.</param>
    /// <response code="200">The upstream returned a successful response.</response>
    /// <response code="503">No Functions base URL is configured, or the host is unreachable.</response>
    [HttpPost("{**path}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Post([FromRoute] string? path, CancellationToken cancellationToken)
    {
        var contentType = Request.Headers.ContentType.ToString();

        // Buffer the body so the upstream call is not tied to the client's request stream lifetime.
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        return await ForwardAsync(
            "POST", path, Request.QueryString.Value, buffer, contentType, cancellationToken);
    }

    private async Task<IActionResult> ForwardAsync(
        string method,
        string? path,
        string? queryString,
        Stream? body,
        string? contentType,
        CancellationToken cancellationToken)
    {
        var target = string.IsNullOrWhiteSpace(path) ? string.Empty : path;
        if (!string.IsNullOrWhiteSpace(queryString))
            target += queryString.StartsWith('?') ? queryString : $"?{queryString}";

        var request = new ForwardedRequest(
            method, target, body, contentType, Request.Headers.Authorization.ToString());

        var upstream = await _proxyService.ForwardAsync(request, cancellationToken);

        if (upstream is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                title = "Upstream not configured",
                detail = "Set AzureFunctions:BaseUrl to the Functions host address, " +
                         "for example http://localhost:7071.",
                status = StatusCodes.Status503ServiceUnavailable
            });
        }

        using (upstream)
        {
            var payload = await upstream.Content.ReadAsByteArrayAsync(cancellationToken);

            _logger.LogInformation("Proxied {Method} /{Path} -> {Status}.",
                method, target, (int)upstream.StatusCode);

            var mediaType = upstream.Content.Headers.ContentType?.MediaType
                            ?? "application/json";
            var encoding = upstream.Content.Headers.ContentType?.CharSet ?? "utf-8";

            return new ContentResult
            {
                Content = Encoding.UTF8.GetString(payload),
                ContentType = $"{mediaType}; charset={encoding}",
                StatusCode = (int)upstream.StatusCode
            };
        }
    }
}
