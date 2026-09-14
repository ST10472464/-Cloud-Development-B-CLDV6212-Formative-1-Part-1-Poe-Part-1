using Azure;
using Azure.Data.Tables;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Cloud_Development_B_CLDV_6212__POE_Part_1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CoffeeNChill.Functions
{
    public class DocumentFunctions
    {
        private readonly ILogger<DocumentFunctions> _logger;
        private readonly string _connectionString;
        private const string ShareName = "staff-docs";

        public DocumentFunctions(ILogger<DocumentFunctions> logger)
        {
            _logger = logger;
            _connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "UseDevelopmentStorage=true";
        }

        [Function("UploadStaffDocument")]
        public async Task<HttpResponseData> UploadStaffDocument(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload")] HttpRequestData req)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrEmpty(requestBody))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("No file data received.");
                return badResponse;
            }

            var shareServiceClient = new ShareServiceClient(_connectionString);
            var shareClient = shareServiceClient.GetShareClient(ShareName);
            await shareClient.CreateIfNotExistsAsync();

            var directoryClient = shareClient.GetRootDirectoryClient();
            var fileClient = directoryClient.GetFileClient("uploaded-document");

            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(requestBody));
            await fileClient.CreateAsync(stream.Length);
            await fileClient.UploadRangeAsync(new HttpRange(0, stream.Length), stream);

            _logger.LogInformation("Uploaded document: uploaded-document ({Size} bytes)", stream.Length);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new
            {
                fileName = "uploaded-document",
                size = stream.Length,
                message = "Document uploaded successfully"
            });
            return response;
        }

        [Function("UploadStaffDocumentBinary")]
        public async Task<HttpResponseData> UploadStaffDocumentBinary(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload/{fileName}")] HttpRequestData req,
            string fileName)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrEmpty(requestBody))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("No file data received.");
                return badResponse;
            }

            var shareServiceClient = new ShareServiceClient(_connectionString);
            var shareClient = shareServiceClient.GetShareClient(ShareName);
            await shareClient.CreateIfNotExistsAsync();

            var directoryClient = shareClient.GetRootDirectoryClient();
            var fileClient = directoryClient.GetFileClient(fileName);

            byte[] fileBytes = Convert.FromBase64String(requestBody);
            var stream = new MemoryStream(fileBytes);
            await fileClient.CreateAsync(stream.Length);
            await fileClient.UploadRangeAsync(new HttpRange(0, stream.Length), stream);

            _logger.LogInformation("Uploaded document: {FileName} ({Size} bytes)", fileName, stream.Length);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new
            {
                fileName = fileName,
                size = stream.Length,
                message = "Document uploaded successfully"
            });
            return response;
        }

        [Function("ListStaffDocuments")]
        public async Task<HttpResponseData> ListStaffDocuments(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents")] HttpRequestData req)
        {
            var shareServiceClient = new ShareServiceClient(_connectionString);
            var shareClient = shareServiceClient.GetShareClient(ShareName);
            await shareClient.CreateIfNotExistsAsync();

            var documents = new List<object>();
            var directoryClient = shareClient.GetRootDirectoryClient();

            await foreach (ShareFileItem item in directoryClient.GetFilesAndDirectoriesAsync())
            {
                if (!item.IsDirectory)
                {
                    documents.Add(new
                    {
                        fileName = item.Name
                    });
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(documents);
            return response;
        }

        }
}