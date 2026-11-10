<#
.SYNOPSIS
    Team Member 2 - Document Management API end-to-end test (CoffeeNChill CLDV6212 Part 1)

.DESCRIPTION
    Starts the Azurite storage emulator and the Azure Functions host, then executes a
    full end-to-end test of the staff-documents document API owned by Team Member 2:

        POST /api/documents/upload                (multipart/form-data upload)
        POST /api/documents/upload/{fileName}     (upload with custom file name)
        GET  /api/documents                       (list all documents)
        GET  /api/documents/download/{fileName}   (download a document)
        GET  /api/documents/download/{fileName}   (404 when the file is missing)
        POST /api/documents/upload                (400 on empty payload)

    Each test prints a PASS / FAIL result and a final summary is shown.

.PARAMETER BaseUrl
    Base URL of the running Functions host. Defaults to http://localhost:7071/api

.PARAMETER KeepRunning
    When supplied, Azurite and the Functions host are left running after the tests
    complete (useful for a live demo / video). Without it, the Functions host
    started by this script is stopped again.

.EXAMPLE
    .\run-document-tests.ps1

.EXAMPLE
    .\run-document-tests.ps1 -KeepRunning
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:7071/api",
    [switch]$KeepRunning
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Join-Path $scriptDir "Cloud Development B CLDV 6212- POE Part 2"
$results = New-Object System.Collections.Generic.List[string]

function Write-Result([string]$status, [string]$message) {
    $color = if ($status -eq "PASS") { "Green" } else { "Red" }
    Write-Host ("[{0}] {1}" -f $status, $message) -ForegroundColor $color
    $results.Add(("$status`t$message"))
}

function Find-FuncExe {
    $cmd = Get-Command func -ErrorAction SilentlyContinue
    if ($cmd) {
        $shimDir = Split-Path -Parent $cmd.Source
        foreach ($candidate in @((Join-Path $shimDir "node_modules\azure-functions-core-tools\bin\func.exe"),
                                 $cmd.Source)) {
            if ($candidate -and $candidate -like "*.exe" -and (Test-Path $candidate)) { return $candidate }
        }
    }
    $npmFallback = Join-Path $env:APPDATA "npm\node_modules\azure-functions-core-tools\bin\func.exe"
    if (Test-Path $npmFallback) { return $npmFallback }
    $chocoFallback = "C:\Program Files\Azure Functions Core Tools\func.exe"
    if (Test-Path $chocoFallback) { return $chocoFallback }
    throw "Azure Functions Core Tools (func) not found. Install with: npm install -g azure-functions-core-tools@4"
}

function Test-TcpPort([int]$Port) {
    (Test-NetConnection -ComputerName localhost -Port $Port -WarningAction SilentlyContinue).TcpTestSucceeded
}

function Start-Azurite {
    docker info *> $null
    if ($LASTEXITCODE -ne 0) { throw "Docker is not running. Open Docker Desktop and try again." }

    $existing = docker ps -a --filter "name=azurite" --format "{{.Names}}"
    if ($existing -eq "azurite") {
        $running = docker ps --filter "name=azurite" --format "{{.Names}}"
        if ($running -eq "azurite") {
            Write-Host "Azurite container already running." -ForegroundColor Cyan
        } else {
            Write-Host "Starting existing Azurite container..." -ForegroundColor Cyan
            docker start azurite | Out-Null
        }
    } else {
        Write-Host "Pulling and starting Azurite container..." -ForegroundColor Cyan
        docker pull mcr.microsoft.com/azure-storage/azurite | Out-Null
        docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite mcr.microsoft.com/azure-storage/azurite | Out-Null
    }

    $deadline = (Get-Date).AddSeconds(60)
    while (-not (Test-TcpPort 10000)) {
        if ((Get-Date) -gt $deadline) { throw "Azurite did not become ready on port 10000." }
        Start-Sleep -Seconds 1
    }
    Write-Host "Azurite is ready (ports 10000-10002)." -ForegroundColor Cyan
}

