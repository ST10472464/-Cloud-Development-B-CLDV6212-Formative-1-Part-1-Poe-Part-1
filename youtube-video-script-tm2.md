# YouTube Video Script — Team Member 2 (Azure Blob Storage & Document Functions)

**Student:** Yadav Iserbelas (ST10472501)
**Length:** ~3 minutes

---

## 0:00 – 0:20 | INTRO

**Screen:** Show GitHub repo

**Say:**
"Hi, I'm Yadav, Team Member 2 on the CoffeeNChill cloud project. My job was to build the staff document management system. I'll show you my four responsibilities: the blob container connection, uploading files, listing them, and downloading PDFs."

---

## 0:20 – 1:00 | CODE WALKTHROUGH

**Screen:** VS Code — open DocumentFunctions.cs

**Say:**
"Here in DocumentFunctions.cs, I connect to the staff-documents blob container using BlobServiceClient. This talks to Azurite, our local storage emulator. Every function uses this same connection."

**Scroll to UploadStaffDocument**
"This is my upload function. It takes a multipart form-data file and streams it straight into blob storage."

**Scroll to ListStaffDocuments**
"This lists every file in the container with its name, size, and last modified date."

**Scroll to DownloadStaffDocument**
"This streams the PDF back to the client. It checks if the file exists first — if not, it returns a 404."

**Show .csproj**
"I added the Azure.Storage.Blobs package to make this work."

---

## 1:00 – 1:20 | DOCKER

**Screen:** Terminal

**Say:**
"Now I start Azurite in Docker."

**Type and run:**
```
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite mcr.microsoft.com/azure-storage/azurite
```

**Say:**
"You can see the container is running with all three ports mapped."

**Run:**
```
docker ps
```

---

## 1:20 – 1:35 | START FUNCTIONS

**Screen:** Terminal

**Type and run:**
```
func start
```

**Say:**
"The functions are building. You can see my four document endpoints at the bottom — upload, list, and download. The host is now listening on port 7071."

---

## 1:35 – 2:20 | POSTMAN TESTS

**Screen:** Postman

**Say:**
"Now I'll test each endpoint with Postman."

**Click POST upload → Send**
"First, I upload a PDF as multipart form-data. There's the 201 — file stored."

**Click GET /documents → Send**
"Now I list all files. Each entry shows the name, size, and last modified date."

**Click GET /documents/download/barista-recipe-sheet.pdf → Send**
"Finally, I download the PDF. A 200 with the correct content type — the file round-trips byte for byte."

**Click GET /documents/download/not-found.pdf → Send**
"And if I ask for a file that doesn't exist, I get a clean 404."

---

## 2:20 – 2:45 | COMMITS

**Screen:** Terminal or GitHub

**Type:**
```
git log --oneline
```

**Say:**
"The deliverable required at least five commits. Here they are — the storage package, upload endpoints, list endpoint, download endpoint, and the README. All five are pushed to the repository."

---

## 2:45 – 3:00 | WRAP UP

**Screen:** Camera or repo page

**Say:**
"To summarise — I configured the staff-documents blob container, built a streaming upload, a list endpoint with full metadata, and a streaming PDF download, all running against Azurite in Docker with five commits. Thanks for watching."

---

## Quick Reference

**Docker command:**
```
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite mcr.microsoft.com/azure-storage/azurite
```

**Functions command:**
```
func start
```

**Postman requests:**
| Method | URL |
|--------|-----|
| POST | {{baseUrl}}/documents/upload |
| GET | {{baseUrl}}/documents |
| GET | {{baseUrl}}/documents/download/barista-recipe-sheet.pdf |
| GET | {{baseUrl}}/documents/download/not-found.pdf |

**Set {{baseUrl}} = http://localhost:7071/api**

**Sample files:**
```
C:\Users\emeris\AppData\Local\Temp\cldv-doc-tests\barista-recipe-sheet.pdf
```

---

## Tips
- Keep screen at 1080p
- Leave status codes visible for 2-3 seconds
- Upload as Unlisted on YouTube
- Add link to README "YouTube Demo"