using Cloud_Development_B_CLDV_6212__POE_Part_1;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;

namespace CoffeeNChill.Functions
{
    public class DocumentFunctions
    {
        private readonly ILogger<DocumentFunctions> _logger;
        private readonly BlobServiceClient _blobServiceClient;
        private const string ContainerName = "staff-documents";

        public DocumentFunctions(ILogger<DocumentFunctions> logger)
        {
            _logger = logger;
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "UseDevelopmentStorage=true";
            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        private async Task<BlobContainerClient> GetContainerClient()
        {
            var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
            await container.CreateIfNotExistsAsync(PublicAccessType.None);
            return container;
        }

        [Function("UploadStaffDocument")]
        public async Task<HttpResponseData> UploadStaffDocument(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload")] HttpRequestData req,
            FunctionContext context)
        {
            try
            {
                _logger.LogInformation("Processing upload request for a staff document.");

                string fileName = "uploaded-document";
                Stream? content = null;

                var hasContentType = req.Headers.TryGetValues("Content-Type", out var contentTypeValues);
                var contentType = hasContentType && contentTypeValues is not null ? string.Join(",", contentTypeValues) : string.Empty;

                if (contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                {
                    var httpContext = context.GetHttpContext();
                    if (httpContext is null)
                    {
                        var unsupportedResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await unsupportedResponse.WriteStringAsync("Multipart upload is not supported in this host configuration.");
                        return unsupportedResponse;
                    }

                    var formData = await httpContext.Request.ReadFormAsync();
                    var file = formData.Files.FirstOrDefault();
                    if (file != null)
                    {
                        fileName = file.FileName;
                        content = file.OpenReadStream();
                    }
                }

                if (content == null)
                {
                    var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                    if (string.IsNullOrEmpty(requestBody))
                    {
                        var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badResponse.WriteStringAsync("No file data received.");
                        return badResponse;
                    }
                    fileName = $"uploaded-document-{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
                    content = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
                }

                var container = await GetContainerClient();
                var blobClient = container.GetBlobClient(fileName);

                using (content)
                {
                    await blobClient.DeleteIfExistsAsync();
                    await blobClient.UploadAsync(content, new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = GetContentType(fileName) }
                    });

                    var blobProps = await blobClient.GetPropertiesAsync();
                    _logger.LogInformation("Uploaded document: {FileName} ({Size} bytes)", fileName, blobProps.Value.ContentLength);

                    var response = req.CreateResponse(HttpStatusCode.Created);
                    await response.WriteAsJsonAsync(new
                    {
                        fileName = fileName,
                        size = blobProps.Value.ContentLength,
                        message = "Document uploaded successfully"
                    });
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document to Azure Blob Storage");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error uploading document: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("UploadStaffDocumentBinary")]
        public async Task<HttpResponseData> UploadStaffDocumentBinary(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload/{fileName}")] HttpRequestData req,
            string fileName)
        {
            try
            {
                _logger.LogInformation("Processing upload request for {FileName}.", fileName);

                using var requestStream = new MemoryStream();
                await req.Body.CopyToAsync(requestStream);

                if (requestStream.Length == 0)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("No file data received.");
                    return badResponse;
                }

                byte[] fileBytes;
                var requestBody = Encoding.UTF8.GetString(requestStream.ToArray());
                var trimmed = requestBody.Trim();
                if (IsBase64String(trimmed))
                {
                    fileBytes = Convert.FromBase64String(trimmed);
                }
                else
                {
                    fileBytes = requestStream.ToArray();
                }

                var container = await GetContainerClient();
                var blobClient = container.GetBlobClient(fileName);

                using var fileStream = new MemoryStream(fileBytes);
                await blobClient.DeleteIfExistsAsync();
                await blobClient.UploadAsync(fileStream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = GetContentType(fileName) }
                });

                _logger.LogInformation("Uploaded document: {FileName} ({Size} bytes)", fileName, fileBytes.Length);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new
                {
                    fileName = fileName,
                    size = fileBytes.Length,
                    message = "Document uploaded successfully"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document {FileName} to Azure Blob Storage", fileName);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error uploading document: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("ListStaffDocuments")]
        public async Task<HttpResponseData> ListStaffDocuments(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents")] HttpRequestData req)
        {
            try
            {
                var container = await GetContainerClient();
                var documents = new List<object>();

                await foreach (var blobItem in container.GetBlobsAsync())
                {
                    documents.Add(new
                    {
                        fileName = blobItem.Name,
                        size = blobItem.Properties.ContentLength ?? 0,
                        lastModified = blobItem.Properties.LastModified?.UtcDateTime,
                        contentType = blobItem.Properties.ContentType
                    });
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(documents);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing documents from Azure Blob Storage");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error listing documents: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("DownloadStaffDocument")]
        public async Task<HttpResponseData> DownloadStaffDocument(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/download/{fileName}")] HttpRequestData req,
            string fileName)
        {
            try
            {
                var container = await GetContainerClient();
                var blobClient = container.GetBlobClient(fileName);

                if (!await blobClient.ExistsAsync())
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync($"Document '{fileName}' not found.");
                    return notFoundResponse;
                }

                var download = await blobClient.DownloadContentAsync();
                var contentType = GetContentType(fileName);
                if (contentType == "application/octet-stream")
                {
                    contentType = download.Value.Details.ContentType ?? contentType;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", contentType);
                response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                var contentBytes = download.Value.Content.ToArray();
                await response.Body.WriteAsync(contentBytes, 0, contentBytes.Length);

                _logger.LogInformation("Downloaded document: {FileName}", fileName);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {FileName} from Azure Blob Storage", fileName);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error downloading document: {ex.Message}");
                return errorResponse;
            }
        }

        private static bool IsBase64String(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            try
            {
                Convert.FromBase64String(value);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".csv" => "text/csv",
                _ => "application/octet-stream"
            };
        }
    }
}