function Start-FunctionsHost([string]$funcExe) {
    if (Test-TcpPort 7071) {
        Write-Host "Functions host already running on port 7071 - reusing it (not started by this script)." -ForegroundColor Cyan
        return $null
    }

    Write-Host "Starting Azure Functions host..." -ForegroundColor Cyan
    $out = Join-Path $env:TEMP "func_e2e_output.txt"
    $err = Join-Path $env:TEMP "func_e2e_error.txt"
    $env:AzureWebJobsStorage = "UseDevelopmentStorage=true"
    $env:FUNCTIONS_WORKER_RUNTIME = "dotnet-isolated"

    $proc = Start-Process -FilePath $funcExe -ArgumentList "start" `
        -WorkingDirectory $projectDir `
        -RedirectStandardOutput $out -RedirectStandardError $err -NoNewWindow -PassThru

    $deadline = (Get-Date).AddSeconds(150)
    while (-not (Test-TcpPort 7071)) {
        if ($proc.HasExited) {
            Write-Host "func exited early. Output:" -ForegroundColor Red
            if (Test-Path $out) { Get-Content $out -Tail 30 }
            throw "Azure Functions host failed to start."
        }
        if ((Get-Date) -gt $deadline) { throw "Timed out waiting for the Functions host on port 7071." }
        Start-Sleep -Seconds 2
    }
    # Give the worker a couple of seconds to register all functions.
    Start-Sleep -Seconds 5
    Write-Host "Functions host is ready on $BaseUrl" -ForegroundColor Cyan
    return $proc
}

function Stop-FunctionsHost($proc) {
    if ($proc -and -not $proc.HasExited) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Write-Host "Functions host stopped." -ForegroundColor Cyan
    }
}

function New-TestFiles {
    $dir = Join-Path $env:TEMP "cldv-doc-tests"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null

    $handbook = Join-Path $dir "staff-handbook.txt"
    Set-Content -Path $handbook -Value "CoffeeNChill Staff Handbook v2.0`nOperational guidelines for all baristas and kitchen staff.`nHealth and safety: always wash hands before handling food." -Encoding utf8

    $recipe = Join-Path $dir "barista-recipe-sheet.pdf"
    Set-Content -Path $recipe -Value "%%PDF-1.4`n1 0 obj<<>>endobj`ntrailer`n%%EOF" -Encoding ascii

    $empty = Join-Path $dir "empty.txt"
    [System.IO.File]::WriteAllBytes($empty, [byte[]]@())

    return @{ Handbook = $handbook; Recipe = $recipe; Empty = $empty }
}

function Invoke-CurlBody {
    param([string[]]$CurlArgs)
    $body = & curl.exe -s @CurlArgs 2>$null
    if ($LASTEXITCODE -ne 0) { throw "curl failed for: $($CurlArgs -join ' ')" }
    return ($body -join "`n")
}

# ---------------------------------------------------------------------------
# Setup
# ---------------------------------------------------------------------------
Write-Host "=============================================================" -ForegroundColor Yellow
Write-Host "  Team Member 2 - staff-documents Document API End-to-End Test"
Write-Host "=============================================================" -ForegroundColor Yellow

Start-Azurite
$funcExe = Find-FuncExe
$funcProc = Start-FunctionsHost -funcExe $funcExe
$files = New-TestFiles

