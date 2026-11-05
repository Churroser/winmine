param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$OutFile,
    [int]$WaitMs = 4000,
    [int]$SettleMs = 900
)

Add-Type -AssemblyName System.Drawing

$src = @'
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

public static class WinShot2
{
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public static Rectangle Bounds(IntPtr hWnd)
    {
        RECT r;
        if (DwmGetWindowAttribute(hWnd, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0)
            GetWindowRect(hWnd, out r);
        return new Rectangle(r.Left, r.Top, Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top));
    }

    public static Bitmap CaptureScreen(Rectangle r)
    {
        var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(r.Width, r.Height), CopyPixelOperation.SourceCopy);
        }
        return bmp;
    }
}
'@
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing

[void][WinShot2]::SetProcessDPIAware()

$proc = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Milliseconds $WaitMs

$hwnd = $proc.MainWindowHandle
for ($i = 0; $i -lt 25 -and $hwnd -eq [IntPtr]::Zero; $i++) {
    Start-Sleep -Milliseconds 300
    $proc.Refresh()
    if ($proc.HasExited) { Write-Host "PROCESS EXITED code=$($proc.ExitCode)"; exit 2 }
    $hwnd = $proc.MainWindowHandle
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Host "NO WINDOW HANDLE"; exit 3 }

[void][WinShot2]::ShowWindow($hwnd, 5)
[void][WinShot2]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds $SettleMs

$rect = [WinShot2]::Bounds($hwnd)
$bmp = [WinShot2]::CaptureScreen($rect)

$dir = Split-Path -Parent $OutFile
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)

# report a crude brightness spread so we can tell a real render from a blank frame
$min = 255; $max = 0
for ($x = 0; $x -lt $bmp.Width; $x += 17) {
    for ($y = 0; $y -lt $bmp.Height; $y += 17) {
        $c = $bmp.GetPixel($x, $y); $l = [int](($c.R + $c.G + $c.B) / 3)
        if ($l -lt $min) { $min = $l }
        if ($l -gt $max) { $max = $l }
    }
}
Write-Host "CAPTURED: $OutFile $($bmp.Width)x$($bmp.Height) pid=$($proc.Id) luminance=[$min..$max]"
$bmp.Dispose()
