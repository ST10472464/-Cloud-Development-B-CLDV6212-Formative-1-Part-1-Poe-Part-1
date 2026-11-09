# CoffeeNChill - Cloud Development B (CLDV6212) POE Part 1, Part 2 & Part 3

## Formative Assessment - Part 1, Part 2 & Part 3

---

### Team Members

| Name | Role | Part 2 Focus | Part 3 Focus |
|------|------|--------------|--------------|
| Keyur Keshav (ST10472464) | Team Member 1 | Queue producer (`POST /api/orders/queue`) | Authentication engine, `Users` Azure Table, JWT issuance, API gateway (Swagger, DTOs, global error handling), business scenario & access-tier documentation |
| Yadav Iserbelas (ST10472501) | Team Member 2 | Queue trigger (`ProcessOrderQueue`) + Orders table | Live VPS production deployment & container orchestration |
| Saiyen Subban (ST10466873) | Team Member 3 | Docker Compose orchestration, Docker Hub, Postman, docs | Automated CI/CD pipeline, gateway routing & role-based authorization policies |

---

## Project Overview

CoffeeNChill is a cloud-enabled canteen management system for a campus environment. This repository contains **Parts 1, 2 and 3** of the POE:

- **Part 1** - Azure Table Storage Menu CRUD API and Azure Blob Storage document API (the foundation).
- **Part 2** - asynchronous order queuing via Azure Storage Queues, a queue-triggered Azure Function that tracks the order lifecycle in Azure Table Storage, and multi-container orchestration with Docker Compose pulling images from Docker Hub - all running locally against the **Azurite** storage emulator.
- **Part 3** - a secure ASP.NET Core **API Gateway** fronting the whole system with Swagger/OpenAPI, JWT Bearer authentication, BCrypt password hashing and role-based authorization against a `Users` Azure Table.

During break times hundreds of students order simultaneously and synchronous processing crashes the system. Part 2 fixes this: order submission never blocks the client — the order is written to an Azure Storage Queue and a background Azure Function processes it, logging it into an Orders Azure Table and advancing its preparation status `Received → Preparing → Ready → Collected`.

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

## Part 2 - Queue Triggers & Docker Compose Orchestration

### Part 2 Deliverables

- Azure Storage Queue `order-processing-queue` (emulated by Azurite)
- Queue producer HTTP endpoint `POST /api/orders/queue` (Base64-encoded JSON message)
- Queue-triggered function `ProcessOrderQueue` with `[QueueTrigger("order-processing-queue")]`
- `Orders` Azure Table (`PartitionKey` = OrderDate, `RowKey` = OrderId, `Status` lifecycle)
- Poison queue handling (`order-processing-queue-poison`) for failed messages
- Single-command local orchestration with `docker compose up` (Azurite + Functions host)
- Updated Postman collection covering the full async workflow

### Part 2 Changelog

| Member | Contribution |
|--------|--------------|
| Keyur Keshav (TM1) | `POST /api/orders/queue` producer: validates the order payload, serializes it to JSON, Base64-encodes it and pushes it onto `order-processing-queue` without blocking the client. Added the `Order` model. |
| Yadav Iserbelas (TM2) | `ProcessOrderQueue` `[QueueTrigger("order-processing-queue")]`: parses the JSON message, writes the row into the `Orders` table as `Received`, then simulates `Received → Preparing → Ready → Collected`. Poison-queue forwarding on failure. `GET /api/orders/{date}` and `GET /api/orders/{date}/{orderId}` for Postman verification. |
| Saiyen Subban (TM3) | Root `docker-compose.yml` orchestrating Azurite + the Functions host from Docker Hub image `st10472501/coffeenchill-functions:v2.0` on a custom bridge network with a persistent Azurite volume. Authored the compose services; the `v2.0` image is published on Docker Hub under `st10472501`. Expanded the Part 2 Postman collection (renamed requests, status-poll GETs, negative test). Fixed the team table and wrote this Part 2 documentation. |

### Part 2 Order Endpoints

| Method | Route | Description | Status Codes |
| ------ | ----- | ----------- | ------------ |
| POST | `/api/orders/queue` | Queue an order (async, non-blocking) | `200`, `400` |
| GET | `/api/orders/{date}` | List all orders for a date (`yyyy-MM-dd`) | `200` |
| GET | `/api/orders/{date}/{orderId}` | Poll one order's status | `200`, `404` |

### Example Request - Queue an Order

```json
{
  "OrderId": "ORD-2026-8801",
  "CustomerName": "Jane Smith",
  "SelectedItemSKUs": ["COF-001", "PAS-104"],
  "TotalPrice": 65.00,
  "OrderTimestamp": "2026-10-12T10:15:00Z"
}
```

The producer serializes this object to a JSON string, Base64-encodes it and sends it to `order-processing-queue`. The response returns immediately:

