param(
  [string]$ReleaseVersion = "0.1.0",
  [string]$OutputPath = "artifacts/installer/build-metadata.env"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path

function Resolve-RepoPath {
  param([string]$PathValue)

  if ([System.IO.Path]::IsPathRooted($PathValue)) {
    return $PathValue
  }

  return (Join-Path $repoRoot $PathValue)
}

$resolvedOutputPath = Resolve-RepoPath $OutputPath
$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
  New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$commitOutput = & git -C $repoRoot rev-parse --short HEAD
if ($LASTEXITCODE -ne 0) {
  throw "git rev-parse failed with exit code $LASTEXITCODE."
}
$commit = ($commitOutput | Select-Object -First 1).Trim()
$buildTime = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

$metadata = @(
  "JOURNAL_RELEASE_VERSION=$ReleaseVersion"
  "JOURNAL_FRONTEND_VERSION=$ReleaseVersion"
  "JOURNAL_BUILD_COMMIT=$commit"
  "JOURNAL_BUILD_TIME_UTC=$buildTime"
  "VITE_JOURNAL_FRONTEND_VERSION=$ReleaseVersion"
  "VITE_JOURNAL_RELEASE_VERSION=$ReleaseVersion"
  "VITE_JOURNAL_COMMIT=$commit"
  "VITE_JOURNAL_BUILD_TIME_UTC=$buildTime"
) -join [Environment]::NewLine

[System.IO.File]::WriteAllText(
  $resolvedOutputPath,
  $metadata + [Environment]::NewLine,
  [System.Text.UTF8Encoding]::new($false))

Write-Host "Wrote build metadata to $resolvedOutputPath"
