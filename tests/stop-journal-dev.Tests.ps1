$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot "..\scripts\stop-journal-dev.ps1"
$content = Get-Content -LiteralPath $scriptPath -Raw -Encoding utf8

if ($content -notmatch 'catch \[Microsoft\.PowerShell\.Commands\.ProcessCommandException\]') {
    throw "stop-journal-dev.ps1 must catch ProcessCommandException for PID-exit races."
}

if ($content -notmatch '\$stillRunning = Get-Process -Id \$ProcessId -ErrorAction SilentlyContinue') {
    throw "stop-journal-dev.ps1 must re-check the PID before swallowing Stop-Process failures."
}

if ($content -notmatch 'if \(\$stillRunning\) \{\s*throw\s*\}') {
    throw "stop-journal-dev.ps1 must rethrow Stop-Process failures when the process still exists."
}

if ($content -notmatch 'Process PID \$ProcessId already exited\.') {
    throw "stop-journal-dev.ps1 must keep the benign race message for already-exited processes."
}

Write-Host "stop-journal-dev.ps1 error handling contract passed."
