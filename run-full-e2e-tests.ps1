<#
.SYNOPSIS
    Full end-to-end test suite for the CoffeeNChill CLDV6212 Portfolio of Evidence.

.DESCRIPTION
    Spins up the complete local stack and exercises every graded surface of the
    project, then prints a PASS/FAIL line per assertion and a final summary.

      Phase A - Document API (direct Functions)          -> reuses run-document-tests.ps1 (7 tests)
      Phase B - Menu CRUD + validation (direct Functions) -> 12 tests
      Phase C - Order queue lifecycle (direct Functions)  -> 9 tests
      Phase D - Part 3 API gateway: Swagger/OpenAPI, JWT auth engine, claim
                round-trip, RBAC tiers (anonymous / Customer / Staff / Admin),
                queue -> Orders table round trip, staff document round trip
                through the gateway, and the Functions proxy -> ~40 tests

    Services started: Azurite (Docker), Azure Functions host (func start),
    ASP.NET Core API gateway (dotnet run on http://localhost:5080).

.PARAMETER FunctionsBaseUrl
    Functions host base URL. Defaults to http://localhost:7071/api.

.PARAMETER GatewayBaseUrl
    Gateway base URL. Defaults to http://localhost:5080 (matches the Part 3
    Postman collection variable {{baseUrl}}).

.PARAMETER KeepRunning
    Leave the Functions host and gateway running after the suite finishes
    (Azurite is always left running). Useful for manual poking afterwards.

.EXAMPLE
    .\run-full-e2e-tests.ps1

.EXAMPLE
    .\run-full-e2e-tests.ps1 -KeepRunning
#>
[CmdletBinding()]
param(
    [string]$FunctionsBaseUrl = "http://localhost:7071/api",
    [string]$GatewayBaseUrl = "http://localhost:5080",
    [switch]$KeepRunning
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$results = New-Object System.Collections.Generic.List[object]
$gwProc = $null

# ---------------------------------------------------------------------------
# Reporting
# ---------------------------------------------------------------------------
function Write-Result {
    param([string]$Status, [string]$Message, [switch]$Quiet)
    $results.Add([pscustomobject]@{ Status = $Status; Message = $Message })
    if (-not $Quiet) {
        $color = if ($Status -eq "PASS") { "Green" } else { "Red" }
        Write-Host "[$Status] $Message" -ForegroundColor $color
    }
}

function Assert-Test {
    param([string]$Name, [bool]$Condition, [string]$Detail = "")
    if ($Condition) { Write-Result PASS $Name }
    elseif ($Detail) { Write-Result FAIL "$Name -- $Detail" }
    else { Write-Result FAIL $Name }
}

function Show-Phase {
    param([string]$Title)
    Write-Host ""
    Write-Host ("=" * 70) -ForegroundColor Yellow
    Write-Host "  $Title" -ForegroundColor Yellow
    Write-Host ("=" * 70) -ForegroundColor Yellow
}

function Describe-Resp {
    param($R)
    if ($null -eq $R) { return "no response" }
    $b = "$($R.Body)" -replace "\s+", " "
    if ($b.Length -gt 180) { $b = $b.Substring(0, 180) + "..." }
    return "status=$($R.Status) body=$b"
}

# ---------------------------------------------------------------------------
# Small helpers
# ---------------------------------------------------------------------------
function Get-Prop {
    param($Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties | Where-Object { $_.Name -ieq $Name } | Select-Object -First 1
    if ($prop) { return $prop.Value }
    return $null
}

function Test-HasProp {
    param($Object, [string]$Name)
    if ($null -eq $Object) { return $false }
    return [bool]($Object.PSObject.Properties | Where-Object { $_.Name -ieq $Name })
}

function Test-TcpPort {
    param([int]$Port)
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $task = $client.BeginConnect("127.0.0.1", $Port, $null, $null)
        $ok = $task.AsyncWaitHandle.WaitOne(1500, $false)
        $connected = $ok -and $client.Connected
        $client.Close()
        return $connected
    } catch { return $false }
}

function Wait-ForTcp {
    param([int]$Port, [int]$TimeoutSec = 90)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-TcpPort $Port) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return (Test-TcpPort $Port)
}

# PS 5.1 friendly HTTP call that returns status/body/json for BOTH success and error responses.
function Invoke-Http {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Url,
        [string]$Body,
        [string]$ContentType = "application/json",
        [hashtable]$Headers,
        [int]$TimeoutSec = 30
    )
    $params = @{
        Uri             = $Url
        Method          = $Method
        UseBasicParsing = $true
        ErrorAction     = "Stop"
        TimeoutSec      = $TimeoutSec
    }
    if ($Body) {
        $params.Body = $Body
        $params.ContentType = $ContentType
    }
    if ($Headers) { $params.Headers = $Headers }

    try {
        $resp = Invoke-WebRequest @params
        $content = [string]$resp.Content
        $json = $null
        try {
            $trim = $content.TrimStart()
            if ($trim.StartsWith("{") -or $trim.StartsWith("[")) { $json = $content | ConvertFrom-Json }
        } catch { }
        return @{
            Status      = [int]$resp.StatusCode
            Body        = $content
            Json        = $json
            ContentType = [string]$resp.Headers["Content-Type"]
            Headers     = $resp.Headers
        }
    } catch {
        $response = $_.Exception.Response
        if ($response -is [System.Net.HttpWebResponse]) {
            $bodyText = ""
            try {
                $stream = $response.GetResponseStream()
                if ($stream) {
                    $reader = New-Object System.IO.StreamReader($stream)
                    $bodyText = $reader.ReadToEnd()
                    $reader.Dispose()
                }
            } catch { }
            $json = $null
            try {
                $trim = $bodyText.TrimStart()
                if ($trim.StartsWith("{") -or $trim.StartsWith("[")) { $json = $bodyText | ConvertFrom-Json }
            } catch { }
            return @{
                Status      = [int]$response.StatusCode
                Body        = $bodyText
                Json        = $json
                ContentType = [string]$response.Headers["Content-Type"]
                Headers     = $response.Headers
            }
        }
        return @{ Status = 0; Body = "$($_.Exception.Message)"; Json = $null; ContentType = ""; Headers = $null }
    }
}

