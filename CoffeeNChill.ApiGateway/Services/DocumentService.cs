using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CoffeeNChill.ApiGateway.Common;
using CoffeeNChill.ApiGateway.Dtos;
using CoffeeNChill.ApiGateway.Options;

namespace CoffeeNChill.ApiGateway.Services;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<UploadDocumentResponse> UploadAsync(
        string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task<(Stream Content, string ContentType, long Length, string FileName)> DownloadAsync(
        string fileName, CancellationToken cancellationToken = default);

    Task DeleteAsync(string fileName, CancellationToken cancellationToken = default);
}

// Microsoft Learn - Quickstart: Azure Blob Storage client library for .NET
// https://learn.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-dotnet?tabs=azure-portal%2Cidentity-based-access%2Cnodejs-v1%2Cpivots=programming-language-csharp
public sealed class DocumentService : IDocumentService
{
    private const string ContainerName = "staff-documents";

    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(StorageOptions storageOptions, ILogger<DocumentService> logger)
    {
        _containerClient = new BlobContainerClient(storageOptions.ConnectionString, ContainerName);
        _logger = logger;
    }

    public async Task<IReadOnlyList<DocumentDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var documents = new List<DocumentDto>();
        await foreach (var blob in _containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            documents.Add(new DocumentDto
            {
                Name = blob.Name,
                Size = blob.Properties.ContentLength ?? 0,
                ContentType = blob.Properties.ContentType ?? "application/octet-stream",
                LastModified = blob.Properties.LastModified
            });
        }

        return documents;
    }

    public async Task<UploadDocumentResponse> UploadAsync(
        string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var name = SanitiseFileName(fileName);
        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blob = _containerClient.GetBlobClient(name);
        await blob.UploadAsync(content, overwrite: true, cancellationToken);

        // Stored alongside the blob so DownloadAsync can restore the header rather than guessing.
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var headers = new BlobHttpHeaders { ContentType = contentType };
            await blob.SetHttpHeadersAsync(headers, cancellationToken: cancellationToken);
        }

        var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("Uploaded document {Name} ({Bytes} bytes).", name, properties.Value.ContentLength);

        return new UploadDocumentResponse
        {
            Name = name,
            Size = properties.Value.ContentLength,
            Message = $"Document '{name}' uploaded successfully."
        };
    }

    public async Task<(Stream Content, string ContentType, long Length, string FileName)> DownloadAsync(
        string fileName, CancellationToken cancellationToken = default)
    {
        var name = SanitiseFileName(fileName);
        var blob = _containerClient.GetBlobClient(name);

        try
        {
            var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return (
                download.Value.Content,
                download.Value.Details.ContentType ?? "application/octet-stream",
                download.Value.Details.ContentLength,
                name);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new ResourceNotFoundException($"Document '{name}' was not found.");
        }
    }

    public async Task DeleteAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var name = SanitiseFileName(fileName);
        var blob = _containerClient.GetBlobClient(name);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);

        _logger.LogInformation("Deleted document {Name}.", name);
    }

    // Blob names are flat, so anything path-like is discarded. Without this a caller could supply  "../../secrets.txt" and choose an unexpected key inside the container.
    private static string SanitiseFileName(string? fileName)
    {
        var name = (fileName ?? string.Empty).Trim().Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];

        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
            throw new AppValidationException("A document file name is required.");

        return name;
    }
}
