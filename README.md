# CoffeeNChill - Cloud Development B (CLDV6212) Part 2

## POE Part 2 - Queue Triggers & Docker Compose Orchestration

---

### Team Members

| Name | Role |
|------|------|
| Keyur Keshav (ST10472464) | Team Member 1 |
| Yadav Iserbelas (ST10472501) | Team Member 2 |
| Saiyen Subban (ST10466873) | Team Member 3 |

---

## Project Overview

CoffeeNChill is a cloud-enabled canteen management system for a campus environment. This repository contains **Part 2** of the POE: asynchronous order queuing via Azure Storage Queues, a queue-triggered Azure Function that tracks the order lifecycle in Azure Table Storage, and multi-container orchestration with Docker Compose pulling images from Docker Hub - all running locally against the **Azurite** storage emulator.

Part 1 (Azure Table Storage Menu CRUD API and Azure Blob Storage document API) is retained in this repository as the foundation that Part 2 builds on.

### Part 2 Deliverables

- Queue producer endpoint `POST /api/orders/queue` pushing to `order-processing-queue`
- JSON payload serialisation (`OrderId`, `CustomerName`, `SelectedItemSKUs`, `TotalPrice`, `OrderTimestamp`)
- Input validation and error handling on the order payload
- Queue-triggered function `ProcessOrderQueue` with `[QueueTrigger("order-processing-queue")]`
- `Orders` Azure Table (`PartitionKey`: OrderDate, `RowKey`: OrderId, `Status`: Received)
- Order lifecycle transitions: Received -> Preparing -> Ready -> Collected
- Poison queue handling and exception logging
- Root `docker-compose.yml` orchestrating Azurite and the Function host from Docker Hub images
- Updated Postman collection covering order placement and status verification

---

## Part 2 - Order Queue Endpoints

| Method | Route                              | Description                                        | Status Codes |
| ------ | ---------------------------------- | -------------------------------------------------- | ------------ |
| POST   | `/api/orders/queue`                | Validate an order and push it to `order-processing-queue` | `200`, `400`, `500` |
| GET    | `/api/orders/{date}/{orderId}`     | Read a single order's status from the `Orders` table | `200`, `404` |
| GET    | `/api/orders/{date}`               | List all orders placed on a given date              | `200`        |

### Example Request - Place an Order

`POST http://localhost:7071/api/orders/queue`

```json
{
  "OrderId": "ORD-2026-8801",
  "CustomerName": "Jane Smith",
  "SelectedItemSKUs": ["COF-001", "PAS-104"],
  "TotalPrice": 65.00,
  "OrderTimestamp": "2026-10-12T10:15:00Z"
}
```

The endpoint rejects a payload with `400 Bad Request` when `OrderId`, `CustomerName` or `SelectedItemSKUs` are missing/empty, when `TotalPrice` is not greater than zero, or when the body is not valid JSON. A missing `OrderTimestamp` is generated as UTC at the time of the request. On success the message is Base64-encoded and written to `order-processing-queue`, returning:

```json
{ "message": "Order queued successfully", "orderId": "ORD-2026-8801" }
```

If the queue connection fails, the endpoint logs the exception and returns `500 Internal Server Error`.

### Orders Table Schema

| Property | Type | Notes |
| ------------- | --------- | ------------------------------------------------- |
| `PartitionKey` | string | Order date, formatted `yyyy-MM-dd` |
| `RowKey` | string | `OrderId`, e.g. `ORD-2026-8801` |
| `CustomerName` | string | Customer placing the order |
| `SelectedItemSKUs` | string | Comma-separated SKU list |
| `TotalPrice` | double | Order total |
| `Status` | string | `Received` -> `Preparing` -> `Ready` -> `Collected` |
| `OrderTimestamp` | datetime | Order timestamp (UTC) |
| `ETag` | ETag | Concurrency token (Azure Tables) |
| `Timestamp` | datetime | Server-managed |

---

## Part 1 Deliverables

- Azure Table Storage emulated by **Azurite**
- `MenuItems` Azure Table entity with `PartitionKey` (Category) and `RowKey` (SKU)
- HTTP-triggered Azure Functions:
  - Create Menu Item
  - Get All Menu Items
  - Get Menu Items By Category
  - Update Menu Item
  - Delete Menu Item
- Azure **Blob Storage** (`staff-documents` container) emulated by **Azurite**
- HTTP-triggered document functions:
  - Upload Staff Document
  - List Staff Documents
  - Download Staff Document
- Input validation and proper HTTP status codes (`200`, `201`, `400`, `404`)
- Docker containerization for local development

---

## Menu API Endpoints