```json
{
  "message": "Order queued successfully",
  "orderId": "ORD-2026-8801"
}
```

Validation rejects malformed payloads with `400 Bad Request` (missing `OrderId`/`CustomerName`, empty SKU list, or `TotalPrice <= 0`).

### Orders Table Schema

| Property | Type | Notes |
| ------------- | --------- | ------------------------------------------------- |
| `PartitionKey` | string | OrderDate, e.g. `2026-10-12` |
| `RowKey` | string | OrderId, e.g. `ORD-2026-8801` |
| `CustomerName` | string | Placing customer |
| `SelectedItemSKUs` | string | Comma-separated SKUs, e.g. `COF-001,PAS-104` |
| `TotalPrice` | double | Order total |
| `Status` | string | `Received` → `Preparing` → `Ready` → `Collected` |
| `OrderTimestamp` | datetime | Original order time |
| `ETag` | ETag | Concurrency token (Azure Tables) |
| `Timestamp` | datetime | Server-managed |

### Status Lifecycle

`ProcessOrderQueue` inserts the row as `Received`, then advances it on a timer: `Preparing` after 2s, `Ready` after 3s more, `Collected` after 2s more. Poll `GET /api/orders/{date}/{orderId}` every couple of seconds to watch the march. Any failed message is forwarded to `order-processing-queue-poison`.

### Single-Command Setup (Docker Compose)

The compose file lives at the repository root and pulls the application image **directly from Docker Hub** — no local build is required.

| Service | Image | Ports |
|---|---|---|
| `azurite` | `mcr.microsoft.com/azure-storage/azurite:3.34.0` | `10000` (Blob), `10001` (Queue), `10002` (Table) |
| `functions` | `st10472501/coffeenchill-functions:v2.0` | `7071` (HTTP) |

```bash
git clone https://github.com/ST10472464/-Cloud-Development-B-CLDV6212-Formative-1-Part-1-Poe-Part-1.git
cd "-Cloud-Development-B-CLDV6212-Formative-1-Part-1-Poe-Part-1"
docker compose up
```

That single command pulls both images and starts the whole stack. `docker compose down` stops it; `docker compose down -v` also wipes the Azurite data volume for a clean re-run.

Compose details worth noting:

- The Functions host sets `AzureWebJobsStorage` to the Azurite **service name** (`http://azurite:10000/...`), not `127.0.0.1`, so the queue trigger can reach storage across the compose network — otherwise the trigger dies inside the container.
- `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated` matches the isolated-worker runtime of the app.
- Both services sit on a custom bridge network (`coffeenchill-network`); the Functions host waits for Azurite's healthcheck before starting.
- Azurite data is persisted in the named volume `azurite-data`.

### Docker Hub Images

| Image | Tag | Link |
|---|---|---|
| `st10472501/coffeenchill-functions` | `v2.0` | https://hub.docker.com/r/st10472501/coffeenchill-functions |

### Rebuilding and Pushing the Image (maintainers only)

```bash
docker build -t st10472501/coffeenchill-functions:v2.0 .
docker push st10472501/coffeenchill-functions:v2.0
```

---

## Part 3 - Secure API Gateway & JWT Authentication

### Business Scenario Context (Section 1)

CoffeeNChill serves a campus canteen during peak break times. Parts 1 and 2 proved the storage
layer works: menu items live in Azure Table Storage, staff documents in Azure Blob Storage, and
orders flow through an Azure Storage Queue into an `Orders` table. What Parts 1 and 2 did **not**
do is protect any of it. Every endpoint was reachable by anyone who could reach the port, and the
function host had no notion of *who* was calling.

That is the gap Part 3 closes, and it is a real operational problem rather than a checkbox:

- **Students must self-serve.** Hundreds of students browse the menu and queue orders during a
  break. That traffic is legitimate and has to stay frictionless - forcing a login to read a menu
  would stall the queue at the counter.
- **Staff must not be students.** A student must not be able to change a price, mark their own
  order `Collected`, or read the canteen's operational documents. Those actions move money and
  stock, so they need an authenticated, auditable identity.
- **Secrets must not be guessable.** A canteen system holds staff documents and pricing rules.
  Credentials therefore cannot travel in clear text, and cannot be recoverable from the database
  if it is ever dumped.

The design answer is a single **unified gateway**: one public entry point that fronts the whole
system, decides per-route whether a caller may proceed, and issues signed JWTs so that decision is
made from a token rather than from trust. The gateway is the only component that needs to be
public; everything behind it (the Functions host, tables, blobs and queue) can stay internal.

#### Business Roles

| Role | Who they are | What they need to do |
|------|--------------|----------------------|
| **Customer** | A student placing an order | Browse the menu, queue an order, check their order status |
| **Staff** | A barista or canteen attendant | Everything a customer can do, plus maintain the menu, manage staff documents and advance orders through their lifecycle |
| **Admin** | The canteen manager | Everything staff can do, plus manage accounts and configuration |

