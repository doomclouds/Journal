param(
  [string]$ReleaseVersion = "0.1.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$distRoot = Join-Path $repoRoot "artifacts/installer/dist"
$setupPath = Join-Path $distRoot "Journal-Setup-$ReleaseVersion.exe"
$checksumPath = Join-Path $distRoot "Journal-Setup-$ReleaseVersion.sha256"

if (-not (Test-Path -LiteralPath $setupPath -PathType Leaf)) {
  throw "Installer setup executable not found: $setupPath"
}

if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
  throw "Installer checksum file not found: $checksumPath"
}

$setupItem = Get-Item -LiteralPath $setupPath
if ($setupItem.Length -le 0) {
  throw "Installer setup executable is empty: $setupPath"
}

$checksumLines = Get-Content -Encoding ascii -LiteralPath $checksumPath
$checksumLine = $checksumLines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($checksumLine)) {
  throw "Installer checksum file is empty: $checksumPath"
}

$expectedHash = ($checksumLine.Trim() -split "\s+")[0]
if ($expectedHash -notmatch "^[a-fA-F0-9]{64}$") {
  throw "Installer checksum file has an invalid SHA256 hash in the first column: $checksumPath"
}

$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $setupPath).Hash
if (-not [string]::Equals($actualHash, $expectedHash, [System.StringComparison]::OrdinalIgnoreCase)) {
  throw "Installer checksum mismatch for $setupPath. Expected $expectedHash but got $actualHash."
}

Write-Host "Installer artifact verified: $setupPath"