try {
    # -----------------------------------------------------------------------
    # 1. Multipart upload  (POST /api/documents/upload)
    # -----------------------------------------------------------------------
    $out = Invoke-CurlBody -CurlArgs @("-X", "POST", "$BaseUrl/documents/upload", "-F", "file=@$($files.Recipe);type=application/pdf")
    if ($out -match '"fileName":"barista-recipe-sheet.pdf"' -and $out -match '"size":43') {
        Write-Result "PASS" "Multipart upload  POST /api/documents/upload  -> 201, fileName=barista-recipe-sheet.pdf"
    } else {
        throw "Unexpected response: $out"
    }

    # -----------------------------------------------------------------------
    # 2. Upload with custom file name  (POST /api/documents/upload/{fileName})
    # -----------------------------------------------------------------------
    $handbookBytes = [System.IO.File]::ReadAllBytes($files.Handbook)
    $uploaded = Invoke-RestMethod -Method Post -Uri "$BaseUrl/documents/upload/staff-handbook.txt" -Body $handbookBytes -ContentType "application/octet-stream"
    if ($uploaded.fileName -eq "staff-handbook.txt" -and $uploaded.size -eq $handbookBytes.Length) {
        Write-Result "PASS" "Named upload      POST /api/documents/upload/staff-handbook.txt  -> 201, $($handbookBytes.Length) bytes"
    } else {
        throw "Unexpected response: $($uploaded | ConvertTo-Json -Compress)"
    }

    # -----------------------------------------------------------------------
    # 3. List documents  (GET /api/documents)
    # -----------------------------------------------------------------------
    $list = Invoke-RestMethod -Method Get -Uri "$BaseUrl/documents"
    $pdf = @($list | Where-Object { $_.fileName -eq "barista-recipe-sheet.pdf" })
    $txt = @($list | Where-Object { $_.fileName -eq "staff-handbook.txt" })
    if ($pdf.Count -eq 1 -and $txt.Count -eq 1 -and
        $pdf[0].size -eq 43 -and $txt[0].size -eq $handbookBytes.Length -and
        $pdf[0].lastModified -and $txt[0].lastModified) {
        Write-Result "PASS" "List documents    GET /api/documents  -> 200, 2 files with name/size/lastModified"
    } else {
        $list | Format-Table fileName, size, lastModified -AutoSize
        throw "List did not contain the expected documents with metadata."
    }

    # -----------------------------------------------------------------------
    # 4. Download document  (GET /api/documents/download/{fileName})
    # -----------------------------------------------------------------------
    $download = Invoke-WebRequest -Uri "$BaseUrl/documents/download/staff-handbook.txt" -UseBasicParsing
    if ($download.StatusCode -ne 200) { throw "Download returned status $($download.StatusCode)" }
    if ($download.Content -notlike "*Staff Handbook*") { throw "Downloaded content did not match the uploaded file." }
    if (-not $download.Headers["Content-Disposition"]) { throw "Download response is missing the Content-Disposition header." }
    Write-Result "PASS" "Download document GET /api/documents/download/staff-handbook.txt  -> 200, streamed + Content-Disposition"

    # -----------------------------------------------------------------------
    # 5. Download missing file  (expect 404)
    # -----------------------------------------------------------------------
    try {
        Invoke-WebRequest -Uri "$BaseUrl/documents/download/not-found.pdf" -UseBasicParsing | Out-Null
        throw "Expected a 404 for a missing document but got a 200."
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 404) {
            Write-Result "PASS" "Missing document   GET /api/documents/download/not-found.pdf  -> 404 Not Found"
        } else {
            throw "Expected 404 but got $($_.Exception.Response.StatusCode.value__)"
        }
    }

    # -----------------------------------------------------------------------
    # 6. Empty upload  (expect 400)
    # -----------------------------------------------------------------------
    $out = Invoke-CurlBody -CurlArgs @("-X", "POST", "$BaseUrl/documents/upload", "-H", "Content-Type: text/plain", "--data-binary", "@$($files.Empty)")
    if ($out -match "No file data received") {
        Write-Result "PASS" "Empty upload       POST /api/documents/upload  -> 400 Bad Request"
    } else {
        throw "Expected 400 'No file data received' but got: $out"
    }

    # -----------------------------------------------------------------------
    # 7. Download the uploaded PDF  (verify round-trip content type)
    # -----------------------------------------------------------------------
    $pdfDownload = Invoke-WebRequest -Uri "$BaseUrl/documents/download/barista-recipe-sheet.pdf" -UseBasicParsing
    if ($pdfDownload.Headers["Content-Type"] -eq "application/pdf") {
        Write-Result "PASS" "PDF round trip     GET /api/documents/download/barista-recipe-sheet.pdf  -> 200, Content-Type: application/pdf"
    } else {
        throw "Expected Content-Type application/pdf but got: $($pdfDownload.Headers['Content-Type'])"
    }

    Write-Host "`n=============================================================" -ForegroundColor Yellow
    $passed = @($results | Where-Object { $_ -like "PASS*" }).Count
    $failed = @($results | Where-Object { $_ -like "FAIL*" }).Count
    Write-Host ("SUMMARY: {0} passed, {1} failed" -f $passed, $failed) -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
    Write-Host "=============================================================" -ForegroundColor Yellow

    if ($failed -gt 0) { exit 1 }
}
finally {
    if (-not $KeepRunning) {
        Stop-FunctionsHost $funcProc
    } else {
        Write-Host "`n-KeepRunning specified: Azurite and the Functions host were left running." -ForegroundColor Cyan
    }
}