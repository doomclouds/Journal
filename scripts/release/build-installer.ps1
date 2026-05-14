param(
  [string]$ReleaseVersion = "0.1.0",
  [switch]$SkipInno
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$metadataScript = Join-Path $PSScriptRoot "write-build-metadata.ps1"
$stageScript = Join-Path $PSScriptRoot "stage-installer.ps1"

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

& $metadataScript -ReleaseVersion $ReleaseVersion
& $stageScript -ReleaseVersion $ReleaseVersion

if ($SkipInno) {
  Write-Host "Skipping Inno Setup compile because -SkipInno was specified."
  return
}

$programFilesX86 = ${env:ProgramFiles(x86)}
if ([string]::IsNullOrWhiteSpace($programFilesX86)) {
  throw "ProgramFiles(x86) environment variable is not set; cannot locate Inno Setup compiler."
}

$innoCompiler = Join-Path $programFilesX86 "Inno Setup 6/ISCC.exe"
$innoScript = Join-Path $repoRoot "installer/windows/Journal.iss"

if (-not (Test-Path -LiteralPath $innoCompiler -PathType Leaf)) {
  throw "Inno Setup compiler not found: $innoCompiler"
}

if (-not (Test-Path -LiteralPath $innoScript -PathType Leaf)) {
  throw "Inno Setup script not found: $innoScript"
}

Invoke-NativeCommand -Name "Inno Setup compiler" -Command {
  & $innoCompiler $innoScript "/DAppVersion=$ReleaseVersion"
}