### Access Tiers (Section 1)

Authorization is deliberately **not** uniform. The brief specifies a strict split between traffic
that must stay open and traffic that must be gated, and the gateway implements exactly that split:

| Tier | Who | Endpoints | Mechanism |
|------|-----|-----------|-----------|
| **Public** | Anonymous students, no token | `GET /api/menu`, `GET /api/menu/category/{category}`, `GET /api/orders/{date}`, `GET /api/orders/{date}/{orderId}`, `POST /api/orders/queue`, `POST /api/auth/register`, `POST /api/auth/login` | `[AllowAnonymous]` |
| **Authenticated** | Any valid token holder | `GET /api/auth/me` | `[Authorize]` |
| **Admin / Staff** | Valid token whose `ClaimTypes.Role` is `Admin` or `Staff` | `POST`, `PUT`, `DELETE /api/menu`, `GET`, `POST`, `DELETE /api/documents`, `GET /api/documents/download/{fileName}`, `PUT /api/orders/status` | `[Authorize(Roles = "Admin,Staff")]` |

The two rules that follow from the table:

1. **Reads that a student needs are open; writes that affect the business are not.** Ordering
   stays frictionless because `POST /api/orders/queue` is anonymous, but changing a price or an
   order status requires a staff token.
2. **The role travels inside the token, not in the request.** A caller cannot claim to be staff by
   sending a header - the gateway re-validates the signature on every request and reads the role
   from the signed claims, so the only way to hold the `Staff` role is to have logged in with a
   staff account.

Swagger UI renders this split visually: the 9 public operations carry no lock, and the 9
protected operations show a padlock and require an `Authorization: Bearer <token>` value.

---

### Part 3 - Technical Specifications & Endpoints (Section 2)

#### Architecture

```
Client (Postman / Swagger UI / mobile app)
        |
        |  HTTPS + optional "Authorization: Bearer <jwt>"
        v
+----------------------------------------------+
|  CoffeeNChill.ApiGateway  (ASP.NET Core)     |   <-- single public entry point
|  - Swagger / OpenAPI (Swashbuckle, XML docs) |
|  - JWT Bearer validation                     |
|  - Global exception middleware (problem+json)|
|  - DTOs + explicit model mapping             |
+----------------------------------------------+
        |                 |                 |                |
        v                 v                 v                v
   Users Table      MenuItems Table     Orders Table    staff-documents
   (auth)           Menu CRUD           status          Blob container
        |
        +---- order-processing-queue  (Azure Storage Queue)
        |
        +---- /api/functions/{**path}  passthrough to the Azure Functions host
```

The gateway talks to Azure Storage **directly** through the SDKs for the `Users`, `MenuItems` and
`Orders` tables, the `staff-documents` blob container and the `order-processing-queue` queue, and
additionally exposes `/api/functions/{**path}` as a passthrough to the Azure Functions host so the
Part 1 / Part 2 function endpoints remain reachable through the same public origin.

#### New Projects and Files

| Path | Purpose |
|------|---------|
| `CoffeeNChill.ApiGateway/` | ASP.NET Core Web API gateway project (`net10.0`) |
| `Program.cs` | Swagger, JWT Bearer validation, DI, CORS, middleware pipeline |
| `Controllers/AuthController.cs` | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` |
| `Controllers/MenuController.cs` | Menu CRUD with role-based authorization |
| `Controllers/OrderController.cs` | Order queueing, reading and status transitions |
| `Controllers/DocumentController.cs` | Staff document upload / list / download / delete |
| `Controllers/FunctionsProxyController.cs` | Passthrough to the Azure Functions host |
| `Services/PasswordHasher.cs` | BCrypt password hashing (cost factor 12) |
| `Services/TokenService.cs` | Signed JWT issuance |
| `Services/UserService.cs` | Registration and login against the `Users` table |
| `Middleware/ExceptionHandlingMiddleware.cs` | Global exception-to-problem+json mapping |
| `Swagger/AuthorizeCheckOperationFilter.cs` | Marks only protected operations as requiring Bearer |
| `Models/UserProfile.cs` | `Users` Azure Table entity |

#### Authentication Endpoints

| Method | Route | Auth | Description | Status Codes |
| ------ | ----- | ---- | ----------- | ------------ |
| POST | `/api/auth/register` | `[AllowAnonymous]` | Hash the password with BCrypt and create a row in the `Users` table | `201`, `400`, `409` |
| POST | `/api/auth/login` | `[AllowAnonymous]` | Validate credentials and issue a signed JWT Bearer token | `200`, `400`, `401` |
| GET | `/api/auth/me` | `[Authorize]` | Echo the identity carried by the supplied token | `200`, `401` |

##### Example - Register

`POST http://localhost:5080/api/auth/register`

