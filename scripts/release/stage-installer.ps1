param(
  [string]$ReleaseVersion = "0.1.0",
  [string]$PublishRoot = "artifacts/installer/publish/Journal"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$metadataPath = Join-Path $repoRoot "artifacts/installer/build-metadata.env"
$publishBasePath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts/installer/publish"))

function Resolve-RepoPath {
  param([string]$PathValue)

  if ([System.IO.Path]::IsPathRooted($PathValue)) {
    return $PathValue
  }

  return (Join-Path $repoRoot $PathValue)
}

function Invoke-NativeCommand {
  param(
    [string]$Name,
    [scriptblock]$Command
  )

  & $Command
  if ($LASTEXITCODE -ne 0) {
    throw "$Name failed with exit code $LASTEXITCODE."
  }
}

function Get-CurrentCommit {
  $commitOutput = & git -C $repoRoot rev-parse --short HEAD
  if ($LASTEXITCODE -ne 0) {
    throw "git rev-parse failed with exit code $LASTEXITCODE."
  }

  return ($commitOutput | Select-Object -First 1).Trim()
}

function Import-BuildMetadata {
  param([string]$PathValue)

  $metadata = @{}
  foreach ($line in Get-Content -Encoding utf8 -LiteralPath $PathValue) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith("#")) {
      continue
    }

    $separatorIndex = $line.IndexOf("=")
    if ($separatorIndex -le 0) {
      throw "Invalid build metadata line in ${PathValue}: $line"
    }

    $name = $line.Substring(0, $separatorIndex)
    $value = $line.Substring($separatorIndex + 1)
    $metadata[$name] = $value
  }

  foreach ($name in $metadata.Keys) {
    [Environment]::SetEnvironmentVariable($name, $metadata[$name], "Process")
  }

  return $metadata
}

function Copy-RequiredFile {
  param(
    [string]$Source,
    [string]$Destination
  )

  $resolvedSource = Resolve-RepoPath $Source
  if (-not (Test-Path -LiteralPath $resolvedSource -PathType Leaf)) {
    throw "Required release input not found: $resolvedSource"
  }

  Copy-Item -LiteralPath $resolvedSource -Destination $Destination -Force
}

function Assert-PublishRootIsSafe {
  param([string]$PathValue)

  $fullPath = [System.IO.Path]::GetFullPath($PathValue)
  $baseWithSeparator = $publishBasePath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
  if (-not $fullPath.StartsWith($baseWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "PublishRoot must be inside $publishBasePath. Refusing to clean: $fullPath"
  }
}

function Invoke-WithTemporaryPackageVersion {
  param(
    [string]$PackageJsonPath,
    [scriptblock]$Command
  )

  $originalPackageJson = [System.IO.File]::ReadAllText($PackageJsonPath, [System.Text.Encoding]::UTF8)
  try {
    $package = $originalPackageJson | ConvertFrom-Json
    $package.version = $ReleaseVersion
    $temporaryPackageJson = ($package | ConvertTo-Json -Depth 20) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($PackageJsonPath, $temporaryPackageJson, [System.Text.UTF8Encoding]::new($false))
    & $Command
  }
  finally {
    [System.IO.File]::WriteAllText($PackageJsonPath, $originalPackageJson, [System.Text.UTF8Encoding]::new($false))
  }
}

function Assert-StagedInstallerInputs {
  param([hashtable]$Metadata)

  $requiredPaths = @(
    (Join-Path $backendPath "Journal.Api.exe"),
    (Join-Path $appPath "Journal.exe"),
    (Join-Path $appPath "resources/app.asar"),
    (Join-Path $appPath "resources/build-metadata.env"),
    (Join-Path $legalPath "LICENSE"),
    (Join-Path $legalPath "NOTICE"),
    (Join-Path $legalPath "PRIVACY.md"),
    (Join-Path $legalPath "DATA_SAFETY.md"),
    (Join-Path $legalPath "AI_NOTICE.md"),
    (Join-Path $assetsPath "journal.ico")
  )

  foreach ($pathValue in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $pathValue -PathType Leaf)) {
      throw "Missing staged installer input: $pathValue"
    }
  }

  $currentCommit = Get-CurrentCommit
  if ($Metadata["JOURNAL_BUILD_COMMIT"] -ne $currentCommit) {
    throw "Build metadata commit $($Metadata["JOURNAL_BUILD_COMMIT"]) does not match current HEAD $currentCommit."
  }

  $appVersion = (Get-Item -LiteralPath (Join-Path $appPath "Journal.exe")).VersionInfo.ProductVersion
  if ($appVersion -ne $ReleaseVersion) {
    throw "Packaged Electron app version $appVersion does not match release version $ReleaseVersion."
  }
}