| Method | Route                             | Description                                 | Status Codes        |
| ------ | --------------------------------- | ------------------------------------------- | ------------------- |
| POST   | `/api/menu`                       | Create a new menu item                      | `201`, `400`, `409` |
| GET    | `/api/menu`                       | Get all menu items                          | `200`               |
| GET    | `/api/menu/category/{category}`   | Get menu items filtered by category         | `200`               |
| PUT    | `/api/menu/{category}/{id}`       | Update a menu item (price / availability)   | `200`, `400`, `404` |
| DELETE | `/api/menu/{category}/{id}`       | Delete a menu item                          | `200`, `404`        |

### Example Request - Create Menu Item

```json
{
  "PartitionKey": "Hot Drinks",
  "RowKey": "COF-001",
  "Name": "Espresso",
  "Description": "Single shot of freshly pulled espresso",
  "Price": 18.50,
  "IsAvailable": true
}
```

### Example Request - Upload a Staff Document

Send a `POST` request to `/api/documents/upload` with a `multipart/form-data` file body.

```bash
curl -X POST http://localhost:7071/api/documents/upload \
  -F "file=@barista-recipe-sheet.pdf"
```

Alternatively, upload with a custom file name via `POST /api/documents/upload/{fileName}` sending the file bytes (Base64-encoded) in the request body.

---

## Document API Endpoints

| Method | Route                                 | Description                                        | Status Codes        |
| ------ | ------------------------------------- | -------------------------------------------------- | ------------------- |
| POST   | `/api/documents/upload`               | Upload a staff document (file body)                | `201`, `400`        |
| POST   | `/api/documents/upload/{fileName}`    | Upload a document with a specific file name        | `201`, `400`        |
| GET    | `/api/documents`                      | List all stored documents                          | `200`               |
| GET    | `/api/documents/download/{fileName}`  | Download a stored document                         | `200`, `404`        |

---

## staff-documents Blob Storage Schema

| Property    | Type   | Notes                                      |
| ----------- | ------ | ------------------------------------------ |
| Blob name   | string | e.g. `barista-recipe-sheet.pdf`            |
| Container   | string | Logical container `staff-documents`        |
| Content     | bytes  | Raw file content streamed to/from Azurite  |
| Size        | long   | Byte size of the file (reported on upload) |
| Last Modified | datetime | Timestamp of last modification          |
| Content Type  | string | MIME type of the stored blob             |

---

## MenuItems Table Schema

| Property | Type | Notes |
| ------------- | --------- | ------------------------------------------------- |
| `PartitionKey` | string | Category, e.g. `Hot Drinks`, `Cold Drinks`, `Pastries`, `Sandwiches` |
| `RowKey` | string | Unique item SKU / ID, e.g. `COF-001`, `PAS-104` |
| `Name` | string | Item name |
| `Description` | string | Short menu description |
| `Price` | double | Item price |
| `IsAvailable` | boolean | Availability status |
| `ETag` | ETag | Concurrency token (Azure Tables) |
| `Timestamp` | datetime | Server-managed |

---

## Local Setup

### 1. Prerequisites

- .NET 10 SDK
- Azure Functions Core Tools
- Docker Desktop
- Azurite (via Docker)

### 2. Run Azurite in an isolated container

```bash
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite mcr.microsoft.com/azure-storage/azurite
```

Ports:

- `10000` - Blob storage
- `10001` - Queue storage
- `10002` - Table storage

### 3. local.settings.json

The `local.settings.json` file points to Azurite using the development storage connection string:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

> `UseDevelopmentStorage=true` automatically targets the local Azurite emulator on ports 10000-10002 (Blob, Queue, Table).

### 4. Run the Functions locally

```bash
cd "Cloud Development B CLDV 6212- POE Part 2"
func start
```

---

## Docker Execution (Standalone, no Compose)

### Docker Hub Images

| Image | Tag | Source |
|---|---|---|
| `st10472501/coffeennchill-functions` | `:v1.0` | This project's Dockerfile |
| `st10472501/coffeennchill-azurite` | `:v1.0` | Custom Azurite image (Dockerfile.azurite) |

### Azurite Container (from Docker Hub)

```bash
docker pull st10472501/coffeennchill-azurite:v1.0
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite st10472501/coffeennchill-azurite:v1.0
```

### Build the Functions image

```bash
docker build -t st10472501/coffeennchill-functions:v1.0 .
```

### Run the Functions container (must be on Azurite's network)

```bash
docker run -p 7058:80 -e AzureWebJobsStorage="UseDevelopmentStorage=true" -e FUNCTIONS_WORKER_RUNTIME=dotnet-isolated st10472501/coffeennchill-functions:v1.0
```

### Push to Docker Hub

```bash
docker push st10472501/coffeennchill-functions:v1.0
```

---

## Team Member Contributions

