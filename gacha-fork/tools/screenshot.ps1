# Capture a screenshot of a process' main window (verification helper for the GachaOnly build).
#
#   .\screenshot.ps1 -ProcessName Starward.GachaOnly -Out shot.png
#
# Uses PrintWindow(PW_RENDERFULLCONTENT) so the capture also works while another window (a game,
# an IDE) is in the foreground — CopyFromScreen would grab whatever is on top instead.
# Falls back to a straight screen capture if PrintWindow returns a blank image.
#
# Note: this machine's execution policy refuses to run .ps1 files; invoke it from a session where
# scripts are allowed, or paste the body inline.

param(
    [Parameter(Mandatory = $true)][string] $ProcessName,
    [Parameter(Mandatory = $true)][string] $Out
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Win32Window {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

$proc = Get-Process -Name $ProcessName -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw "no window found for process $ProcessName" }
$h = $proc.MainWindowHandle

$rect = New-Object Win32Window+RECT
[Win32Window]::GetWindowRect($h, [ref]$rect) | Out-Null
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -le 0 -or $height -le 0) {
    [Win32Window]::ShowWindow($h, 9) | Out-Null     # SW_RESTORE
    Start-Sleep -Milliseconds 800
    [Win32Window]::GetWindowRect($h, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
}
if ($width -le 0 -or $height -le 0) { throw "bad window rect" }

function Save-Window($mode) {
    $bmp = New-Object System.Drawing.Bitmap($width, $height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    if ($mode -eq 'print') {
        $hdc = $g.GetHdc()
        [Win32Window]::PrintWindow($h, $hdc, 2) | Out-Null
        $g.ReleaseHdc($hdc)
    }
    else {
        $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    }
    $g.Dispose()
    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

Save-Window 'print'
# blank check (PrintWindow can return an empty surface for some composition setups)
$probe = New-Object System.Drawing.Bitmap($Out)
$seen = @{}
for ($x = 5; $x -lt $probe.Width; $x += 25) { for ($y = 5; $y -lt $probe.Height; $y += 25) { $seen[$probe.GetPixel($x, $y).ToArgb()] = 1 } }
$probe.Dispose()
if ($seen.Count -le 2) {
    Write-Warning 'PrintWindow looks blank, falling back to a screen capture (window must be on top)'
    Save-Window 'screen'
}

Write-Host "captured $width x $height -> $Out (distinct colors: $($seen.Count))"
