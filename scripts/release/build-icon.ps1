param(
  [string]$Source = "assets/app-icon/journal-icon-source.png",
  [string]$OutputDirectory = "assets/app-icon"
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

function New-IconPngBytes {
  param(
    [System.Drawing.Image]$SourceImage,
    [int]$Size
  )

  $bitmap = $null
  $graphics = $null
  $stream = $null

  try {
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.DrawImage($SourceImage, 0, 0, $Size, $Size)

    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    return ,$stream.ToArray()
  }
  finally {
    if ($stream) { $stream.Dispose() }
    if ($graphics) { $graphics.Dispose() }
    if ($bitmap) { $bitmap.Dispose() }
  }
}

Add-Type -AssemblyName System.Drawing

$sourcePath = Resolve-RepoPath $Source
$outputPath = Resolve-RepoPath $OutputDirectory

if (-not (Test-Path -LiteralPath $sourcePath)) {
  throw "Source image not found: $sourcePath"
}

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$entries = New-Object System.Collections.Generic.List[object]
$sourceImage = $null

try {
  $sourceImage = [System.Drawing.Image]::FromFile($sourcePath)

  foreach ($size in $sizes) {
    [byte[]]$pngBytes = New-IconPngBytes -SourceImage $sourceImage -Size $size
    $pngPath = Join-Path $outputPath "journal-icon-$size.png"
    [System.IO.File]::WriteAllBytes($pngPath, $pngBytes)
    $entries.Add([pscustomobject]@{
      Size = $size
      Bytes = $pngBytes
    })
  }
}
finally {
  if ($sourceImage) { $sourceImage.Dispose() }
}

$icoPath = Join-Path $outputPath "journal.ico"
$fileStream = $null
$writer = $null

try {
  $fileStream = [System.IO.FileStream]::new($icoPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
  $writer = [System.IO.BinaryWriter]::new($fileStream)

  $writer.Write([UInt16]0)
  $writer.Write([UInt16]1)
  $writer.Write([UInt16]$entries.Count)

  $imageOffset = 6 + (16 * $entries.Count)

  foreach ($entry in $entries) {
    $dimension = if ($entry.Size -eq 256) { 0 } else { $entry.Size }

    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$entry.Bytes.Length)
    $writer.Write([UInt32]$imageOffset)

    $imageOffset += $entry.Bytes.Length
  }

  foreach ($entry in $entries) {
    $writer.Write($entry.Bytes)
  }
}
finally {
  if ($writer) { $writer.Dispose() }
  if ($fileStream) { $fileStream.Dispose() }
}

Write-Host "Wrote icon assets to $outputPath"