### Team Member 1 - Keyur Keshav (Azure Storage Queue Producer & Order Validation)

**Part 2**

- Built the queue producer endpoint `POST /api/orders/queue` (`OrderFunctions.cs`) that accepts incoming order details
- Implemented C# serialisation of the order into the required JSON message payload (`OrderId`, `CustomerName`, `SelectedItemSKUs`, `TotalPrice`, `OrderTimestamp`) using `System.Text.Json`
- Established the connection to `order-processing-queue` in Azurite through the `AzureWebJobsStorage` connection string (`UseDevelopmentStorage=true`), creating the queue with `CreateIfNotExistsAsync`
- Added input validation covering `OrderId`, `CustomerName`, `SelectedItemSKUs`, `TotalPrice` and `OrderTimestamp`, with `400 Bad Request` responses and warning logs for each failure case
- Added robust error handling: malformed JSON is caught as `JsonException` and rejected with `400`, while queue connection/payload failures are caught, logged and returned as `500 Internal Server Error`
- Base64-encoded the serialised payload before `SendMessageAsync` so the message is valid for Azure Storage Queues
- Updated the Postman collection in `/Docs` with sample order placement requests, including a valid order and a `TotalPrice: 0` validation-rejection case

**Part 1**

- Set up the C# Azure Functions isolated project structure
- Configured `local.settings.json` to connect to Azurite (`UseDevelopmentStorage=true`)
- Created the `MenuItem` entity model using `PartitionKey` (Category) and `RowKey` (SKU/ID)
- Implemented all HTTP-triggered Menu functions (Create, Get All, Get By Category, Update, Delete)
- Added input validation and proper HTTP status codes (`400` Bad Request, `404` Not Found, `409` Conflict)
- Tested and verified all Menu CRUD endpoints (POST, GET, PUT, DELETE) using Postman against the local Azurite storage emulator running in Docker
- Validated input handling, boundary conditions, and proper HTTP status codes (201 Created, 200 OK, 400 Bad Request, 404 Not Found, 409 Conflict) during test executions
- Coordinated recording of the YouTube demonstration video

### Team Member 2 - Yadav Iserbelas (Queue-Triggered Function & Order Lifecycle)

**Part 2**

- Implemented the background queue-triggered function `ProcessOrderQueue` using `[QueueTrigger("order-processing-queue")]`
- Parses incoming queue JSON messages into an `Order` object, accepting both raw and Base64-encoded payloads
- Wrote `OrderEntity` and tracked order records in the `Orders` Azure Table (`PartitionKey`: OrderDate, `RowKey`: OrderId, initial `Status`: Received)
- Simulated the order lifecycle transitions Received -> Preparing -> Ready -> Collected
- Implemented poison queue handling (`order-processing-queue-poison`) and exception logging
- Added `GET /api/orders/{date}/{orderId}` and `GET /api/orders/{date}` for Postman verification of status updates

**Part 1**

- Configured the `staff-documents` Azure Blob Storage container connection within the Functions project (`BlobServiceClient` with `UseDevelopmentStorage=true`)
- Implemented the HTTP trigger function to **Upload** staff documents (`POST /api/documents/upload` and `POST /api/documents/upload/{fileName}`)
- Implemented the HTTP trigger function to **List** all stored operational files (`GET /api/documents`)
- Implemented the HTTP trigger function to **Download** specific documents back to the client (`GET /api/documents/download/{fileName}`)
- Added proper HTTP status codes (`201` Created, `400` Bad Request, `404` Not Found) and stream-based file transfers
- Coordinated recording of the YouTube demonstration video

### Team Member 3 - Saiyen Subban (Docker Compose, Docker Hub & Documentation)

- Pulled and verified the Azurite storage container (`docker run -p 10000:10000 -p 10001:10001 -p 10002:10002`)
- Wrote the Dockerfile using official Azure Functions .NET 10 isolated runtime base image
- Built and tagged the Docker image as `st10472501/coffeennchill-functions:v1.0`
- Built and pushed the custom Azurite image as `st10472501/coffeennchill-azurite:v1.0`
- Published the Docker Hub repositories as public
- Built comprehensive Postman collection covering all Menu and Document endpoints with `{{baseUrl}}` environment variables and automated test assertions
- Exported Postman collection as JSON in `/Docs` folder
- Wrote root `README.md` with local setup steps, Docker execution commands, and team member contributions
- Coordinated recording of the YouTube demonstration video

---

## Testing with Postman

Import `Docs/CLOUD DEVELOPMENT B CLDV6212-Poe Part 2.postman_collection.json`. The collection ships with these variables pre-filled:

```
{{baseUrl}}   = http://localhost:7071/api
{{orderDate}} = 2026-10-12
{{orderId}}   = ORD-2026-8801
```

