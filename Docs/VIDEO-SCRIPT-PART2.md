# Part 2 Video Script — Saiyen's section (Docker Compose & Queue Testing, ~4 min)

Target: single-command `docker compose up` startup, messages landing in queues,
status march verified live in Azure Tables, image pulls from Docker Hub proven.
Record with voiceover (phone mic is fine). 1080p, terminal font ≥ 14pt.

## Setup before recording (2 min, off-camera)

1. `docker rmi st10466873/coffeenchill-functions:v2.0 mcr.microsoft.com/azure-storage/azurite:3.34.0`
2. `docker compose down -v` (clean slate — empty volume, no containers)
3. Open Postman with the Part 2 collection
   (`Docs/CLOUD DEVELOPMENT B CLDV6212-Poe Part 2.postman_collection.json`).
4. Start recording.

## Shot list (say the SAY lines out loud)

### 1. Clean pull + one-command startup (~60s)
- Run: `docker compose pull`
- SAY: "Starting from a clean machine with no images, compose pulls both
  images straight from Docker Hub — Azurite 3.34.0 and our published
  st10466873/coffeenchill-functions v2.0."
- LINGER on the `Pulled` lines — this is the Docker Hub proof.
- Run: `docker compose up -d`, then `docker ps`
- SAY: "One command starts the whole stack: Azurite on 10000-10002 and the
  Functions host on 7071, on a custom bridge network with a persistent
  Azurite volume."

### 2. Queue the order (~45s)
- In Postman send request **1. Place Order - Valid**.
- SAY: "Posting Jane Smith's order. The producer Base64-encodes the JSON and
  drops one message on order-processing-queue — 200 OK, non-blocking."
- Run: `docker logs coffeenchill-functions --tail 5` (optional B-roll).

### 3. Live status march (~90s)
- In Postman re-send request **5. Get Order Status** every ~3 seconds.
- SAY each result: "Received… Preparing… Ready… Collected — the
  QueueTrigger wrote the row to the Orders table and walked the full
  lifecycle in about seven seconds."
- Send request **6. List Orders by Date** → order appears in the daily list.
- Send request **2 (TotalPrice 0)** → 400. SAY: "Invalid orders are rejected
  before they ever reach the queue."

### 4. Teardown (~15s)
- Run: `docker compose down -v`
- SAY: "Clean shutdown, volume wiped — ready for the grader to repeat with a
  single docker compose up."
- Stop recording. Upload to YouTube as **Unlisted**, paste the link in the
  README under "YouTube Demo – Part 2".