$resolvedPublishRoot = Resolve-RepoPath $PublishRoot
$resolvedPublishRoot = [System.IO.Path]::GetFullPath($resolvedPublishRoot)
$backendPath = Join-Path $resolvedPublishRoot "backend"
$appPath = Join-Path $resolvedPublishRoot "app"
$legalPath = Join-Path $resolvedPublishRoot "legal"
$assetsPath = Join-Path $resolvedPublishRoot "assets"
$electronPackagePath = Join-Path $repoRoot "artifacts/installer/electron/Journal-win32-x64"
$packageJsonPath = Join-Path $repoRoot "apps/desktop/package.json"

Assert-PublishRootIsSafe -PathValue $resolvedPublishRoot
& (Join-Path $PSScriptRoot "write-build-metadata.ps1") -ReleaseVersion $ReleaseVersion -OutputPath $metadataPath
$metadata = Import-BuildMetadata -PathValue $metadataPath

if (Test-Path -LiteralPath $resolvedPublishRoot) {
  Remove-Item -LiteralPath $resolvedPublishRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $backendPath, $appPath, $legalPath, $assetsPath | Out-Null

Push-Location $repoRoot
try {
  Invoke-NativeCommand -Name "dotnet publish" -Command {
    dotnet publish src/Journal.Api/Journal.Api.csproj -c Release -r win-x64 --self-contained true -o $backendPath
  }
  Invoke-NativeCommand -Name "npm run build" -Command {
    npm run build --prefix apps/desktop
  }
  Invoke-WithTemporaryPackageVersion -PackageJsonPath $packageJsonPath -Command {
    Invoke-NativeCommand -Name "npm run package:win" -Command {
      npm run package:win --prefix apps/desktop
    }
  }
}
finally {
  Pop-Location
}

if (-not (Test-Path -LiteralPath $electronPackagePath -PathType Container)) {
  throw "Electron package output not found: $electronPackagePath"
}

Get-ChildItem -LiteralPath $electronPackagePath | Copy-Item -Destination $appPath -Recurse -Force
Copy-Item -LiteralPath $metadataPath -Destination (Join-Path $appPath "resources/build-metadata.env") -Force

Copy-RequiredFile -Source "LICENSE" -Destination (Join-Path $legalPath "LICENSE")
Copy-RequiredFile -Source "NOTICE" -Destination (Join-Path $legalPath "NOTICE")
Copy-RequiredFile -Source "docs/legal/PRIVACY.md" -Destination (Join-Path $legalPath "PRIVACY.md")
Copy-RequiredFile -Source "docs/legal/DATA_SAFETY.md" -Destination (Join-Path $legalPath "DATA_SAFETY.md")
Copy-RequiredFile -Source "docs/legal/AI_NOTICE.md" -Destination (Join-Path $legalPath "AI_NOTICE.md")
Copy-RequiredFile -Source "assets/app-icon/journal.ico" -Destination (Join-Path $assetsPath "journal.ico")

Assert-StagedInstallerInputs -Metadata $metadata

Write-Host "Staged installer inputs to $resolvedPublishRoot"