| # | Request | Method | Expected |
|---|---------|--------|----------|
| 1 | Place Order - Valid | `POST /api/orders/queue` | `200` |
| 2 | Place Order - Rejected: TotalPrice 0 | `POST /api/orders/queue` | `400` |
| 3 | Place Order - Rejected: Missing OrderId | `POST /api/orders/queue` | `400` |
| 4 | Place Order - Rejected: Empty SelectedItemSKUs | `POST /api/orders/queue` | `400` |
| 5 | Get Order Status from Orders Table | `GET /api/orders/{date}/{orderId}` | `200` / `404` |
| 6 | List Orders by Date from Orders Table | `GET /api/orders/{date}` | `200` |

Every request carries automated test assertions. Run request **1** first so the order exists, then run **5** and **6** a few seconds later to watch the status advance through `Received` -> `Preparing` -> `Ready` -> `Collected`.

---

## YouTube Demo

https://youtu.be/G6AYkKV_1hI 

---

## Changelog

### v2.0 - POE Part 2

| Commit | Change |
|--------|--------|
| `4de3c3c` | Added the `Order` model matching the queue message JSON payload structure (`OrderId`, `CustomerName`, `SelectedItemSKUs`, `TotalPrice`, `OrderTimestamp`) |
| `e63f7b6` | Added the queue producer endpoint `POST /api/orders/queue` with field validation and Base64 encoding |
| `ca45559` | Added the Part 2 Postman collection with order queue sample requests |
| `1911b15` | Added `OrderEntity` for the `Orders` table (`OrderDate`/`OrderId`, status `Received`) - *Team Member 2* |
| `f123781` | Added the `ProcessOrderQueue` trigger parsing orders into the `Orders` table as `Received` - *Team Member 2* |
| `4030bdc` | Simulated the lifecycle Received -> Preparing -> Ready -> Collected - *Team Member 2* |
| `5826bb2` | Added poison queue handling and exception logging - *Team Member 2* |
| `2bb9ba8` | Added order status query endpoints for Postman verification - *Team Member 2* |
| `0432f34` | Resolved the merge conflict in `DeleteMenuItem`, keeping the ETag-safe delete - *Team Member 2* |
| `6e8ee2a` | Untracked the `.vs` IDE cache and ignored it at the repository root - *Team Member 2* |
| `3383db2` | Renamed the function app folder from `POE Part 1` to `POE Part 2`, updating Dockerfile and solution paths - *Team Member 2* |
| `2bf342f` | Ignored function host log files (`*.log`) |

### v1.0 - Formative 1 Part 1

- Azure Table Storage Menu CRUD API and Azure Blob Storage document API
- Docker containerisation and Docker Hub images
- Initial README, Postman collection and YouTube demonstration link

---

## Technology Stack

- .NET 10, Azure Functions v4 (Isolated Worker)
- Azure Table Storage (via `Azure.Data.Tables`)
- Azure Blob Storage (via `Azure.Storage.Blobs`)
- Azurite storage emulator
- Docker

---

## References

- Docker. (n.d.). What is Docker? Retrieved from Docker Documentation: https://docs.docker.com/get-started/docker-overview/

- Microsoft. (n.d.). Azure Functions HTTP trigger. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger?tabs=python-v2%2Cisolated-process%2Cnodejs-v4%2Cfunctionsv2&pivots=programming-language-csharp

- Microsoft. (n.d.). Azure Tables client library for .NET. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/overview/azure/data.tables-readme?view=azure-dotnet

- Microsoft. (n.d.). Guide for running C# Azure Functions in the isolated worker model. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=ihostapplicationbuilder%2Cconfig%2Cwindows

- Microsoft. (n.d.). ITableEntity Interface. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.itableentity?view=azure-dotnet

- Microsoft. (n.d.). Quickstart: Azure Blob Storage client library for .NET. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-dotnet?tabs=visual-studio%2Cmanaged-identity%2Croles-azure-portal%2Csign-in-azure-cli%2Cidentity-visual-studio&pivots=blob-storage-quickstart-scratch

- Microsoft. (n.d.). TableClient.DeleteEntityAsync Method. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.tableclient.deleteentityasync?view=azure-dotnet

- Microsoft. (n.d.). TableClient.QueryAsync Method. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.tableclient.queryasync?view=azure-dotnet

- Microsoft. (n.d.). Use dependency injection in .NET Azure Functions. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/azure-functions/functions-dotnet-dependency-injection

- Microsoft. (n.d.). Use the Azurite emulator for local Azure Storage development. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite

- Postman. (n.d.). Work with API response data and cookies in Postman. Retrieved from Postman Documentation: https://learning.postman.com/docs/use/send-requests/response-data/response-data/


---

*Last Updated: October 2026*