function Get-JwtClaims {
    param([string]$Token)
    try {
        $parts = $Token.Split(".")
        if ($parts.Count -lt 3) { return $null }
        $payload = $parts[1].Replace("-", "+").Replace("_", "/")
        $payload += "=" * ((4 - ($payload.Length % 4)) % 4)
        $bytes = [Convert]::FromBase64String($payload)
        return ([System.Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json)
    } catch { return $null }
}

# ---------------------------------------------------------------------------
# Infrastructure
# ---------------------------------------------------------------------------
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

function Start-Azurite {
    docker info *> $null
    if ($LASTEXITCODE -ne 0) { throw "Docker is not running. Open Docker Desktop and try again." }

    # The gateway's Azure SDKs send x-ms-version 2026-10-06, which stock Azurite
    # rejects; the Part 3 brief requires Azurite to run with --skipApiVersionCheck.
    $running = docker ps --filter "name=azurite" --format "{{.Names}}"
    $exists = docker ps -a --filter "name=azurite" --format "{{.Names}}"
    $cmd = ""
    if ($running -eq "azurite" -or $exists -eq "azurite") {
        $cmd = "$(docker inspect azurite --format '{{.Config.Cmd}}' 2>$null)"
    }

    if ($running -eq "azurite" -and $cmd -like "*skipApiVersionCheck*") {
        Write-Host "Azurite container already running with --skipApiVersionCheck." -ForegroundColor Cyan
    } else {
        if ($exists -eq "azurite") {
            if ($cmd -like "*skipApiVersionCheck*") {
                Write-Host "Starting existing Azurite container..." -ForegroundColor Cyan
                docker start azurite | Out-Null
            } else {
                Write-Host "Recreating Azurite container with --skipApiVersionCheck..." -ForegroundColor Cyan
                docker rm -f azurite 2>$null | Out-Null
                docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite `
                    mcr.microsoft.com/azure-storage/azurite `
                    azurite -l /data --skipApiVersionCheck --blobHost 0.0.0.0 --queueHost 0.0.0.0 --tableHost 0.0.0.0 | Out-Null
            }
        } else {
            Write-Host "Pulling and starting Azurite container..." -ForegroundColor Cyan
            docker pull mcr.microsoft.com/azure-storage/azurite | Out-Null
            docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite `
                mcr.microsoft.com/azure-storage/azurite `
                azurite -l /data --skipApiVersionCheck --blobHost 0.0.0.0 --queueHost 0.0.0.0 --tableHost 0.0.0.0 | Out-Null
        }
    }

    if (-not (Wait-ForTcp -Port 10000 -TimeoutSec 60)) {
        throw "Azurite did not become ready on port 10000."
    }
    Write-Host "Azurite is ready (ports 10000-10002)." -ForegroundColor Cyan
}

function Start-FunctionsHost {
    if (Test-TcpPort 7071) {
        Write-Host "Functions host already running on port 7071 - reusing it." -ForegroundColor Cyan
        return $null
    }

    $funcExe = Find-FuncExe
    Write-Host "Starting Azure Functions host ($funcExe)..." -ForegroundColor Cyan
    $out = Join-Path $env:TEMP "e2e_func_out.txt"
    $err = Join-Path $env:TEMP "e2e_func_err.txt"
    $env:AzureWebJobsStorage = "UseDevelopmentStorage=true"
    $env:FUNCTIONS_WORKER_RUNTIME = "dotnet-isolated"

    $projectDir = Join-Path $scriptDir "Cloud Development B CLDV 6212- POE Part 2"
    $proc = Start-Process -FilePath $funcExe -ArgumentList "start" `
        -WorkingDirectory $projectDir `
        -RedirectStandardOutput $out -RedirectStandardError $err -NoNewWindow -PassThru

    $deadline = (Get-Date).AddSeconds(150)
    while (-not (Test-TcpPort 7071)) {
        if ($proc.HasExited) {
            Write-Host "func exited early. Output:" -ForegroundColor Red
            if (Test-Path $err) { Get-Content $err -Tail 30 }
            throw "Azure Functions host failed to start."
        }
        if ((Get-Date) -gt $deadline) { throw "Timed out waiting for the Functions host on port 7071." }
        Start-Sleep -Seconds 2
    }
    Start-Sleep -Seconds 5
    Write-Host "Functions host is ready on $FunctionsBaseUrl" -ForegroundColor Cyan
    return $proc
}

function Stop-Tree {
    param([int]$ProcessId)
    try { & taskkill.exe /PID $ProcessId /T /F 2>$null | Out-Null } catch { }
}

# ---------------------------------------------------------------------------
# Phase A - Document API (delegates to the run-document-tests.ps1 deliverable)
# ---------------------------------------------------------------------------
function Invoke-DocumentPhase {
    Show-Phase "PHASE A - Document API end-to-end (run-document-tests.ps1)"

    $docScript = Join-Path $scriptDir "run-document-tests.ps1"
    if (-not (Test-Path $docScript)) {
        Write-Result FAIL "A: run-document-tests.ps1 not found"
        return
    }

    $captured = @()
    try {
        $captured = @(& $docScript -KeepRunning 6>&1 | ForEach-Object { "$_" })
        $exitCode = $LASTEXITCODE
    } catch {
        $captured = @("EXCEPTION: $_")
        $exitCode = 1
    }

    foreach ($line in $captured) { Write-Host $line }

    $parsed = 0
    foreach ($line in $captured) {
        if ($line -match "^\[(PASS|FAIL)\]\s+(.+)$") {
            Write-Result -Status $Matches[1] -Message "A: $($Matches[2])" -Quiet
            $parsed++
        }
    }
    if ($parsed -eq 0) {
        Write-Result FAIL "A: document phase produced no results (exit=$exitCode)"
    } elseif ($exitCode -ne 0) {
        $failCount = @($captured | Where-Object { $_ -match "^\[FAIL\]" }).Count
        if ($failCount -eq 0) {
            Write-Result FAIL "A: run-document-tests.ps1 exited with code $exitCode"
        }
    }
}

# ---------------------------------------------------------------------------
# Phase B - Menu CRUD against the Functions host
# ---------------------------------------------------------------------------
function Invoke-MenuPhase {
    Show-Phase "PHASE B - Menu CRUD + validation (Functions direct)"

    $base = $FunctionsBaseUrl
    $sku = "E2E-$stamp"
    $createBody = @{
        PartitionKey = "Coffee"
        RowKey       = $sku
        Name         = "E2E Latte"
        Description  = "Created by the full E2E suite"
        Price        = 25.5
        IsAvailable  = $true
    } | ConvertTo-Json

    # B01 create
    $r = Invoke-Http POST "$base/menu" $createBody
    Assert-Test "B01 POST /menu valid item -> 201 Created" `
        ($r.Status -eq 201 -and (Get-Prop $r.Json "RowKey") -eq $sku -and (Get-Prop $r.Json "Price") -eq 25.5) `
        (Describe-Resp $r)

    # B02 duplicate
    $r = Invoke-Http POST "$base/menu" $createBody
    Assert-Test "B02 POST /menu duplicate SKU -> 400 already exists" `
        ($r.Status -eq 400 -and "$($r.Body)" -like "*already exists*") (Describe-Resp $r)

    # B03 invalid price
    $badPrice = $createBody | ConvertFrom-Json
    $badPrice.RowKey = "E2E-BAD-$stamp"
    $badPrice.Price = 0
    $r = Invoke-Http POST "$base/menu" ($badPrice | ConvertTo-Json)
    Assert-Test "B03 POST /menu Price 0 -> 400 validation failed" `
        ($r.Status -eq 400 -and "$($r.Body)" -like "*Validation*") (Describe-Resp $r)

    # B04 malformed JSON
    $r = Invoke-Http POST "$base/menu" "this-is-not-json{"
    Assert-Test "B04 POST /menu malformed JSON -> 400 invalid JSON" `
        ($r.Status -eq 400 -and "$($r.Body)" -like "*Invalid JSON*") (Describe-Resp $r)

    # B05 list contains item
    $r = Invoke-Http GET "$base/menu"
    $items = @()
    if ($r.Json) { $items = @($r.Json) }
    $found = @($items | Where-Object { (Get-Prop $_ "RowKey") -eq $sku }).Count -eq 1
    Assert-Test "B05 GET /menu -> 200 and lists the new item" `
        ($r.Status -eq 200 -and $found) (Describe-Resp $r)

    # B06 category filter
    $r = Invoke-Http GET "$base/menu/category/Coffee"
    $catItems = @()
    if ($r.Json) { $catItems = @($r.Json) }
    $allCoffee = @($catItems | Where-Object { (Get-Prop $_ "PartitionKey") -ne "Coffee" }).Count -eq 0
    $found = @($catItems | Where-Object { (Get-Prop $_ "RowKey") -eq $sku }).Count -eq 1
    Assert-Test "B06 GET /menu/category/Coffee -> 200 filtered to Coffee only" `
        ($r.Status -eq 200 -and $allCoffee -and $found) (Describe-Resp $r)

    # B07 update
    $updateBody = @{
        Name         = "E2E Latte Updated"
        Description  = "Price changed by E2E"
        Price        = 30.75
        IsAvailable  = $false
    } | ConvertTo-Json
    $r = Invoke-Http PUT "$base/menu/Coffee/$sku" $updateBody
    Assert-Test "B07 PUT /menu/{category}/{sku} -> 200 with new price" `
        ($r.Status -eq 200 -and (Get-Prop $r.Json "Price") -eq 30.75 -and (Get-Prop $r.Json "IsAvailable") -eq $false) `
        (Describe-Resp $r)

    # B08 persistence
    $r = Invoke-Http GET "$base/menu"
    $items = @()
    if ($r.Json) { $items = @($r.Json) }
    $updated = @($items | Where-Object { (Get-Prop $_ "RowKey") -eq $sku -and (Get-Prop $_ "Price") -eq 30.75 }).Count -eq 1
    Assert-Test "B08 GET /menu reflects the update (persisted)" ($r.Status -eq 200 -and $updated) (Describe-Resp $r)

    # B09 delete
    $r = Invoke-Http DELETE "$base/menu/Coffee/$sku"
    Assert-Test "B09 DELETE /menu/{category}/{sku} -> 200" ($r.Status -eq 200) (Describe-Resp $r)

    # B10 gone from category
    $r = Invoke-Http GET "$base/menu/category/Coffee"
    $items = @()
    if ($r.Json) { $items = @($r.Json) }
    $gone = @($items | Where-Object { (Get-Prop $_ "RowKey") -eq $sku }).Count -eq 0
    Assert-Test "B10 GET /menu/category/Coffee no longer lists the item" ($r.Status -eq 200 -and $gone) (Describe-Resp $r)

    # B11 delete again
    $r = Invoke-Http DELETE "$base/menu/Coffee/$sku"
    Assert-Test "B11 DELETE same item again -> 404 Not Found" ($r.Status -eq 404) (Describe-Resp $r)

    # B12 update missing item
    $r = Invoke-Http PUT "$base/menu/Coffee/NOPE-$stamp" '{"Price":10}'
    Assert-Test "B12 PUT missing item -> 404 Not Found" ($r.Status -eq 404) (Describe-Resp $r)
}

# ---------------------------------------------------------------------------
# Phase C - Order queue lifecycle against the Functions host
# ---------------------------------------------------------------------------
function Invoke-OrderPhase {
    Show-Phase "PHASE C - Order queue -> Orders table lifecycle (Functions direct)"

    $base = $FunctionsBaseUrl

    # C01 valid queue
    $validOrder = @{
        OrderId          = $orderId
        CustomerName     = "E2E Customer"
        SelectedItemSKUs = @("COF-001", "PAS-104")
        TotalPrice       = 65.00
        OrderTimestamp   = "$orderDate" + "T10:15:00Z"
    } | ConvertTo-Json
    $r = Invoke-Http POST "$base/orders/queue" $validOrder
    Assert-Test "C01 POST /orders/queue valid order -> 200 queued" `
        ($r.Status -eq 200 -and (Get-Prop $r.Json "message") -eq "Order queued successfully" -and (Get-Prop $r.Json "orderId") -eq $orderId) `
        (Describe-Resp $r)

    # C02 TotalPrice 0
    $o = $validOrder | ConvertFrom-Json
    $o.OrderId = "ORD-E2E-ZERO-$stamp"; $o.TotalPrice = 0
    $r = Invoke-Http POST "$base/orders/queue" ($o | ConvertTo-Json)
    Assert-Test "C02 POST /orders/queue TotalPrice 0 -> 400 mentions TotalPrice" `
        ($r.Status -eq 400 -and "$($r.Body)" -like "*TotalPrice*") (Describe-Resp $r)

    # C03 missing OrderId
    $r = Invoke-Http POST "$base/orders/queue" '{"CustomerName":"X","SelectedItemSKUs":["COF-001"],"TotalPrice":10}'
    Assert-Test "C03 POST /orders/queue missing OrderId -> 400 mentions OrderId" `
        ($r.Status -eq 400 -and "$($r.Body)" -like "*OrderId*") (Describe-Resp $r)

    # C04 empty SKUs
    $c04Body = '{"OrderId":"ORD-E2E-SKU-' + $stamp + '","CustomerName":"X","SelectedItemSKUs":[],"TotalPrice":10}'
    $r = Invoke-Http POST "$base/orders/queue" $c04Body
    Assert-Test "C04 POST /orders/queue empty SKUs -> 400 mentions SKU" `
        ($r.Status -eq 400 -and "$($r.Body)" -like "*SKU*") (Describe-Resp $r)

    # C05 missing CustomerName
    $c05Body = '{"OrderId":"ORD-E2E-CN-' + $stamp + '","SelectedItemSKUs":["COF-001"],"TotalPrice":10}'
    $r = Invoke-Http POST "$base/orders/queue" $c05Body
    Assert-Test "C05 POST /orders/queue missing CustomerName -> 400 mentions CustomerName" `
        ($r.Status -eq 400 -and "$($r.Body)" -like "*CustomerName*") (Describe-Resp $r)

    # C06/C07 poll the lifecycle until Collected
    $seen = New-Object System.Collections.Generic.List[string]
    $lastGood = $null
    $finalStatus = $null
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline) {
        $g = Invoke-Http GET "$base/orders/$orderDate/$orderId" -TimeoutSec 15
        if ($g.Status -eq 200 -and $g.Json) {
            $lastGood = $g
            $st = [string](Get-Prop $g.Json "Status")
            $finalStatus = $st
            if ($st -and -not $seen.Contains($st)) { $seen.Add($st) }
            if ($st -eq "Collected") { break }
        }
        Start-Sleep -Seconds 2
    }

    $pk = if ($lastGood) { [string](Get-Prop $lastGood.Json "PartitionKey") } else { "" }
    $rk = if ($lastGood) { [string](Get-Prop $lastGood.Json "RowKey") } else { "" }
    $lifecycle = @("Received", "Preparing", "Ready", "Collected")
    Assert-Test "C06 GET /orders/{date}/{orderId} -> 200 with date PartitionKey + lifecycle status" `
        ($null -ne $lastGood -and $pk -eq $orderDate -and $rk -eq $orderId -and $lifecycle -contains $finalStatus) `
        "pk=$pk rk=$rk status=$finalStatus"

    Assert-Test "C07 queue processor advanced the order to Collected (Received -> ... -> Collected)" `
        ($finalStatus -eq "Collected") "observed=[$($seen -join ' -> ')]"

    # C08 list by date
    $r = Invoke-Http GET "$base/orders/$orderDate"
    $orders = @()
    if ($r.Json) { $orders = @($r.Json) }
    $listed = @($orders | Where-Object { (Get-Prop $_ "RowKey") -eq $orderId }).Count -eq 1
    $hasCustomer = $false
    $match = @($orders | Where-Object { (Get-Prop $_ "RowKey") -eq $orderId }) | Select-Object -First 1
    if ($match -and (Get-Prop $match "CustomerName") -eq "E2E Customer") { $hasCustomer = $true }
    Assert-Test "C08 GET /orders/{date} -> 200 array containing the order" `
        ($r.Status -eq 200 -and $listed -and $hasCustomer) (Describe-Resp $r)

    # C09 missing order
    $r = Invoke-Http GET "$base/orders/$orderDate/ORD-NOPE-$stamp"
    Assert-Test "C09 GET /orders/{date}/{missingId} -> 404 Not Found" ($r.Status -eq 404) (Describe-Resp $r)
}

# ---------------------------------------------------------------------------
# Phase D - Part 3 API gateway
# ---------------------------------------------------------------------------
function Invoke-GatewayPhase {
    Show-Phase "PHASE D - API gateway: Swagger, JWT auth, RBAC, orders, documents"

    $gw = $GatewayBaseUrl

    # --- Swagger ---------------------------------------------------------
    $r = Invoke-Http GET "$gw/swagger"
    Assert-Test "D01 GET /swagger -> 200 Swagger UI" `
        ($r.Status -eq 200 -and "$($r.Body)" -match "swagger-ui") (Describe-Resp $r)

    $r = Invoke-Http GET "$gw/swagger/v1/swagger.json"
    $swBody = "$($r.Body)"
    $swOk = ($r.Status -eq 200) -and
        ($swBody -like '*"Bearer"*') -and
        ($swBody -like '*securityScheme*') -and
        ($swBody -like '*"/api/auth/login"*') -and
        ($swBody -like '*"/api/menu"*') -and
        ($swBody -like '*"/api/orders/queue"*') -and
        ($swBody -like '*"/api/documents"*')
    Assert-Test "D02 GET /swagger/v1/swagger.json -> 200 with Bearer security scheme + all route groups" $swOk (Describe-Resp $r)

    # --- Registration ----------------------------------------------------
    $regAdmin = @{ fullName = $adminName; email = $adminEmail; password = $password; role = "Admin" } | ConvertTo-Json
    $r = Invoke-Http POST "$gw/api/auth/register" $regAdmin
    $regOk = ($r.Status -eq 201 -and (Get-Prop $r.Json "role") -eq "Admin" -and
        (Get-Prop $r.Json "email") -eq $adminEmail -and
        (Get-Prop $r.Json "fullName") -eq $adminName -and
        $null -ne (Get-Prop $r.Json "createdDate") -and
        -not (Test-HasProp $r.Json "passwordHash") -and
        -not (Test-HasProp $r.Json "password")) -or ($r.Status -eq 409)
    Assert-Test "D03 POST /api/auth/register Admin -> 201 (no hash leaked)" $regOk (Describe-Resp $r)

    $regCustomer = @{ fullName = $customerName; email = $customerEmail; password = $password; role = "Customer" } | ConvertTo-Json
    $r = Invoke-Http POST "$gw/api/auth/register" $regCustomer
    $regOk = ($r.Status -eq 201 -and (Get-Prop $r.Json "role") -eq "Customer") -or ($r.Status -eq 409)
    Assert-Test "D04 POST /api/auth/register Customer -> 201" $regOk (Describe-Resp $r)

    $regStaff = @{ fullName = $staffName; email = $staffEmail; password = $password; role = "Staff" } | ConvertTo-Json
    $r = Invoke-Http POST "$gw/api/auth/register" $regStaff
    $regOk = ($r.Status -eq 201 -and (Get-Prop $r.Json "role") -eq "Staff") -or ($r.Status -eq 409)
    Assert-Test "D05 POST /api/auth/register Staff -> 201" $regOk (Describe-Resp $r)

    # D06 duplicate email -> 409 problem+json
    $r = Invoke-Http POST "$gw/api/auth/register" $regCustomer
    Assert-Test "D06 POST /api/auth/register duplicate email -> 409 problem+json with traceId" `
        ($r.Status -eq 409 -and (Get-Prop $r.Json "title") -eq "Resource already exists" -and $null -ne (Get-Prop $r.Json "traceId")) `
        (Describe-Resp $r)

    # D07 weak password rejected
    $weak = @{ fullName = "Weak Password"; email = "weak$stamp@test.ac.za"; password = "short"; role = "Customer" } | ConvertTo-Json
    $r = Invoke-Http POST "$gw/api/auth/register" $weak
    $weakBody = "$($r.Body)"
    Assert-Test "D07 POST /api/auth/register short password -> 400 rejected" `
        ($r.Status -eq 400 -and $weakBody -like "*assword*") (Describe-Resp $r)

    # --- Login + JWT claims ---------------------------------------------
    $r = Invoke-Http POST "$gw/api/auth/login" (@{ email = $adminEmail; password = $password } | ConvertTo-Json)
    $adminToken = $null
    $loginOk = $r.Status -eq 200 -and (Get-Prop $r.Json "tokenType") -eq "Bearer" -and (Get-Prop $r.Json "role") -eq "Admin"
    if ($loginOk) { $adminToken = [string](Get-Prop $r.Json "token") }
    Assert-Test "D08 POST /api/auth/login Admin -> 200 Bearer token" ($loginOk -and $adminToken.Length -gt 50) (Describe-Resp $r)

    $claims = Get-JwtClaims $adminToken
    $claimName = [string](Get-Prop $claims "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
    $claimEmail = [string](Get-Prop $claims "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")
    $claimRole = [string](Get-Prop $claims "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
    $claimIss = [string](Get-Prop $claims "iss")
    $claimAud = [string](Get-Prop $claims "aud")
    $claimExp = Get-Prop $claims "exp"
    $hasPwdClaim = $false
    if ($claims) {
        foreach ($p in $claims.PSObject.Properties) {
            if ($p.Name -match "password|hash") { $hasPwdClaim = $true }
        }
    }
    $expOk = $false
    if ($claimExp) { $expOk = [double]$claimExp -gt [double][DateTimeOffset]::UtcNow.ToUnixTimeSeconds() }
    Assert-Test "D09 JWT carries ClaimTypes.Name/Email/Role, iss/aud/exp, no password claim" `
        ($null -ne $claims -and $claimName -eq $adminName -and $claimEmail -eq $adminEmail -and
         $claimRole -eq "Admin" -and $claimIss -eq "CoffeeNChill" -and $claimAud -eq "CoffeeNChill Clients" -and
         $expOk -and -not $hasPwdClaim) `
        "name=$claimName email=$claimEmail role=$claimRole iss=$claimIss aud=$claimAud expOk=$expOk pwdClaim=$hasPwdClaim"

    $r = Invoke-Http POST "$gw/api/auth/login" (@{ email = $customerEmail; password = $password } | ConvertTo-Json)
    $customerToken = $null
    $loginOk = $r.Status -eq 200 -and (Get-Prop $r.Json "role") -eq "Customer"
    if ($loginOk) { $customerToken = [string](Get-Prop $r.Json "token") }
    Assert-Test "D10 POST /api/auth/login Customer -> 200 token" ($loginOk -and $customerToken.Length -gt 50) (Describe-Resp $r)

    $r = Invoke-Http POST "$gw/api/auth/login" (@{ email = $staffEmail; password = $password } | ConvertTo-Json)
    $staffToken = $null
    $loginOk = $r.Status -eq 200 -and (Get-Prop $r.Json "role") -eq "Staff"
    if ($loginOk) { $staffToken = [string](Get-Prop $r.Json "token") }
    Assert-Test "D11 POST /api/auth/login Staff -> 200 token" ($loginOk -and $staffToken.Length -gt 50) (Describe-Resp $r)

    # D12 wrong password
    $r = Invoke-Http POST "$gw/api/auth/login" (@{ email = $adminEmail; password = "DefinitelyWrong1" } | ConvertTo-Json)
    $wrongPwDetail = [string](Get-Prop $r.Json "detail")
    Assert-Test "D12 POST /api/auth/login wrong password -> 401 generic message" `
        ($r.Status -eq 401 -and $wrongPwDetail -eq "Invalid email or password.") (Describe-Resp $r)

    # D13 unknown email - identical message (no user enumeration)
    $r = Invoke-Http POST "$gw/api/auth/login" (@{ email = "ghost$stamp@nowhere.test"; password = "Whatever123" } | ConvertTo-Json)
    $unknownDetail = [string](Get-Prop $r.Json "detail")
    Assert-Test "D13 POST /api/auth/login unknown email -> 401 same message (no enumeration)" `
        ($r.Status -eq 401 -and $unknownDetail -eq $wrongPwDetail -and $unknownDetail -eq "Invalid email or password.") `
        (Describe-Resp $r)

    # --- /api/auth/me ----------------------------------------------------
    $authH = @{ Authorization = "Bearer $adminToken" }
    $r = Invoke-Http GET "$gw/api/auth/me" -Headers $authH
    Assert-Test "D14 GET /api/auth/me with Admin token -> 200 identity round-trip" `
        ($r.Status -eq 200 -and (Get-Prop $r.Json "email") -eq $adminEmail -and
         (Get-Prop $r.Json "role") -eq "Admin" -and (Get-Prop $r.Json "fullName") -eq $adminName -and
         -not (Test-HasProp $r.Json "passwordHash")) (Describe-Resp $r)

    $r = Invoke-Http GET "$gw/api/auth/me"
    Assert-Test "D15 GET /api/auth/me no token -> 401 problem+json" `
        ($r.Status -eq 401 -and (Get-Prop $r.Json "title") -eq "Authentication required" -and
         "$($r.ContentType)" -like "*problem+json*") (Describe-Resp $r)

    $r = Invoke-Http GET "$gw/api/auth/me" -Headers @{ Authorization = "Bearer not-a-real-token" }
    Assert-Test "D16 GET /api/auth/me garbage token -> 401" `
        ($r.Status -eq 401 -and (Get-Prop $r.Json "title") -eq "Authentication required") (Describe-Resp $r)

    # --- Menu RBAC tiers -------------------------------------------------
    $r = Invoke-Http GET "$gw/api/menu"
    Assert-Test "D17 GET /api/menu anonymous -> 200 public read tier" `
        ($r.Status -eq 200 -and $r.Json -is [System.Array]) (Describe-Resp $r)

    $gwSku1Body = @{
        category = "Coffee"; sku = $gwSku1; name = "E2E Gateway Latte"
        description = "Created through the Part 3 gateway by an Admin"
        price = 34.5; isAvailable = $true
    } | ConvertTo-Json

    $r = Invoke-Http POST "$gw/api/menu" $gwSku1Body
    Assert-Test "D18 POST /api/menu no token -> 401 explains how to authenticate" `
        ($r.Status -eq 401 -and "$([string](Get-Prop $r.Json "detail"))" -like "*/api/auth/login*") (Describe-Resp $r)

    $custH = @{ Authorization = "Bearer $customerToken" }
    $r = Invoke-Http POST "$gw/api/menu" $gwSku1Body -Headers $custH
    Assert-Test "D19 POST /api/menu Customer token -> 403 Insufficient role" `
        ($r.Status -eq 403 -and (Get-Prop $r.Json "title") -eq "Insufficient role") (Describe-Resp $r)

    $r = Invoke-Http POST "$gw/api/menu" $gwSku1Body -Headers $authH
    Assert-Test "D20 POST /api/menu Admin token -> 201 created (no entity internals exposed)" `
        ($r.Status -eq 201 -and (Get-Prop $r.Json "sku") -eq $gwSku1 -and
         (Get-Prop $r.Json "category") -eq "Coffee" -and (Get-Prop $r.Json "price") -eq 34.5 -and
         (Get-Prop $r.Json "isAvailable") -eq $true -and
         -not (Test-HasProp $r.Json "etag") -and -not (Test-HasProp $r.Json "timestamp") -and
         -not (Test-HasProp $r.Json "partitionKey")) (Describe-Resp $r)

    $r = Invoke-Http GET "$gw/api/menu"
    $items = @(); if ($r.Json) { $items = @($r.Json) }
    $publicSees = @($items | Where-Object { (Get-Prop $_ "sku") -eq $gwSku1 }).Count -eq 1
    Assert-Test "D21 GET /api/menu anonymous sees the Admin-created item" ($r.Status -eq 200 -and $publicSees) (Describe-Resp $r)

    $staffH = @{ Authorization = "Bearer $staffToken" }
    $gwSku2Body = @{
        category = "Pastry"; sku = $gwSku2; name = "E2E Staff Muffin"
        description = "Created through the Part 3 gateway by Staff"
        price = 22; isAvailable = $true
    } | ConvertTo-Json
    $r = Invoke-Http POST "$gw/api/menu" $gwSku2Body -Headers $staffH
    Assert-Test "D22 POST /api/menu Staff token -> 201 (Staff tier allowed)" `
        ($r.Status -eq 201 -and (Get-Prop $r.Json "sku") -eq $gwSku2) (Describe-Resp $r)

    $r = Invoke-Http PUT "$gw/api/menu/Coffee/$gwSku1" '{"price":39.5,"isAvailable":false,"name":"E2E Gateway Latte v2"}' -Headers $authH
    Assert-Test "D23 PUT /api/menu/{category}/{sku} Admin -> 200 updated" `
        ($r.Status -eq 200 -and (Get-Prop $r.Json "price") -eq 39.5 -and (Get-Prop $r.Json "isAvailable") -eq $false) `
        (Describe-Resp $r)

    $r1 = Invoke-Http DELETE "$gw/api/menu/Coffee/$gwSku1" -Headers $authH
    $r2 = Invoke-Http DELETE "$gw/api/menu/Pastry/$gwSku2" -Headers $authH
    Assert-Test "D24 DELETE /api/menu/{category}/{sku} Admin -> 204 (both items)" `
        ($r1.Status -eq 204 -and $r2.Status -eq 204) "first=$(Describe-Resp $r1) second=$(Describe-Resp $r2)"

    $r = Invoke-Http GET "$gw/api/menu"
    $items = @(); if ($r.Json) { $items = @($r.Json) }
    $noneLeft = @($items | Where-Object { (Get-Prop $_ "sku") -in @($gwSku1, $gwSku2) }).Count -eq 0
    Assert-Test "D25 GET /api/menu confirms both items deleted" ($r.Status -eq 200 -and $noneLeft) (Describe-Resp $r)

    # --- Orders through the gateway --------------------------------------
    $gwOrderBody = @{
        orderId = $gwOrderId; customerName = "Jane Smith"
        selectedItemSKUs = @("COF-001", "PAS-104")
        totalPrice = 65.00; orderTimestamp = "$orderDate" + "T11:00:00Z"
    } | ConvertTo-Json
    $r = Invoke-Http POST "$gw/api/orders/queue" $gwOrderBody
    Assert-Test "D26 POST /api/orders/queue anonymous -> 200 queued" `
        ($r.Status -eq 200 -and (Get-Prop $r.Json "message") -eq "Order queued successfully" -and
         (Get-Prop $r.Json "orderId") -eq $gwOrderId) (Describe-Resp $r)

    # The gateway only writes the queue message; the Functions host must pick it
    # up and walk it through Received -> Preparing -> Ready -> Collected.
    $gwFinal = $null
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline) {
        $g = Invoke-Http GET "$gw/api/orders/$orderDate/$gwOrderId" -TimeoutSec 15
        if ($g.Status -eq 200 -and $g.Json) {
            $gwFinal = [string](Get-Prop $g.Json "status")
            if ($gwFinal -eq "Collected") { break }
        }
        Start-Sleep -Seconds 2
    }
    Assert-Test "D27 gateway order flowed queue -> Functions processor -> Orders table -> Collected" `
        ($gwFinal -eq "Collected") "final=$gwFinal"

    $r = Invoke-Http GET "$gw/api/orders/$orderDate"
    $orders = @(); if ($r.Json) { $orders = @($r.Json) }
    $listed = @($orders | Where-Object { (Get-Prop $_ "orderId") -eq $gwOrderId }).Count -eq 1
    Assert-Test "D28 GET /api/orders/{date} lists the gateway order" ($r.Status -eq 200 -and $listed) (Describe-Resp $r)

    $statusBody = @{ orderDate = $orderDate; orderId = $gwOrderId; status = "Collected" } | ConvertTo-Json
    $r = Invoke-Http PUT "$gw/api/orders/status" $statusBody -Headers $custH
    Assert-Test "D29 PUT /api/orders/status Customer token -> 403 Insufficient role" `
        ($r.Status -eq 403 -and (Get-Prop $r.Json "title") -eq "Insufficient role") (Describe-Resp $r)

    $statusBody = @{ orderDate = $orderDate; orderId = $gwOrderId; status = "Ready" } | ConvertTo-Json
    $r = Invoke-Http PUT "$gw/api/orders/status" $statusBody -Headers $authH
    Assert-Test "D30 PUT /api/orders/status Admin token -> 200 advanced to Ready" `
        ($r.Status -eq 200 -and (Get-Prop $r.Json "status") -eq "Ready") (Describe-Resp $r)

    # --- Staff documents through the gateway -----------------------------
    $r = Invoke-Http GET "$gw/api/documents" -Headers $authH
    $docs = @(); if ($r.Json -is [System.Array]) { $docs = @($r.Json) }
    $allHaveProps = $docs.Count -ge 0
    foreach ($d in $docs) {
        if (-not (Test-HasProp $d "name") -or -not (Test-HasProp $d "size")) { $allHaveProps = $false }
    }
    Assert-Test "D31 GET /api/documents Admin token -> 200 array with name/size" `
        ($r.Status -eq 200 -and $r.Json -is [System.Array] -and $allHaveProps) (Describe-Resp $r)

    $r = Invoke-Http GET "$gw/api/documents"
    Assert-Test "D32 GET /api/documents no token -> 401 not leaked anonymously" `
        ($r.Status -eq 401 -and -not ($r.Json -is [System.Array]) -and (Get-Prop $r.Json "title") -eq "Authentication required") `
        (Describe-Resp $r)

    $r = Invoke-Http GET "$gw/api/documents" -Headers $custH
    Assert-Test "D33 GET /api/documents Customer token -> 403 Insufficient role" `
        ($r.Status -eq 403 -and (Get-Prop $r.Json "title") -eq "Insufficient role") (Describe-Resp $r)

    # Upload -> list -> download -> delete round trip (multipart via curl).
    $docName = "e2e-gateway-$stamp.txt"
    $docMarker = "E2E-GW-DOC-$stamp"
    $docPath = Join-Path $env:TEMP $docName
    Set-Content -Path $docPath -Value $docMarker -NoNewline
    $curlOut = Join-Path $env:TEMP "e2e_curl_upload.json"
    $httpCode = & curl.exe -s -o $curlOut -w "%{http_code}" -X POST "$gw/api/documents" `
        -H "Authorization: Bearer $adminToken" -F "file=@$docPath;type=text/plain" 2>$null
    $uploadBody = ""
    if (Test-Path $curlOut) { $uploadBody = Get-Content $curlOut -Raw }
    $uploadJson = $null
    try { $uploadJson = $uploadBody | ConvertFrom-Json } catch { }
    Assert-Test "D34 POST /api/documents multipart upload Admin -> 201" `
        ("$httpCode" -eq "201" -and (Get-Prop $uploadJson "name") -eq $docName) `
        "http=$httpCode body=$uploadBody"

    $r = Invoke-Http GET "$gw/api/documents" -Headers $authH
    $docs = @(); if ($r.Json) { $docs = @($r.Json) }
    $listed = @($docs | Where-Object { (Get-Prop $_ "name") -eq $docName }).Count -eq 1
    Assert-Test "D35 GET /api/documents lists the uploaded file" ($r.Status -eq 200 -and $listed) (Describe-Resp $r)

    $r = Invoke-Http GET "$gw/api/documents/download/$docName" -Headers $authH
    $disposition = $null
    if ($r.Headers) { $disposition = $r.Headers["Content-Disposition"] }
    $dlOk = ($r.Status -eq 200) -and
        ("$($r.Body)" -like "*$docMarker*") -and
        ($null -ne $disposition)
    Assert-Test "D36 GET /api/documents/download/{name} -> 200 content + Content-Disposition" $dlOk (Describe-Resp $r)

    $r = Invoke-Http DELETE "$gw/api/documents/$docName" -Headers $authH
    Assert-Test "D37 DELETE /api/documents/{name} Admin -> 204" ($r.Status -eq 204) (Describe-Resp $r)

    $r = Invoke-Http GET "$gw/api/documents/download/$docName" -Headers $authH
    Assert-Test "D38 GET /api/documents/download after delete -> 404" ($r.Status -eq 404) (Describe-Resp $r)

    # --- Misc -------------------------------------------------------------
    $staffMeH = @{ Authorization = "Bearer $staffToken" }
    $r = Invoke-Http GET "$gw/api/auth/me" -Headers $staffMeH
    Assert-Test "D39 GET /api/auth/me Staff token -> 200 role Staff" `
        ($r.Status -eq 200 -and (Get-Prop $r.Json "role") -eq "Staff") (Describe-Resp $r)

    # Functions proxy: BaseUrl is http://localhost:7071 so the /api prefix has
    # to travel with the path; accept either spelling as proof the proxy works.
    $r = Invoke-Http GET "$gw/api/functions/api/menu"
    if ($r.Status -ne 200) { $r = Invoke-Http GET "$gw/api/functions/menu" }
    Assert-Test "D40 GET /api/functions/{path} proxies to the Functions host -> 200 array" `
        ($r.Status -eq 200 -and $r.Json -is [System.Array]) (Describe-Resp $r)

    Remove-Item -Path $docPath -ErrorAction SilentlyContinue
    Remove-Item -Path $curlOut -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
$stamp = Get-Date -Format "yyMMddHHmmss"
$orderDate = "2026-10-12"
$orderId = "ORD-E2E-$stamp"
$gwOrderId = "ORD-GW-E2E-$stamp"

$adminEmail = "keyur@coffeenchill.ac.za"
$customerEmail = "jane@student.ac.za"
$staffEmail = "sipho@coffeenchill.ac.za"
$password = "Str0ngPass!"
$adminName = "Keyur Keshav"
$customerName = "Jane Smith"
$staffName = "Sipho Naidoo"
$gwSku1 = "GWE2E1-$stamp"
$gwSku2 = "GWE2E2-$stamp"

Write-Host "=============================================================" -ForegroundColor Yellow
Write-Host "  CoffeeNChill CLDV6212 - FULL END-TO-END TEST SUITE" -ForegroundColor Yellow
Write-Host "  Functions: $FunctionsBaseUrl" -ForegroundColor Yellow
Write-Host "  Gateway:   $GatewayBaseUrl" -ForegroundColor Yellow
Write-Host "=============================================================" -ForegroundColor Yellow

$funcProc = $null
try {
    Start-Azurite
    $funcProc = Start-FunctionsHost

    if (-not (Test-TcpPort 7071)) {
        Write-Result FAIL "INFRA: Functions host is not reachable on port 7071"
    } else {
        Invoke-DocumentPhase
        Invoke-MenuPhase
        Invoke-OrderPhase

        # --- Start the gateway ------------------------------------------
        Show-Phase "Starting API gateway on $GatewayBaseUrl"
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $gwProject = Join-Path $scriptDir "CoffeeNChill.ApiGateway"
        $gwOut = Join-Path $env:TEMP "e2e_gw_out.txt"
        $gwErr = Join-Path $env:TEMP "e2e_gw_err.txt"
        $gwProc = Start-Process -FilePath "dotnet" `
            -ArgumentList @("run", "--project", $gwProject, "--no-launch-profile", "--urls", $GatewayBaseUrl) `
            -WorkingDirectory $scriptDir `
            -RedirectStandardOutput $gwOut -RedirectStandardError $gwErr -NoNewWindow -PassThru

        $gwReady = $false
        $deadline = (Get-Date).AddSeconds(120)
        while ((Get-Date) -lt $deadline) {
            if ($gwProc.HasExited) { break }
            $probe = Invoke-Http GET "$GatewayBaseUrl/swagger" -TimeoutSec 5
            if ($probe.Status -eq 200) { $gwReady = $true; break }
            Start-Sleep -Seconds 2
        }

        if (-not $gwReady) {
            if ($gwProc.HasExited) {
                Write-Host "Gateway exited early. stderr:" -ForegroundColor Red
                if (Test-Path $gwErr) { Get-Content $gwErr -Tail 40 }
                Write-Result FAIL "INFRA: gateway process exited before becoming ready (exit=$($gwProc.ExitCode))"
            } else {
                Write-Result FAIL "INFRA: gateway did not become ready on $GatewayBaseUrl within 120s"
            }
        } else {
            Write-Host "Gateway is ready on $GatewayBaseUrl" -ForegroundColor Cyan
            Invoke-GatewayPhase
        }
    }
} catch {
    Write-Result FAIL "INFRA: unhandled error - $($_.Exception.Message)"
    Write-Host $_ -ForegroundColor Red
} finally {
    if (-not $KeepRunning) {
        if ($gwProc -and -not $gwProc.HasExited) {
            Write-Host "Stopping gateway..." -ForegroundColor Cyan
            Stop-Tree -ProcessId $gwProc.Id
        }
        if ($funcProc -and -not $funcProc.HasExited) {
            Write-Host "Stopping Functions host..." -ForegroundColor Cyan
            Stop-Tree -ProcessId $funcProc.Id
        }
        # Also clear any listener left on 7071 (e.g. a host this script reused).
        Get-NetTCPConnection -LocalPort 7071 -State Listen -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess -Unique |
            ForEach-Object { Stop-Tree -ProcessId $_ }
    } else {
        Write-Host "-KeepRunning specified: Functions host and gateway were left running." -ForegroundColor Cyan
    }
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
$passed = @($results | Where-Object { $_.Status -eq "PASS" }).Count
$failed = @($results | Where-Object { $_.Status -eq "FAIL" }).Count

Write-Host ""
Write-Host "=============================================================" -ForegroundColor Yellow
$summaryColor = if ($failed -eq 0) { "Green" } else { "Red" }
Write-Host ("  FULL E2E SUMMARY: {0} passed, {1} failed, {2} total" -f $passed, $failed, $results.Count) -ForegroundColor $summaryColor
Write-Host "=============================================================" -ForegroundColor Yellow

if ($failed -gt 0) {
    Write-Host "  Failures:" -ForegroundColor Red
    foreach ($f in @($results | Where-Object { $_.Status -eq "FAIL" })) {
        Write-Host "   - $($f.Message)" -ForegroundColor Red
    }
    Write-Host "=============================================================" -ForegroundColor Yellow
    exit 1
}
exit 0