```json
{
  "fullName": "Keyur Keshav",
  "email": "keyur@coffeenchill.ac.za",
  "password": "Str0ngPass!",
  "role": "Admin"
}
```

Returns `201 Created` with the created profile - **never** the password hash:

```json
{
  "email": "keyur@coffeenchill.ac.za",
  "fullName": "Keyur Keshav",
  "role": "Admin",
  "createdDate": "2026-11-09T20:43:11.9676375Z"
}
```

Registering an address that already exists returns `409 Conflict`. `role` must be one of
`Admin`, `Staff` or `Customer`; `password` must be at least 8 characters.

##### Example - Login

`POST http://localhost:5080/api/auth/login`

```json
{ "email": "keyur@coffeenchill.ac.za", "password": "Str0ngPass!" }
```

Returns `200 OK`:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-11-09T21:49:20Z",
  "email": "keyur@coffeenchill.ac.za",
  "fullName": "Keyur Keshav",
  "role": "Admin"
}
```

An unknown email and a wrong password both return `401 Unauthorized` with the **same** message
(`"Invalid email or password."`), so the endpoint cannot be used to discover which addresses are
registered.

#### JWT Claims

The token is signed with HMAC-SHA256 using `Jwt:SecretKey` and carries the three claims the brief
specifies, written as their full `ClaimTypes` URIs so `[Authorize(Roles = ...)]` resolves against
them without any claim-type remapping:

| Claim | Value for the example above |
| ----- | --------------------------- |
| `ClaimTypes.Name` | `Keyur Keshav` |
| `ClaimTypes.Email` | `keyur@coffeenchill.ac.za` |
| `ClaimTypes.Role` | `Admin` |

Plus `ClaimTypes.NameIdentifier`, `jti`, `iat`, `iss` (`CoffeeNChill`), `aud` (`CoffeeNChill
Clients`) and `exp`. Token lifetime is `Jwt:ExpiryMinutes` (60 by default).

Decoded payload (abbreviated):

```json
{
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": "Yadav Iserbelas",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress": "yadav@coffeenchill.ac.za",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Staff",
  "iss": "CoffeeNChill",
  "aud": "CoffeeNChill Clients",
  "exp": 1794308960
}
```

#### Users Table Schema

`PartitionKey` is the **role** and `RowKey` is the **email**, exactly as the brief specifies, so a
whole access tier can be scanned in one partition query while an email lookup remains unique
across the table.

| Property | Type | Notes |
| -------- | ---- | ----- |
| `PartitionKey` | string | Role: `Admin`, `Staff` or `Customer` |
| `RowKey` | string | User email / username, unique across all partitions, normalised to lower case |
| `PasswordHash` | string | Salted BCrypt hash (cost 12). **Never returned by any endpoint** |
| `FullName` | string | Display name, surfaced as `ClaimTypes.Name` |
| `CreatedDate` | datetime | UTC account creation timestamp |
| `ETag` | ETag | Concurrency token (Azure Tables) |
| `Timestamp` | datetime | Server-managed |

#### Password Storage

Passwords are hashed with **BCrypt** (`BCrypt.Net-Next`, work factor 12) rather than stored or
plain-hashed. BCrypt is deliberately slow and memory-hard, embeds a fresh random salt in every
hash, and records its own cost factor, which makes offline cracking of a stolen `Users` table
impractical compared with a fast hash. `PasswordHash` is excluded from every DTO by the explicit
mappers in `Mapping/Mappers.cs`, so a hash cannot leak through a serialisation accident.

#### Role-Based Authorization Policies

| Policy | Attribute | Applied to |
| ------ | --------- | ---------- |
| Public | `[AllowAnonymous]` | Menu reads, order queueing, order reads, register, login |
| Any authenticated user | `[Authorize]` | `GET /api/auth/me` |
| Staff or Admin | `[Authorize(Roles = "Admin,Staff")]` | Menu writes, document reads/writes, order status |

#### API Gateway Endpoints (complete)

| Method | Route | Auth | Description | Status Codes |
| ------ | ----- | ---- | ----------- | ------------ |
| POST | `/api/auth/register` | Public | Create an account | `201`, `400`, `409` |
| POST | `/api/auth/login` | Public | Issue a JWT | `200`, `400`, `401` |
| GET | `/api/auth/me` | Authenticated | Echo token identity | `200`, `401` |
| GET | `/api/menu` | Public | List all menu items | `200` |
| GET | `/api/menu/category/{category}` | Public | List a category | `200`, `400` |
| POST | `/api/menu` | Admin, Staff | Create a menu item | `201`, `400`, `401`, `403`, `409` |
| PUT | `/api/menu/{category}/{sku}` | Admin, Staff | Update a menu item | `200`, `400`, `401`, `403`, `404`, `409` |
| DELETE | `/api/menu/{category}/{sku}` | Admin, Staff | Delete a menu item | `204`, `401`, `403`, `404` |
| POST | `/api/orders/queue` | Public | Validate and enqueue an order | `200`, `400` |
| GET | `/api/orders/{date}` | Public | List orders for a date | `200` |
| GET | `/api/orders/{date}/{orderId}` | Public | Poll one order | `200`, `404` |
| PUT | `/api/orders/status` | Admin, Staff | Advance order status | `200`, `400`, `401`, `403`, `404`, `409` |
| GET | `/api/documents` | Admin, Staff | List staff documents | `200`, `401`, `403` |
| POST | `/api/documents` | Admin, Staff | Upload a staff document (`multipart/form-data`) | `201`, `400`, `401`, `403` |
| GET | `/api/documents/download/{fileName}` | Admin, Staff | Download a document | `200`, `401`, `403`, `404` |
| DELETE | `/api/documents/{fileName}` | Admin, Staff | Delete a document | `204`, `401`, `403`, `404` |
| GET | `/api/functions/{**path}` | Passthrough | Forward to the Azure Functions host | upstream, `503` |
| POST | `/api/functions/{**path}` | Passthrough | Forward to the Azure Functions host | upstream, `503` |

#### Swagger / OpenAPI

- Swashbuckle UI served at `/swagger` in **every** environment, because the brief requires a
  publicly reachable Swagger URL on the deployed instance for the walkthrough.
- **XML documentation is generated and injected**, so all 18 operations carry a `<summary>`,
  parameter documentation, and `<response>` codes that become the documented status responses.
- 15 component schemas are emitted for the DTOs, each with property-level descriptions.
- A `Bearer` HTTP security scheme (`bearerFormat: JWT`) is registered. The document-level
  requirement is `[{"Bearer":[]}]`, and `AuthorizeCheckOperationFilter` writes an explicit empty
  `security: []` on each `[AllowAnonymous]` operation so it overrides the global requirement.
  Result: 9 public operations render without a lock, 9 protected operations render with one.
- `EnablePersistAuthorization()` and `EnableTryItOutByDefault()` are enabled so a reviewer can
  paste a token once and immediately exercise protected endpoints.

#### DTO Pattern & Model Mapping

Every endpoint accepts and returns a DTO rather than a storage entity. `Mapping/Mappers.cs`
holds the conversions in one place, which is what guarantees internal fields - `PasswordHash`,
`ETag`, `Timestamp` - can never be serialised by accident.

| DTO | Used by |
| --- | ------- |
| `RegisterRequest`, `LoginRequest`, `AuthResponse`, `UserProfileDto` | `/api/auth/*` |
| `MenuItemDto`, `CreateMenuItemRequest`, `UpdateMenuItemRequest` | `/api/menu` |
| `QueueOrderRequest`, `QueueOrderResponse`, `OrderDto`, `UpdateOrderStatusRequest` | `/api/orders` |
| `DocumentDto`, `UploadDocumentResponse` | `/api/documents` |

Requests are additionally validated with `System.ComponentModel.DataAnnotations` attributes
(`[Required]`, `[EmailAddress]`, `[MinLength]`, `[RegularExpression]`, `[Range]`), so malformed
input fails before it reaches the service layer.

#### Global Error Handling

`Middleware/ExceptionHandlingMiddleware.cs` is registered as the outermost middleware and maps
every escaping exception onto a single RFC 7807 `application/problem+json` contract, so clients
never receive a raw stack trace:

| Exception | Status | Title |
| --------- | ------ | ----- |
| `AppValidationException` | `400` | Validation failed |
| `ResourceNotFoundException` | `404` | Resource not found |
| `ConflictException` | `409` | Resource already exists |
| `UnauthorizedException` | `401` | Authentication failed |
| `BadHttpRequestException` | `400` | Malformed request |
| `RequestFailedException` (Azure Storage) | upstream status | Azure Storage request failed |
| `InvalidOperationException` | `503` | Dependency unavailable |
| anything else | `500` | An unexpected error occurred (message withheld) |

Every body carries `status`, `title`, `detail`, `instance` and `traceId`. `JwtBearerEvents`
returns the same shape for `401` and `403`, so authentication failures are indistinguishable in
form from every other error. Server faults are logged in full but their messages are never echoed
to the caller.

---

## Part 1 - Menu CRUD & Staff Documents

### Part 1 Deliverables

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

### Menu API Endpoints

| Method | Route                             | Description                                 | Status Codes        |
| ------ | --------------------------------- | ------------------------------------------- | ------------------- |
| POST   | `/api/menu`                       | Create a new menu item                      | `201`, `400`, `409` |
| GET    | `/api/menu`                       | Get all menu items                          | `200`               |
| GET    | `/api/menu/category/{category}`   | Get menu items filtered by category         | `200`               |
| PUT    | `/api/menu/{category}/{id}`       | Update a menu item (price / availability)   | `200`, `400`, `404` |
| DELETE | `/api/menu/{category}/{id}`       | Delete a menu item                          | `200`, `404`        |

#### Example Request - Create Menu Item

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

#### Example Request - Upload a Staff Document

Send a `POST` request to `/api/documents/upload` with a `multipart/form-data` file body.

```bash
curl -X POST http://localhost:7071/api/documents/upload \
  -F "file=@barista-recipe-sheet.pdf"
```

Alternatively, upload with a custom file name via `POST /api/documents/upload/{fileName}` sending the file bytes (Base64-encoded) in the request body.

### Document API Endpoints

| Method | Route                                 | Description                                        | Status Codes        |
| ------ | ------------------------------------- | -------------------------------------------------- | ------------------- |
| POST   | `/api/documents/upload`               | Upload a staff document (file body)                | `201`, `400`        |
| POST   | `/api/documents/upload/{fileName}`    | Upload a document with a specific file name        | `201`, `400`        |
| GET    | `/api/documents`                      | List all stored documents                          | `200`               |
| GET    | `/api/documents/download/{fileName}`  | Download a stored document                         | `200`, `404`        |

### staff-documents Blob Storage Schema

| Property    | Type   | Notes                                      |
| ----------- | ------ | ------------------------------------------ |
| Blob name   | string | e.g. `barista-recipe-sheet.pdf`            |
| Container   | string | Logical container `staff-documents`        |
| Content     | bytes  | Raw file content streamed to/from Azurite  |
| Size        | long   | Byte size of the file (reported on upload) |
| Last Modified | datetime | Timestamp of last modification          |
| Content Type  | string | MIME type of the stored blob             |

## YouTube Link Part 2
https://youtu.be/Gqj6Svw36YA 

### MenuItems Table Schema

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
- Docker Desktop (bundles the Compose plugin)
- Azurite (via Docker, or run through compose)

### 2. Recommended: run everything with Docker Compose

```bash
docker compose up
```

This starts Azurite **and** the Functions host together. Skip steps 3-4 below and jump to "Testing with Postman".

### 3. Alternative: run Azurite alone

```bash
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite mcr.microsoft.com/azure-storage/azurite
```

Ports:

- `10000` - Blob storage
- `10001` - Queue storage
- `10002` - Table storage

### 4. local.settings.json

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

### 5. Run the Functions locally (outside compose)

```bash
cd "Cloud Development B CLDV 6212- POE Part 2"
func start
```

### 6. Run the Part 3 API Gateway

The gateway is a separate ASP.NET Core project at the repository root. It reads the same Azurite
connection string as the Functions project, so start Azurite first (steps 2 or 3 above).

```bash
cd CoffeeNChill.ApiGateway
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

It listens on `http://localhost:5080` by default. Override with `ASPNETCORE_URLS`:

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 dotnet run
```

| URL | Purpose |
|-----|---------|
| `http://localhost:5080/swagger` | Swagger UI |
| `http://localhost:5080/swagger/v1/swagger.json` | OpenAPI document |
| `http://localhost:5080/api/auth/register` | Create an account |
| `http://localhost:5080/api/auth/login` | Obtain a JWT |

> `ASPNETCORE_ENVIRONMENT=Development` is required for local runs. Outside `Development` the
> gateway refuses to start if `Jwt:SecretKey` is still the committed development key - set
> `Jwt__SecretKey` from the environment before deploying.

> If Azurite is started outside Docker, run it with `--skipApiVersionCheck`; otherwise the current
> Azure SDK clients are rejected with `InvalidHeaderValue`. The `docker-compose.yml` already
> passes this flag.

---

## Part 3 Changelog

| Member | Contribution |
|--------|--------------|
| Keyur Keshav (TM1) | New `CoffeeNChill.ApiGateway` project: `Users` Azure Table entity, BCrypt password hashing, `POST /api/auth/register`, `POST /api/auth/login` issuing a signed JWT with `ClaimTypes.Name`/`Email`/`Role`, `GET /api/auth/me`, Swashbuckle Swagger with XML docs and Bearer scheme, DTO + mapper layer, global problem+json exception middleware, role-based authorization across all endpoints, and the business scenario / access-tier documentation above. |
| Yadav Iserbelas (TM2) | _Live VPS production deployment and container orchestration - to be documented here._ |
| Saiyen Subban (TM3) | _Automated CI/CD pipeline - to be documented here._ |

---

## Team Member Contributions

### Team Member 1 - Keyur Keshav (Authentication Engine, Users Table, API Gateway & Security Policies)

**Part 3**

- Created the `CoffeeNChill.ApiGateway` ASP.NET Core Web API project (`net10.0`) as the single public entry point in front of the Functions host and Azure Storage
- Designed and implemented the `Users` Azure Table entity: `PartitionKey` = Role (`Admin` / `Staff` / `Customer`), `RowKey` = User Email/Username, with `FullName`, `CreatedDate` and `PasswordHash`
- Implemented `BCryptPasswordHasher` (BCrypt.Net-Next, work factor 12) so every stored password is a self-salted, deliberately slow hash; `PasswordHash` is excluded from all DTOs by the mappers in `Mapping/Mappers.cs`
- Built `POST /api/auth/register`: validates the payload, normalises the email, rejects duplicates with `409`, hashes the password and writes the row - returns `201` with the profile and never the hash
- Built `POST /api/auth/login`: performs a cross-partition lookup on `RowKey`, verifies the BCrypt hash and issues a signed HMAC-SHA256 JWT carrying `ClaimTypes.Name`, `ClaimTypes.Email` and `ClaimTypes.Role`, plus `iss` / `aud` / `exp`
- Returned an identical `401` for an unknown email and a wrong password so the endpoint cannot be used to enumerate registered accounts
- Built `GET /api/auth/me` to echo the identity back from the token claims, confirming the three required claims round-trip through `[Authorize]`
- Configured Swashbuckle with generated XML documentation, a `Bearer` security scheme and response schemas for all 18 operations, plus `AuthorizeCheckOperationFilter` so only the 9 protected operations render a lock in Swagger UI
- Implemented the DTO layer (`AuthDtos`, `MenuDtos`, `OrderDtos`, `DocumentDtos`) and explicit `Mappers` so storage entities - including `PasswordHash` and `ETag` - are never serialised directly
- Implemented `Middleware/ExceptionHandlingMiddleware` mapping every exception to one RFC 7807 `application/problem+json` contract, and matching `401`/`403` bodies via `JwtBearerEvents`
- Applied the brief's role split across all endpoints: `[AllowAnonymous]` for menu reads, order queueing and auth; `[Authorize(Roles = "Admin,Staff")]` for menu writes, documents and order status
- Documented the business scenario context and the public / authenticated / Admin-Staff access tiers in this README, together with the `Users` schema, JWT claims and the complete gateway endpoint table

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

### Team Member 2 - Yadav Iserbelas (Blob Documents, Queue Trigger & Order Lifecycle)

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

### Team Member 3 - Saiyen Subban (Docker Compose, Docker Hub, Postman & Documentation)

**Part 1**

- Wrote the Dockerfile using the official Azure Functions .NET 10 isolated runtime base image
- Built and pushed the Azurite and Functions images to Docker Hub
- Built the Postman collection covering all Menu and Document endpoints with automated test assertions
- Wrote the root `README.md` with local setup steps and contribution breakdown

**Part 2**

- Wrote the root `docker-compose.yml`: Azurite (ports 10000-10002, `azurite-data` volume, healthcheck) plus the Functions host from Docker Hub image `st10472501/coffeenchill-functions:v2.0`
- Pointed `AzureWebJobsStorage` at the Azurite **service name** so the queue trigger resolves storage over the compose bridge network (never `127.0.0.1`)
- Authored the compose services (Azurite ports/healthcheck/volume, Functions host, bridge network); the `v2.0` image is built, tagged and published on Docker Hub under `st10472501`
- Expanded the Part 2 Postman collection: renamed the requests, kept the Jane Smith sample body, added both GET endpoints and the `TotalPrice: 0 → 400` negative test
- Fixed the team roles table and documented the Part 2 changelog, compose workflow and Docker Hub images

---

## Testing with Postman

The Postman collections live in `Docs/` as Postman v3 YAML folders: `CLOUD DEVELOPMENT B CLDV6212 - POE Part 2 (Order Queue)` (Part 2 async order workflow), `CoffeeNChill API - Part 1 Complete Collection` (Part 1 Menu + Document endpoints) and `CLOUD DEVELOPMENT B CLDV6212 - POE Part 3 (API Gateway & JWT Aut)` (Part 3 gateway + JWT auth). Import one into Postman with **Import → Files**. The Part 2 collection ships with these variables pre-filled:

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

Captured request and response screenshots for all 16 Part 3 API gateway requests are stored in `Docs/Postman Collection POE/` as `1postman.png` through `16postman.png`.

---

## YouTube Demo

- Part 1: https://youtu.be/G6AYkKV_1hI
- Part 2 (compose up + queue workflow): _link to be added after the Part 2 walkthrough is recorded_

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
- Azure Storage Queues (via `Azure.Storage.Queues` + Queue trigger extension)
- Azurite storage emulator
- Docker + Docker Compose, Docker Hub

---

## References Part 1

- Docker. (n.d.). What is Docker? Retrieved from Docker Documentation: https://docs.docker.com/get-started/docker-overview/

- Docker. (n.d.). Compose specification. Retrieved from Docker Documentation: https://docs.docker.com/compose/

- Microsoft. (n.d.). Azure Functions HTTP trigger. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger?tabs=python-v2%2Cisolated-process%2Cnodejs-v4%2Cfunctionsv2&pivots=programming-language-csharp

- Microsoft. (n.d.). Azure Functions queue storage trigger and bindings. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue

- Microsoft. (n.d.). Azure Tables client library for .NET. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/overview/azure/data.tables-readme?view=azure-dotnet

- Microsoft. (n.d.). Guide for running C# Azure Functions in the isolated worker model. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=ihostapplicationbuilder%2Cconfig%2Cwindows

- Microsoft. (n.d.). ITableEntity Interface. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.itableentity?view=azure-dotnet

- Microsoft. (n.d.). Quickstart: Azure Blob Storage client library for .NET. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-dotnet?tabs=azure-portal%2Cidentity-based-access%2Cnodejs-v1%2Cpivots=programming-language-csharp

- Microsoft. (n.d.). TableClient.DeleteEntityAsync Method. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.tableclient.deleteentityasync?view=azure-dotnet

- Microsoft. (n.d.). TableClient.QueryAsync Method. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.tableclient.queryasync?view=azure-dotnet

- Microsoft. (n.d.). Use dependency injection in .NET Azure Functions. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/azure-functions/functions-dotnet-dependency-injection

- Microsoft. (n.d.). Use the Azurite emulator for local Azure Storage development. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite

- Microsoft. (n.d.). Run a container with Azure Container Apps or Docker Compose. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/container-apps/quickstart-code-to-cloud

- Postman. (n.d.). Work with API response data and cookies in Postman. Retrieved from Postman Documentation: https://learning.postman.com/docs/use/send-requests/response-data/response-data/

- Postman. (n.d.). Writing tests in Postman. Retrieved from Postman Documentation: https://learning.postman.com/docs/tests-and-scripts/write-scripts/test-scripts/

---

## References Part 2

- Microsoft. (n.d.). Azure Functions error handling and retries. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-error-pages?tabs=fixed-delay%2Cisolated-process%2Cnode-v4%2Cpython-v2&pivots=programming-language-csharp

- Microsoft. (n.d.). Azure Functions HTTP triggers and bindings overview. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook?tabs=isolated-process&pivots=programming-language-csharp

- Microsoft. (n.d.). Azure Queue storage trigger and bindings for Azure Functions overview. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue?tabs=isolated-process%2Cextensionv5&pivots=programming-language-csharp

- Microsoft. (n.d.). Convert.ToBase64String Method (System). Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/system.convert.tobase64string?view=net-10.0

- Microsoft. (n.d.). Guide for running C# Azure Functions in the isolated worker model. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=ihostapplicationbuilder%2Cconfig%2Cwindows#logging

- Microsoft. (n.d.). JSON serialization and deserialization in .NET - overview. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview

- Microsoft. (n.d.). Use the Azurite emulator for local Azure Storage development. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite

---

## References Part 3

- Microsoft. (n.d.). JwtBearer Class. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer?view=aspnetcore-10.0

- Microsoft. (n.d.). ASP.NET Core authorization with roles. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0

- Microsoft. (n.d.). ClaimTypes Class. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/system.security.claims.claimtypes?view=net-10.0

- Microsoft. (n.d.). JwtSecurityTokenHandler Class. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytokenhandler?view=msal-14.x

- Microsoft. (n.d.). Swashbuckle.AspNetCore - ASP.NET Core Swagger tooling. Retrieved from GitHub: https://github.com/domaindrivendev/Swashbuckle.AspNetCore

- Microsoft. (n.d.). Use Swagger/OpenAPI with ASP.NET Core. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/aspnet/core/tutorials/web-api-help-pages-using-swagger?view=aspnetcore-10.0

- Microsoft. (n.d.). ProblemDetails in ASP.NET Core. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/aspnet/core/web-api/handle-errors?view=aspnetcore-10.0

- Microsoft. (n.d.). ITableEntity Interface. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.itableentity?view=azure-dotnet

- Microsoft. (n.d.). TableClient.QueryAsync Method. Retrieved from Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.tableclient.queryasync?view=azure-dotnet

- BCrypt.Net. (n.d.). BCrypt password hashing for .NET. Retrieved from GitHub: https://github.com/BcryptNet/bcrypt.net

- OWASP. (n.d.). Password Storage Cheat Sheet. Retrieved from OWASP: https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html

- Internet Engineering Task Force. (2015). RFC 7519 - JSON Web Token (JWT). Retrieved from IETF: https://datatracker.ietf.org/doc/html/rfc7519

- Internet Engineering Task Force. (2015). RFC 7807 - Problem Details for HTTP APIs. Retrieved from IETF: https://datatracker.ietf.org/doc/html/rfc7807

---
