param(
    [switch]$Api,
    [switch]$Vite,
    [switch]$Electron
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$stopAll = -not ($Api -or $Vite -or $Electron)

function Stop-ProcessIfRunning {
    param(
        [Parameter(Mandatory = $true)][int]$ProcessId,
        [Parameter(Mandatory = $true)][string]$Reason
    )

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if (-not $process) {
        return
    }

    Write-Host "Stopping $($process.ProcessName) PID $ProcessId ($Reason)"
    Stop-Process -Id $ProcessId -Force
}

function Stop-RepoProcess {
    param([Parameter(Mandatory = $true)][string]$Name)

    Get-Process -Name $Name -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase) } |
        ForEach-Object { Stop-ProcessIfRunning -ProcessId $_.Id -Reason "repo process" }
}

function Stop-PortOwner {
    param([Parameter(Mandatory = $true)][int]$Port)

    $owners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique

    foreach ($owner in $owners) {
        Stop-ProcessIfRunning -ProcessId $owner -Reason "port $Port owner"
    }
}

function Stop-RepoLauncher {
    param([Parameter(Mandatory = $true)][string[]]$Needles)

    Get-CimInstance Win32_Process |
        Where-Object {
            $commandLine = $_.CommandLine
            if (-not [string]::IsNullOrWhiteSpace($commandLine)) {
                $isRepoCommand = $commandLine.IndexOf($repoRoot, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
                $matchesNeedle = $Needles | Where-Object {
                    $commandLine.IndexOf($_, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
                }

                $isRepoCommand -and $matchesNeedle
            }
        } |
        ForEach-Object { Stop-ProcessIfRunning -ProcessId $_.ProcessId -Reason "repo launcher" }
}

if ($stopAll -or $Api) {
    Stop-RepoProcess -Name "Journal.Api"
    Stop-PortOwner -Port 5057
    Stop-RepoLauncher -Needles @("dotnet run --project src/Journal.Api")
}

if ($stopAll -or $Vite) {
    Stop-PortOwner -Port 5173
    Stop-RepoLauncher -Needles @("npm run dev --prefix apps/desktop")
}

if ($stopAll -or $Electron) {
    Stop-RepoProcess -Name "electron"
    Stop-RepoLauncher -Needles @("npm run electron --prefix apps/desktop", "npm run desktop --prefix apps/desktop")
}

Write-Host "Journal dev processes stopped."
