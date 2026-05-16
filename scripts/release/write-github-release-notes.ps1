param(
  [Parameter(Mandatory = $true)]
  [string]$ReleaseVersion,
  [string]$PreviousTag = "",
  [string]$OutputPath = "artifacts/installer/release-assets/GITHUB_RELEASE_NOTES.md"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$outputFullPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
  $OutputPath
} else {
  Join-Path $repoRoot $OutputPath
}

function Invoke-Git {
  param([string[]]$Arguments)

  $output = & git -C $repoRoot @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
  }

  return $output
}

function Get-VersionFromTag {
  param([string]$TagName)

  if ($TagName -notmatch "^v(?<version>\d+\.\d+\.\d+)$") {
    return $null
  }

  return [version]$Matches["version"]
}

function Resolve-PreviousTag {
  param([version]$CurrentVersion)

  $tags = Invoke-Git @("tag", "--list", "v[0-9]*")
  $versionedTags = @()
  foreach ($tag in $tags) {
    $parsedVersion = Get-VersionFromTag $tag
    if ($null -ne $parsedVersion -and $parsedVersion -lt $CurrentVersion) {
      $versionedTags += [pscustomobject]@{
        Tag = $tag
        Version = $parsedVersion
      }
    }
  }

  return $versionedTags |
    Sort-Object -Property Version -Descending |
    Select-Object -First 1 -ExpandProperty Tag
}

function Format-CommitLine {
  param([string]$Line)

  $parts = $Line -split "`t", 2
  if ($parts.Count -lt 2) {
    return $Line
  }

  $hash = $parts[0]
  $subject = $parts[1]
  $displaySubject = $subject -replace "^(feat|fix|docs|test|ci|chore|refactor|style|perf)(\([^)]+\))?:\s*", ""
  return "- $displaySubject ($hash)"
}

if ($ReleaseVersion -notmatch "^\d+\.\d+\.\d+$") {
  throw "ReleaseVersion must use semantic version format, e.g. 0.1.1."
}

$releaseVersionValue = [version]$ReleaseVersion
if ([string]::IsNullOrWhiteSpace($PreviousTag)) {
  $PreviousTag = Resolve-PreviousTag $releaseVersionValue
}

$rangeLabel = if ([string]::IsNullOrWhiteSpace($PreviousTag)) {
  "repository history"
} else {
  $PreviousTag
}
$gitRange = if ([string]::IsNullOrWhiteSpace($PreviousTag)) {
  "HEAD"
} else {
  "$PreviousTag..HEAD"
}

$commitLines = Invoke-Git @("log", "--pretty=format:%h`t%s", $gitRange)
$formattedCommits = @()
foreach ($line in $commitLines) {
  if (-not [string]::IsNullOrWhiteSpace($line)) {
    $formattedCommits += Format-CommitLine $line
  }
}

if ($formattedCommits.Count -eq 0) {
  $formattedCommits += "- No source changes were detected in this release range."
}

$curatedNotesPath = Join-Path $repoRoot "docs/release/notes/v$ReleaseVersion.md"
$curatedNotes = ""
if (Test-Path -LiteralPath $curatedNotesPath -PathType Leaf) {
  $curatedNotes = [System.IO.File]::ReadAllText($curatedNotesPath, [System.Text.Encoding]::UTF8).Trim()
}

$curatedBlock = if ([string]::IsNullOrWhiteSpace($curatedNotes)) {
  @()
} else {
  @($curatedNotes, "")
}

$body = @(
  "# Journal v$ReleaseVersion",
  "",
  "## Assets",
  "",
  "- ``Journal-Setup-$ReleaseVersion.exe``",
  "- ``Journal-Setup-$ReleaseVersion.sha256``",
  ""
) + $curatedBlock + @(
  "## Changes Since $rangeLabel",
  "",
  ($formattedCommits -join [Environment]::NewLine),
  "",
  "## Install",
  "",
  "Download the setup executable for this release and run it on Windows x64.",
  "",
  "## Data Safety",
  "",
  "The installer preserves ``%LocalAppData%/Journal`` during upgrade and uninstall. User journal data is not treated as disposable installer output. Export packages do not include full API keys by default.",
  "",
  "## Verification",
  "",
  "After downloading both assets, compare the SHA-256 hash of the setup executable with the value in the matching ``.sha256`` file."
) -join [Environment]::NewLine

$outputDirectory = Split-Path -Parent $outputFullPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
  New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

[System.IO.File]::WriteAllText($outputFullPath, $body + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote GitHub release notes to $outputFullPath"
