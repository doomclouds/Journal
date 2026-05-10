param(
    [switch]$RestartApi,
    [switch]$RestartVite,
    [switch]$NoElectron,
    [switch]$OpenBrowser,
    [switch]$ShowLogs
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiUrl = "http://localhost:5057/health"
$viteUrl = "http://127.0.0.1:5173"
$logRoot = Join-Path $env:TEMP "journal-dev"
$apiLog = Join-Path $logRoot "journal-api.log"
$viteLog = Join-Path $logRoot "journal-vite.log"
$electronLog = Join-Path $logRoot "journal-electron.log"

New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

function Quote-PSPath {
    param([Parameter(Mandatory = $true)][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function Test-Endpoint {
    param([Parameter(Mandatory = $true)][string]$Url)

    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 2
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 500
    }
    catch {
        return $false
    }
}

function Wait-Endpoint {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [int]$TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Endpoint -Url $Url) {
            Write-Host "$Name is ready: $Url"
            return
        }

        Start-Sleep -Milliseconds 700
    }

    Write-Host "$Name did not become ready. Log: $LogPath"
    if (Test-Path -LiteralPath $LogPath) {
        Get-Content -LiteralPath $LogPath -Tail 80
    }

    throw "$Name startup timed out."
}

function Stop-RepoProcess {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$PathLike
    )

    Get-Process -Name $Name -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($PathLike, [System.StringComparison]::OrdinalIgnoreCase) } |
        ForEach-Object {
            Write-Host "Stopping $Name PID $($_.Id)"
            Stop-Process -Id $_.Id -Force
        }
}

function Stop-PortOwner {
    param([Parameter(Mandatory = $true)][int]$Port)

    $owners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique

    foreach ($owner in $owners) {
        $process = Get-Process -Id $owner -ErrorAction SilentlyContinue
        if ($process) {
            Write-Host "Stopping process on port ${Port}: $($process.ProcessName) PID $($process.Id)"
            Stop-Process -Id $process.Id -Force
        }
    }
}

function Start-BackgroundCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Command,
        [Parameter(Mandatory = $true)][string]$LogPath
    )

    $repoLiteral = Quote-PSPath -Value $repoRoot
    $logLiteral = Quote-PSPath -Value $LogPath
    $fullCommand = "Set-Location -LiteralPath $repoLiteral; $Command *> $logLiteral"
    $process = Start-Process -FilePath "powershell.exe" `
        -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $fullCommand) `
        -WindowStyle Hidden `
        -PassThru

    Write-Host "Started $Name launcher PID $($process.Id). Log: $LogPath"
}

if ($RestartApi) {
    Stop-RepoProcess -Name "Journal.Api" -PathLike $repoRoot
    Stop-PortOwner -Port 5057
    Start-Sleep -Milliseconds 500
}

if ($RestartVite) {
    Stop-PortOwner -Port 5173
    Start-Sleep -Milliseconds 500
}

if (Test-Endpoint -Url $apiUrl) {
    Write-Host "API already running: $apiUrl"
}
else {
    Start-BackgroundCommand -Name "API" -Command "dotnet run --project src/Journal.Api" -LogPath $apiLog
    Wait-Endpoint -Url $apiUrl -Name "API" -LogPath $apiLog
}

if (Test-Endpoint -Url $viteUrl) {
    Write-Host "Vite already running: $viteUrl"
}
else {
    Start-BackgroundCommand -Name "Vite" -Command "npm run dev --prefix apps/desktop" -LogPath $viteLog
    Wait-Endpoint -Url $viteUrl -Name "Vite" -LogPath $viteLog
}

if ($OpenBrowser) {
    Start-Process $viteUrl
}

if (-not $NoElectron) {
    Start-BackgroundCommand -Name "Electron" -Command "npm run electron --prefix apps/desktop" -LogPath $electronLog
}

Write-Host ""
Write-Host "Journal dev environment is ready."
Write-Host "API:      $apiUrl"
Write-Host "Frontend: $viteUrl"
Write-Host "Logs:"
Write-Host "  API:      $apiLog"
Write-Host "  Vite:     $viteLog"
Write-Host "  Electron: $electronLog"
Write-Host ""
Write-Host "Stop later with: .\scripts\stop-journal-dev.ps1"

if ($ShowLogs) {
    foreach ($log in @($apiLog, $viteLog, $electronLog)) {
        if (Test-Path -LiteralPath $log) {
            Write-Host ""
            Write-Host "== $log =="
            Get-Content -LiteralPath $log -Tail 50
        }
    }
}
