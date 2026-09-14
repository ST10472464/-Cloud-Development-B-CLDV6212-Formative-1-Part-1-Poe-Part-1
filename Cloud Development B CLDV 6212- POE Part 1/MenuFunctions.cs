using Azure;
using Azure.Data.Tables;
using Cloud_Development_B_CLDV_6212__POE_Part_1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CoffeeNChill.Functions
{
    // Microsoft Learn - Azure Functions C# isolated worker
    // https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide
    public class MenuFunctions
    {
        private readonly ILogger<MenuFunctions> _logger;
        private readonly string _connectionString;
        private const string TableName = "MenuItems";

        public MenuFunctions(ILogger<MenuFunctions> logger)
        {
            _logger = logger;
            _connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "UseDevelopmentStorage=true";
        }

        [Function("CreateMenuItem")]
        public async Task<HttpResponseData> CreateMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "menu")] HttpRequestData req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            MenuItem? menuItem;
            try
            {
                menuItem = JsonSerializer.Deserialize<MenuItem>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                _logger.LogWarning("Invalid JSON payload received for create menu item.");
                return await BadRequestAsync(req, "Invalid JSON payload in the request body.");
            }

            if (menuItem == null ||
                string.IsNullOrWhiteSpace(menuItem.PartitionKey) ||
                string.IsNullOrWhiteSpace(menuItem.RowKey) ||
                string.IsNullOrWhiteSpace(menuItem.Name) ||
                menuItem.Price <= 0)
            {
                _logger.LogWarning("Menu item validation failed. PartitionKey, RowKey, Name and a positive Price are required.");
                return await BadRequestAsync(req,
                    "Validation failed. Fields 'PartitionKey' (Category), 'RowKey' (SKU), 'Name' and a positive 'Price' are required.");
            }

            // Microsoft Learn - Azure Tables client library
            // https://learn.microsoft.com/en-us/dotnet/api/overview/azure/data.tables-readme?view=azure-dotnet
            try
            {
                var tableClient = new TableClient(_connectionString, TableName);
                await tableClient.CreateIfNotExistsAsync();
                await tableClient.AddEntityAsync(menuItem);

                _logger.LogInformation("Created menu item {RowKey} in category {PartitionKey}.", menuItem.RowKey, menuItem.PartitionKey);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(menuItem);
                return response;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                _logger.LogWarning("Menu item {RowKey} already exists in category {PartitionKey}.", menuItem.RowKey, menuItem.PartitionKey);
                return await BadRequestAsync(req,
                    $"A menu item with SKU '{menuItem.RowKey}' already exists in category '{menuItem.PartitionKey}'.");
            }
        }

        // Microsoft Learn - QueryAsync
        // https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.tableclient.queryasync?view=azure-dotnet
        [Function("GetAllMenuItems")]
        public async Task<HttpResponseData> GetAllMenuItems(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu")] HttpRequestData req)
        {
            var tableClient = new TableClient(_connectionString, TableName);
            await tableClient.CreateIfNotExistsAsync();

            var menuItems = new List<MenuItem>();
            await foreach (var item in tableClient.QueryAsync<MenuItem>())
            {
                menuItems.Add(item);
            }

            _logger.LogInformation("Returned {Count} menu items.", menuItems.Count);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(menuItems);
            return response;
        }

        // Microsoft Learn - Query with filter
        // https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger?tabs=python-v2%2Cisolated-process%2Cnodejs-v4%2Cfunctionsv2&pivots=programming-language-csharp
        [Function("GetMenuItemsByCategory")]
        public async Task<HttpResponseData> GetMenuItemsByCategory(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/category/{category}")] HttpRequestData req,
            string category)
        {
            var tableClient = new TableClient(_connectionString, TableName);
            await tableClient.CreateIfNotExistsAsync();

            var menuItems = new List<MenuItem>();
            string filter = $"PartitionKey eq '{category}'";

            await foreach (var item in tableClient.QueryAsync<MenuItem>(filter: filter))
            {
                menuItems.Add(item);
            }

            _logger.LogInformation("Returned {Count} menu items in category {Category}.", menuItems.Count, category);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(menuItems);
            return response;
        }

        // Microsoft Learn - UpdateEntityAsync
        // https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger?tabs=python-v2%2Cisolated-process%2Cnodejs-v4%2Cfunctionsv2&pivots=programming-language-csharp
        [Function("UpdateMenuItem")]
        public async Task<HttpResponseData> UpdateMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "menu/{category}/{id}")] HttpRequestData req,
            string category, string id)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            MenuItem? updatedData;
            try
            {
                updatedData = JsonSerializer.Deserialize<MenuItem>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                _logger.LogWarning("Invalid JSON payload received for update menu item.");
                return await BadRequestAsync(req, "Invalid JSON payload in the request body.");
            }

            if (updatedData == null || updatedData.Price < 0)
            {
                _logger.LogWarning("Update validation failed for {Category}/{Id}.", category, id);
                return await BadRequestAsync(req, "Validation failed. Body must contain a valid item and 'Price' cannot be negative.");
            }

            var tableClient = new TableClient(_connectionString, TableName);
            await tableClient.CreateIfNotExistsAsync();

            try
            {
                var existingItem = await tableClient.GetEntityAsync<MenuItem>(category, id);

                existingItem.Value.Price = updatedData.Price;
                existingItem.Value.IsAvailable = updatedData.IsAvailable;
                if (!string.IsNullOrEmpty(updatedData.Name)) existingItem.Value.Name = updatedData.Name;
                if (!string.IsNullOrEmpty(updatedData.Description)) existingItem.Value.Description = updatedData.Description;

                await tableClient.UpdateEntityAsync(existingItem.Value, existingItem.Value.ETag, TableUpdateMode.Replace);

                _logger.LogInformation("Updated menu item {Id} in category {Category}.", id, category);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(existingItem.Value);
                return response;
            }
            catch (RequestFailedException)
            {
                _logger.LogWarning("Menu item {Id} not found in category {Category}.", id, category);
                return await NotFoundAsync(req, $"Menu item with category '{category}' and ID '{id}' not found.");
            }
        }

        // Microsoft Learn - DeleteEntityAsync
        // https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.tableclient.deleteentityasync?view=azure-dotnet
        [Function("DeleteMenuItem")]
        public async Task<HttpResponseData> DeleteMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu/{category}/{id}")] HttpRequestData req,
            string category, string id)
        {
            var tableClient = new TableClient(_connectionString, TableName);
            await tableClient.CreateIfNotExistsAsync();

            try
            {
                await tableClient.DeleteEntityAsync(category, id);
                _logger.LogInformation("Deleted menu item {Id} in category {Category}.", id, category);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync($"Menu item '{id}' deleted successfully.");
                return response;
            }
            catch (RequestFailedException)
            {
                _logger.LogWarning("Menu item {Id} not found in category {Category} during delete.", id, category);
                return await NotFoundAsync(req, $"Menu item with category '{category}' and ID '{id}' not found.");
            }
        }

        private static async Task<HttpResponseData> BadRequestAsync(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync(message);
            return response;
        }

        private static async Task<HttpResponseData> NotFoundAsync(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.NotFound);
            await response.WriteStringAsync(message);
            return response;
        }
    }
}