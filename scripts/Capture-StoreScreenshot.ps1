[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$ProcessId,
    [Parameter(Mandatory)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (!('YeobaekStoreCapture' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class YeobaekStoreCapture
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after,
        int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
}
'@
}
$process = Get-Process -Id $ProcessId
if ($process.ProcessName -ne 'Yeobaek' -or $process.MainWindowHandle -eq [IntPtr]::Zero) {
    throw '실행 중인 여백 창을 찾을 수 없다.'
}
$window = $process.MainWindowHandle
$before = [YeobaekStoreCapture+Rect]::new()
if (![YeobaekStoreCapture]::GetWindowRect($window, [ref]$before)) { throw '창 위치를 읽지 못했다.' }
if (![YeobaekStoreCapture]::SetWindowPos($window, [IntPtr]::Zero, 0, 0, 1600, 1000, 0x16)) {
    throw '화면 촬영용 창 크기를 설정하지 못했다.'
}
try {
    Start-Sleep -Milliseconds 800
    $rect = [YeobaekStoreCapture+Rect]::new()
    if (![YeobaekStoreCapture]::GetWindowRect($window, [ref]$rect)) { throw '촬영 크기를 읽지 못했다.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -lt 1366 -or $height -lt 768) { throw 'Store 최소 화면 이미지 크기에 못 미친다.' }
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $dc = $graphics.GetHdc()
    try {
        if (![YeobaekStoreCapture]::PrintWindow($window, $dc, 2)) { throw '앱 화면을 촬영하지 못했다.' }
    }
    finally { $graphics.ReleaseHdc($dc); $graphics.Dispose() }
    try {
        $target = [System.IO.Path]::GetFullPath($OutputPath)
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        $bitmap.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "실제 앱 화면 저장: $target ($width x $height)"
    }
    finally { $bitmap.Dispose() }
}
finally {
    [void][YeobaekStoreCapture]::SetWindowPos($window, [IntPtr]::Zero, $before.Left, $before.Top,
        ($before.Right - $before.Left), ($before.Bottom - $before.Top), 0x14)
}
