# Part 1 Rubric & Requirements Audit

## Full Rubric (100 marks)

### 1. Submission, Structure & Documentation (10 marks)
**Criteria:**
- Repository organization & /docs
- Readme completeness & setup notes
- Git commit requirements (min 5 per student)
- Video link & presentation quality

**Score bands:**
- 0-3: Missing files or broken repo link. Fewer than 5 commits per student. No README or setup notes.
- 4-6: Basic README provided. Minimum commit threshold met. Video present but lacks thorough code explanation.
- 7-8: Clean folder structure with /docs directory. README covers local Azurite startup steps. Video clearly walks through code.
- 9-10: Exemplary README with step-by-step startup guide, architecture overview, individual role contributions, verified commit logs, and a professional unlisted YouTube video walk-through.

**Checklist:**
- [ ] /docs directory exists with Postman collection JSON
- [ ] README has step-by-step startup guide
- [ ] README has architecture overview
- [ ] README has individual role contributions section
- [ ] README has verified commit logs
- [ ] 5+ meaningful commits per student (Keyur, Yadav, Saiyen)
- [ ] Unlisted YouTube video link in README

---

### 2. Azure Tables & HTTP Functions - Menu Management (30 marks)
**Criteria:**
- MenuItems table schema (PartitionKey/RowKey)
- HTTP CRUD Functions (Create, Read, Update, Delete)
- Category filtering (/category/{category})
- Model validation & error handling

**Score bands:**
- 0-11: Table missing or HTTP Functions fail against Azurite. CRUD features broken or incomplete.
- 12-20: MenuItems table created. CRUD functions execute but lack input validation or proper HTTP status codes.
- 21-26: Complete CRUD operations working against Azurite. Category filtering functional with clean DTO mapping.
- 27-30: Flawless HTTP Functions with robust model validation, custom error handling (e.g., 404 Not Found, 400 Bad Request), category filtering, and clean separation of storage concerns.

**Endpoints required:**
- [ ] CreateMenuItem (POST /api/menu) - accepts JSON, inserts into MenuItems
- [ ] GetAllMenuItems (GET /api/menu) - queries and returns all entities
- [ ] GetMenuItemsByCategory (GET /api/menu/category/{category}) - filters by PartitionKey
- [ ] UpdateMenuItem (PUT /api/menu/{category}/{id}) - updates price or availability
- [ ] DeleteMenuItem (DELETE /api/menu/{category}/{id}) - removes item

**Schema:**
- PartitionKey: Category (string)
- RowKey: Unique SKU/ID (string)
- Name: Item name (string)
- Description: Short description (string)
- Price: Item price (Double/Decimal)
- IsAvailable: Availability status (Boolean)

---

### 3. Azure Files Integration - Staff Documents (20 marks)
**Criteria:**
- Staff-docs File Share configuration
- Upload function (POST /api/documents/upload)
- List function (GET /api/documents)
- Download function (GET /api/documents/download/{fileName})

**Score bands:**
- 0-7: File Share integration missing, unconfigured, or upload/download features fail completely.
- 8-12: staff-docs File Share created. Upload works but file listing or streaming download breaks or causes errors.
- 13-16: All three document functions (Upload, List, Download) working correctly against local Azurite storage.
- 17-20: Complete File Share implementation featuring stream-based file transfers, file metadata tracking (size, upload date), MIME-type validation, and full error logging.

**Endpoints required:**
- [ ] UploadStaffDocument (POST /api/documents/upload) - multipart/form-data, streams to staff-docs
- [ ] ListStaffDocuments (GET /api/documents) - lists files with name, size, last modified
- [ ] DownloadStaffDocument (GET /api/documents/download/{fileName}) - streams file back

---

### 4. Standalone Dockerfiles & Docker Hub Publishing (20 marks)
**Criteria:**
- Dockerfile syntax & base runtime
- Standalone execution (no Compose)
- Image publishing to Docker Hub
- Version tagging (v1.0)

**Score bands:**
- 0-7: Dockerfiles missing or build fails. Images not pushed to Docker Hub or fail to run in isolation.
- 8-12: Dockerfile builds locally. Container runs but fails to communicate with Azurite or lacks tag versioning on Docker Hub.
- 13-16: Application image builds cleanly, runs in standalone container, and is published with a version tag on Docker Hub.
- 17-20: Optimized multi-stage Dockerfile, explicit port binding, environment variables passed via CLI, and public Docker Hub repository with semantic version tags verified.

**Required:**
- [ ] Dockerfile in Azure Functions project root
- [ ] Uses official Azure Functions base runtime
- [ ] Multi-stage build (build + runtime)
- [ ] docker build -t <dockerhub_username>/coffeennchill-functions:v1.0
- [ ] docker push to public Docker Hub repo
- [ ] docker run -p 7071:80 -e AzureWebJobsStorage="UseDevelopmentStorage=true" <dockerhub_username>/coffeennchill-functions:v1.0
- [ ] No Docker Compose in Part 1

---

### 5. Testing Suite & Postman Collection (20 marks)
**Criteria:**
- Postman collection completeness
- Environment variable usage ({{baseUrl}})
- Test coverage across all endpoints

**Score bands:**
- 0-7: No Postman collection provided or endpoints fail to execute.
- 8-12: Basic Postman collection covering menu endpoints only. Hardcoded URLs used.
- 13-16: Postman collection covers all Table and File endpoints using collection variables.
- 17-20: Professional Postman collection organized into folders, using dynamic environment variables ({{baseUrl}}), containing sample request bodies for all scenarios, and passing saved automated tests.

**Required:**
- [ ] Collection covers ALL endpoints (Menu + Documents)
- [ ] Uses {{baseUrl}} collection variable (not hardcoded)
- [ ] Organized into folders (Menu, Document)
- [ ] Sample request bodies for all scenarios
- [ ] Automated test scripts that PASS
- [ ] Exported as .json and committed in /docs folder

---

### 6. No GitHub used (-5 marks penalty)
- [ ] GitHub used correctly (public repo, proper commits, etc.)

---

## Part 1 Deliverables Checklist (from rubric images)
- [ ] GitHub Repository link submitted on learning platform
- [ ] Minimum 5 meaningful commits per group member verified in Git history
- [ ] README.md formatted neatly in Markdown with:
  - [ ] Local setup steps
  - [ ] Standalone Docker execution commands
  - [ ] Section for each group member's contributions
  - [ ] YouTube video link
- [ ] Working HTTP Trigger Azure Functions connected to Azurite Table and File storage
- [ ] Verified Docker Hub link showing published, tagged container image:
  - [ ] coffeennchill-functions:v1.0
  - [ ] coffeennchill-Azurite:v1.0 (Azurite image)
- [ ] Postman Collection exported (.json) committed in /docs folder, verifying all HTTP function endpoints
- [ ] Video demonstrating docker run command and successful Postman collection execution
