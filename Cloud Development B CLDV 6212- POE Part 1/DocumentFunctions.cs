using Cloud_Development_B_CLDV_6212__POE_Part_1;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

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
            _blobServiceClient = new BlobServiceClient("UseDevelopmentStorage=true");
        }

        private async Task<BlobContainerClient> GetContainerClient()
        {
            var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
            await container.CreateIfNotExistsAsync(PublicAccessType.None);
            return container;
        }

        [Function("UploadStaffDocument")]
        public async Task<HttpResponseData> UploadStaffDocument(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload")] HttpRequestData req)
        {
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                if (string.IsNullOrEmpty(requestBody))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("No file data received.");
                    return badResponse;
                }

                var container = await GetContainerClient();
                var fileName = $"uploaded-document-{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
                var blobClient = container.GetBlobClient(fileName);

                var bytes = System.Text.Encoding.UTF8.GetBytes(requestBody);
                using var stream = new MemoryStream(bytes);
                await blobClient.UploadAsync(stream, overwrite: true);

                _logger.LogInformation("Uploaded document: {FileName} ({Size} bytes)", fileName, bytes.Length);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new
                {
                    fileName = fileName,
                    size = bytes.Length,
                    message = "Document uploaded successfully"
                });
                return response;
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
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                if (string.IsNullOrEmpty(requestBody))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("No file data received.");
                    return badResponse;
                }

                byte[] fileBytes;
                try
                {
                    fileBytes = Convert.FromBase64String(requestBody);
                }
                catch
                {
                    fileBytes = System.Text.Encoding.UTF8.GetBytes(requestBody);
                }

                var container = await GetContainerClient();
                var blobClient = container.GetBlobClient(fileName);

                using var stream = new MemoryStream(fileBytes);
                await blobClient.UploadAsync(stream, overwrite: true);

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
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", download.Value.Details.ContentType ?? "application/octet-stream");
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
    }
}
