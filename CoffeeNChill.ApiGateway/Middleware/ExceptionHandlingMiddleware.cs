using System.Text.Json;
using Azure;
using CoffeeNChill.ApiGateway.Common;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.ApiGateway.Middleware;

// IETF - RFC 7807 Problem Details for HTTP APIs
// https://datatracker.ietf.org/doc/html/rfc7807
// Microsoft Learn - ProblemDetails in ASP.NET Core
// https://learn.microsoft.com/en-us/aspnet/core/web-api/handle-errors?view=aspnetcore-10.0
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        // The caller hung up, so there is nobody left to answer and the response cannot be written.
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request {Path} was cancelled by the client.", context.Request.Path);
            return;
        }

        var (status, title, detail, logLevel) = Classify(exception);

        if (logLevel == LogLevel.Error)
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}.",
                context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning(exception, "Request {Method} {Path} failed: {Message}",
                context.Request.Method, context.Request.Path, exception.Message);

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Could not write the error response for {Path}; the response had already started.",
                context.Request.Path);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }), context.RequestAborted);
    }

    private static (int Status, string Title, string Detail, LogLevel Level) Classify(Exception exception)
        => exception switch
        {
            AppValidationException validation => (
                validation.StatusCode, "Validation failed", validation.Message, LogLevel.Warning),

            ResourceNotFoundException notFound => (
                notFound.StatusCode, "Resource not found", notFound.Message, LogLevel.Warning),

            ConflictException conflict => (
                conflict.StatusCode, "Resource already exists", conflict.Message, LogLevel.Warning),

            UnauthorizedException unauthorized => (
                unauthorized.StatusCode, "Authentication failed", unauthorized.Message, LogLevel.Warning),

            BadHttpRequestException malformed => (
                StatusCodes.Status400BadRequest, "Malformed request",
                "The request body could not be read as valid JSON.", LogLevel.Warning),

            // Storage already reports the precise status (404, 409, 403 ...); collapsing those
            // into a generic 500 would hide what actually went wrong upstream.
            RequestFailedException storage => (
                storage.Status, "Azure Storage request failed",
                storage.Message, LogLevel.Warning),

            InvalidOperationException unavailable => (
                StatusCodes.Status503ServiceUnavailable, "Dependency unavailable",
                unavailable.Message, LogLevel.Error),

            // Unknown fault: log everything, but hand the caller only the traceId.
            _ => (
                StatusCodes.Status500InternalServerError, "An unexpected error occurred",
                "An unexpected error occurred while processing the request. " +
                "Quote the traceId when reporting this.", LogLevel.Error)
        };
}
