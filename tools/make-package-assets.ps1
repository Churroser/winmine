# Generates the PNG assets required by AppxManifest.xml from the 256x256 app icon.
# ASCII only on purpose: Windows PowerShell 5.1 parses BOM-less scripts as ANSI.
param(
    [string]$Source = "src\WinMine\Assets\winmine.png",
    [string]$OutDir = "packaging\Assets"
)

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$srcPath = Join-Path $root $Source
$outPath = Join-Path $root $OutDir
if (-not (Test-Path $srcPath)) { throw "icon not found: $srcPath" }
New-Item -ItemType Directory -Force -Path $outPath | Out-Null

$icon = [System.Drawing.Image]::FromFile($srcPath)

function New-Canvas([int]$width, [int]$height) {
    $bmp = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    return @($bmp, $g)
}

function Save-Square([string]$name, [int]$size) {
    $pair = New-Canvas $size $size
    $bmp = $pair[0]; $g = $pair[1]
    $g.DrawImage($icon, 0, 0, $size, $size)
    $g.Dispose()
    $target = Join-Path $outPath $name
    $bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ("{0,-26} {1}x{1}" -f $name, $size)
}

function Save-Tile([string]$name, [int]$width, [int]$height, [double]$scale) {
    $pair = New-Canvas $width $height
    $bmp = $pair[0]; $g = $pair[1]
    $side = [int][Math]::Round([Math]::Min($width, $height) * $scale)
    $x = [int](($width - $side) / 2)
    $y = [int](($height - $side) / 2)
    $g.DrawImage($icon, $x, $y, $side, $side)
    $g.Dispose()
    $target = Join-Path $outPath $name
    $bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ("{0,-26} {1}x{2}" -f $name, $width, $height)
}

Save-Square "StoreLogo.png" 50
Save-Square "Square44x44Logo.png" 44
Save-Square "Square150x150Logo.png" 150
Save-Square "LockScreenLogo.png" 24
Save-Tile "Wide310x150Logo.png" 310 150 0.72
Save-Tile "SplashScreen.png" 620 300 0.9

$icon.Dispose()
Write-Host ("assets written to {0}" -f $outPath)
