# CoffeeNChill - Cloud Development B (CLDV6212) Part 1

## Formative Assessment 1 - Part 1

---

### Team Members

| Name | Role |
|------|------|
| Keyur Keshav | Team Member 1 |
| Yadav Iserbelas (ST10472501) | Team Member 2 |
| Saiyen Subban | Team Member 3 |

---

## Project Overview

CoffeeNChill is a cloud-enabled canteen management system for a campus environment. This repository contains **Part 1** of the POE: Azure Functions, Azure Table Storage and Azure File Share integration running locally against the **Azurite** storage emulator inside isolated Docker containers.

This is the **Team Member 1** deliverable: the Azure Table Storage setup and the Menu Items CRUD API.

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

- `10000` - Table storage
- `10001` - Blob storage
- `10002` - Queue storage

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

> `UseDevelopmentStorage=true` automatically targets the local Azurite emulator on ports 10000-10002.

### 4. Run the Functions locally

```bash
cd "Cloud Development B CLDV 6212- POE Part 1"
func start
```

---

## Docker Execution (Standalone, no Compose)

### Build the Functions image

```bash
docker build -t st10472501/coffeennchill-functions:v1.0 .
```

### Run the Functions container (must be on Azurite's network)

```bash
docker run --network host -p 7071:80 st10472501/coffeennchill-functions:v1.0
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

- Configure the `staff-docs` Azure Blob Storage container
- Upload, List and Download document HTTP functions

### Team Member 3 - Saiyen Subban (Docker, Postman & Documentation)

- Azurite container verification & Dockerfile
- Postman collection with `{{baseUrl}}` variables
- Root `README.md` and YouTube demonstration video

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
- Azurite storage emulator
- Docker

---

## References

- [Azure Functions .NET isolated worker](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [Azure Tables client library](https://learn.microsoft.com/en-us/azure/storage/tables/table-storage-how-to-use-dotnet)
- [ITableEntity interface](https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.itableentity)
- [Azurite emulator](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite)

---

*Last Updated: September 2026*