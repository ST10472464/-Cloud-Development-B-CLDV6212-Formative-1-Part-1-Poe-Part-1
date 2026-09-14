# CoffeeNChill - Cloud Development B (CLDV6212) Part 1

## Formative Assessment 1 - Part 1

---

### Team Members

| Name | Role |
|------|------|
| Keyur Keshav (ST10472464) | Team Member 1 |
| Saiyen Subban (ST10466873) | Team Member 2 |
| Yadav Iserbelas (ST10472501) | Team Member 3 |

---

## Project Overview

CoffeeNChill is a cloud-enabled canteen management system for a campus environment. This repository contains **Part 1** of the POE: Azure Functions, Azure Table Storage and Azure Blob Storage integration running locally against the **Azurite** storage emulator inside isolated Docker containers.

This repository contains the **Team Member 1** deliverable (Azure Table Storage setup and the Menu Items CRUD API) and the **Team Member 2** deliverable (Azure Blob Storage integration and the staff document management API).

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
cd "Cloud Development B CLDV 6212- POE Part 1"
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

### Team Member 1 - Keyur Keshav (Azure Table Storage & Menu CRUD)

- Set up the C# Azure Functions isolated project structure
- Configured `local.settings.json` to connect to Azurite (`UseDevelopmentStorage=true`)
- Created the `MenuItem` entity model using `PartitionKey` (Category) and `RowKey` (SKU/ID)
- Implemented all HTTP-triggered Menu functions (Create, Get All, Get By Category, Update, Delete)
- Added input validation and proper HTTP status codes (`400` Bad Request, `404` Not Found, `409` Conflict)

### Team Member 2 - Yadav Iserbelas (Azure Blob Storage & Document Functions)

- Configured the `staff-documents` Azure Blob Storage container connection within the Functions project (`BlobServiceClient` with `UseDevelopmentStorage=true`)
- Implemented the HTTP trigger function to **Upload** staff documents (`POST /api/documents/upload` and `POST /api/documents/upload/{fileName}`)
- Implemented the HTTP trigger function to **List** all stored operational files (`GET /api/documents`)
- Implemented the HTTP trigger function to **Download** specific documents back to the client (`GET /api/documents/download/{fileName}`)
- Added proper HTTP status codes (`201` Created, `400` Bad Request, `404` Not Found) and stream-based file transfers

### Team Member 3 - Saiyen Subban (Docker, Postman & Documentation)

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

Import the collection in `/docs` and set the collection variable:

```
{{baseUrl}} = http://localhost:7071/api
```

---

## YouTube Demo

[Video Demo Link](#)

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

*Last Updated: September 2026*
