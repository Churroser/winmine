Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'src\WinMine\Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null

function New-MineBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [double]$size * 0.03
    $radius = [double]$size * 0.22
    $rect = New-Object System.Drawing.RectangleF($pad, $pad, ($size - 2 * $pad), ($size - 2 * $pad))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc(($rect.Right - $d), $rect.Y, $d, $d, 270, 90)
    $path.AddArc(($rect.Right - $d), ($rect.Bottom - $d), $d, $d, 0, 90)
    $path.AddArc($rect.X, ($rect.Bottom - $d), $d, $d, 90, 90)
    $path.CloseFigure()

    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 0x2B, 0x88, 0xE0),
        [System.Drawing.Color]::FromArgb(255, 0x0A, 0x4E, 0x9C),
        90.0)
    $g.FillPath($bg, $path)

    $cx = $size / 2.0
    $cy = $size / 2.0
    $bodyR = $size * 0.20
    $barW = $size * 0.075
    $barL = $size * 0.74

    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    for ($i = 0; $i -lt 4; $i++) {
        $g.ResetTransform()
        $g.TranslateTransform($cx, $cy)
        $g.RotateTransform(45 * $i)
        $g.FillRectangle($white, (-$barL / 2), (-$barW / 2), $barL, $barW)
    }
    $g.ResetTransform()
    $g.FillEllipse($white, ($cx - $bodyR), ($cy - $bodyR), ($bodyR * 2), ($bodyR * 2))

    $g.Dispose()
    return $bmp
}

$large = New-MineBitmap 256
$large.Save((Join-Path $assets 'winmine.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$large.Dispose()

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$payloads = @()
foreach ($s in $sizes) {
    $bmp = New-MineBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $payloads += , @{ Size = $s; Bytes = $ms.ToArray() }
    $ms.Dispose()
    $bmp.Dispose()
}

$icoPath = Join-Path $assets 'winmine.ico'
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$payloads.Count)

$offset = 6 + (16 * $payloads.Count)
foreach ($p in $payloads) {
    $dim = if ($p.Size -ge 256) { 0 } else { $p.Size }
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]0)
    $bw.Write([Byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$p.Bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $p.Bytes.Length
}
foreach ($p in $payloads) {
    $bw.Write($p.Bytes)
}
$bw.Flush()
$bw.Dispose()
$fs.Dispose()

Write-Host "icon: $icoPath ($((Get-Item $icoPath).Length) bytes)"
Write-Host "png : $(Join-Path $assets 'winmine.png')"
