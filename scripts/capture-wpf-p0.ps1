# capture-wpf-p0.ps1
#
# Best-effort helper for the deterministic part of the WPF P0 capture set.
# It writes presets, starts the WPF build, hovers the island and captures the
# fixed 760x460 DIP canvas region. Settings pages, tray, media playback and all
# P1 flows stay manual; see docs/plans/2026-11-18-wpf-capture-runbook.md.
#
# This script has not been executed on a Windows host yet. The first run is a
# smoke test: check every output against the runbook tables before trusting it.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\capture-wpf-p0.ps1 -DryRun
#   powershell -ExecutionPolicy Bypass -File scripts\capture-wpf-p0.ps1
#   powershell -ExecutionPolicy Bypass -File scripts\capture-wpf-p0.ps1 -MediaState playing -Videos
#
# Params:
#   -OutDir     capture set root; defaults to docs\results\phase4-reference\wpf
#   -ExePath    WPF binary; defaults to the Release build under src\DynamicIsland
#   -Wallpaper  dark|light, used in the folder name
#   -MediaState playing|paused|empty; names the output, does not control playback
#   -Videos     also record the hover video with ffmpeg when available
#   -SkipLaunch do not stop or start Aster; capture the running instance
#   -DryRun     print paths and geometry, write no settings

[CmdletBinding()]
param(
    [string]$OutDir = "",
    [string]$ExePath = "",
    [ValidateSet("dark", "light")]
    [string]$Wallpaper = "dark",
    [ValidateSet("playing", "paused", "empty")]
    [string]$MediaState = "empty",
    [switch]$Videos,
    [switch]$SkipLaunch,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "This helper runs on Windows only."
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $scriptDir

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $root "docs\results\phase4-reference\wpf"
}
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $candidates = @(
        (Join-Path $root "src\DynamicIsland\bin\Release\net8.0-windows10.0.19041.0\Aster.exe"),
        (Join-Path $root "src\DynamicIsland\bin\Release\net8.0-windows10.0.19041.0\win-x64\Aster.exe"),
        (Join-Path $root "dist\Aster.exe")
    )
    $ExePath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $ExePath) {
        throw "Aster.exe not found. Build src\DynamicIsland first, or pass -ExePath."
    }
}
if (-not (Test-Path $ExePath)) {
    throw "Aster.exe not found at $ExePath"
}

# --- win32 ---------------------------------------------------------------

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class WpfCaptureNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    // className is IntPtr: PowerShell binds $null to "" for string parameters,
    // which silently matches no window. IntPtr.Zero is the real NULL.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(IntPtr className, string windowName);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    public static bool TryGetWindowBox(IntPtr hWnd, out int x, out int y, out int width, out int height)
    {
        RECT r;
        x = 0; y = 0; width = 0; height = 0;
        if (!GetWindowRect(hWnd, out r)) { return false; }
        x = r.Left; y = r.Top;
        width = r.Right - r.Left;
        height = r.Bottom - r.Top;
        return true;
    }
}
"@

try {
    # -4 = DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
    [void][WpfCaptureNative]::SetProcessDpiAwarenessContext([IntPtr](-4))
} catch {
    Write-Warning "Could not enable per-monitor DPI awareness; geometry may be off."
}

function Get-AsterWindow {
    param([int]$ProcessId)
    $handle = [WpfCaptureNative]::FindWindow([IntPtr]::Zero, "Aster")
    if ($handle -eq [IntPtr]::Zero) { return [IntPtr]::Zero }
    [uint32]$windowPid = 0
    [void][WpfCaptureNative]::GetWindowThreadProcessId($handle, [ref]$windowPid)
    if ($windowPid -ne $ProcessId) { return [IntPtr]::Zero }
    return $handle
}

function Wait-AsterWindow {
    param([int]$ProcessId, [int]$Tries = 40)
    for ($i = 0; $i -lt $Tries; $i++) {
        Start-Sleep -Milliseconds 250
        $handle = Get-AsterWindow -ProcessId $ProcessId
        if ($handle -ne [IntPtr]::Zero) { return $handle }
    }
    return [IntPtr]::Zero
}

function Get-WindowBox {
    param([IntPtr]$Handle)
    [int]$x = 0; [int]$y = 0; [int]$w = 0; [int]$h = 0
    if (-not [WpfCaptureNative]::TryGetWindowBox($Handle, [ref]$x, [ref]$y, [ref]$w, [ref]$h)) {
        throw "GetWindowRect failed"
    }
    return [pscustomobject]@{ X = $x; Y = $y; Width = $w; Height = $h }
}

function Save-Region {
    param(
        [string]$Path,
        [int]$X,
        [int]$Y,
        [int]$Width,
        [int]$Height
    )
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $size = New-Object System.Drawing.Size $Width, $Height
        $gfx.CopyFromScreen($X, $Y, 0, 0, $size)
        $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $gfx.Dispose()
        $bmp.Dispose()
    }
    Write-Host "wrote $Path"
}

function Move-Cursor {
    param([int]$X, [int]$Y)
    [void][WpfCaptureNative]::SetCursorPos($X, $Y)
}

function Write-TextNoBom {
    param([string]$Path, [string]$Text)
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $encoding = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $Text, $encoding)
}

function Stop-Aster {
    Get-Process -Name "Aster" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

# --- presets -------------------------------------------------------------

$baseSettings = [ordered]@{
    StartWithWindows      = $false
    MinimalNotch          = $false
    ShowMascot            = $true
    HideInFullscreen      = $true
    VolumeToast           = $true
    BrightnessToast       = $true
    LyricsEnabled         = $true
    LyricsHighlightColor  = "#7C9CFF"
    WaterReminderEnabled  = $false
    WaterIntervalMinutes  = 60
    WaterActiveStartHour  = 8
    WaterActiveEndHour    = 22
    WaterDailyGoal        = 8
    WaterGlassesToday     = 0
    WaterDate             = (Get-Date -Format "yyyy-MM-dd")
    CameraMirror          = $true
    GlassBackground       = $true
    BouncySpring          = $true
    FocusMinutes          = 25
    ShortBreakMinutes     = 5
    LongBreakMinutes      = 15
    FocusChime            = $true
}

$settingsPath = Join-Path $env:LOCALAPPDATA "DynamicIsland\settings.json"

function Write-Preset {
    param(
        [string]$Name,
        [hashtable]$Overrides,
        [switch]$Apply
    )
    $settings = [ordered]@{}
    foreach ($key in $baseSettings.Keys) {
        $settings[$key] = $baseSettings[$key]
    }
    foreach ($key in $Overrides.Keys) {
        $settings[$key] = $Overrides[$key]
    }
    $json = $settings | ConvertTo-Json -Depth 4

    $presetPath = Join-Path (Join-Path $OutDir "settings") "$Name.json"
    Write-TextNoBom -Path $presetPath -Text $json
    Write-Host "preset $presetPath"

    if ($Apply -and -not $DryRun -and -not $SkipLaunch) {
        Write-TextNoBom -Path $settingsPath -Text $json
        Write-Host "applied $settingsPath"
    }
}

function Write-AllPresets {
    Write-Preset -Name "01-default" -Overrides @{}
    Write-Preset -Name "02-minimal-notch" -Overrides @{ MinimalNotch = $true }
    Write-Preset -Name "03-glass-off" -Overrides @{ GlassBackground = $false }
    Write-Preset -Name "04-bouncy-off" -Overrides @{ BouncySpring = $false }
    Write-Preset -Name "05-mascot-off" -Overrides @{ ShowMascot = $false }
    Write-Preset -Name "06-overlays-off" -Overrides @{ VolumeToast = $false; BrightnessToast = $false }
    Write-Preset -Name "07-focus-1min" -Overrides @{ FocusMinutes = 1; ShortBreakMinutes = 1; LongBreakMinutes = 1 }
    Write-Preset -Name "08-focus-default" -Overrides @{ FocusMinutes = 25; ShortBreakMinutes = 5; LongBreakMinutes = 15 }
    Write-Preset -Name "09-water-active" -Overrides @{
        WaterReminderEnabled = $true
        WaterIntervalMinutes = 1
        WaterActiveStartHour = 0
        WaterActiveEndHour = 0
        WaterGlassesToday = 0
    }
    $hour = (Get-Date).Hour
    Write-Preset -Name "10-water-suppressed" -Overrides @{
        WaterReminderEnabled = $true
        WaterActiveStartHour = (($hour + 2) % 24)
        WaterActiveEndHour = (($hour + 3) % 24)
    }
    Write-Preset -Name "11-lyrics-custom" -Overrides @{ LyricsHighlightColor = "#F6A6C8" }
    Write-Preset -Name "12-camera-mirror-off" -Overrides @{ CameraMirror = $false }
    Write-Preset -Name "13-water-near-goal" -Overrides @{
        WaterReminderEnabled = $true
        WaterGlassesToday = 7
    }
    Write-Preset -Name "14-appearance-off" -Overrides @{
        VolumeToast = $false
        BrightnessToast = $false
        ShowMascot = $false
    }
}

# --- geometry ------------------------------------------------------------

$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$graphics = [System.Drawing.Graphics]::FromHwnd([IntPtr]::Zero)
try {
    $dpi = $graphics.DpiX
} finally {
    $graphics.Dispose()
}
$scale = [math]::Round($dpi / 96.0, 2)
$scaleTag = [string]([int][math]::Round($scale * 100))

$canvasWidth = [int][math]::Round(760 * $scale)
$canvasHeight = [int][math]::Round(460 * $scale)
$canvasX = $bounds.Left + [int](($bounds.Width - $canvasWidth) / 2)
$canvasY = $bounds.Top
$islandCenterX = $canvasX + [int]($canvasWidth / 2)
$islandCenterY = $canvasY + [int](19 * $scale)

$folder = Join-Path $OutDir ("screenshots\{0}-{1}" -f $scaleTag, $Wallpaper)
$videoDir = Join-Path $OutDir "videos\125-dark"

Write-Host ""
Write-Host "capture set : $OutDir"
Write-Host "binary      : $ExePath"
Write-Host "monitor     : $($bounds.Width)x$($bounds.Height) at scale $scale ($scaleTag%)"
Write-Host "canvas      : ${canvasWidth}x${canvasHeight} at ($canvasX,$canvasY)"
Write-Host "folder      : $folder"
Write-Host "media state : $MediaState"

if ($DryRun) {
    Write-Host ""
    Write-Host "Dry run: no settings written, no process started, nothing captured."
    exit 0
}

if (-not $SkipLaunch) {
    Stop-Aster
}

Write-AllPresets
Write-Preset -Name "01-default" -Overrides @{} -Apply

# --- info.md -------------------------------------------------------------

$infoPath = Join-Path $OutDir "info.md"
if (-not (Test-Path $infoPath)) {
    $os = Get-CimInstance Win32_OperatingSystem
    $gpu = Get-CimInstance Win32_VideoController | Select-Object -First 1
    $commit = "unknown"
    try { $commit = "$(git -C $root rev-parse HEAD)".Trim() } catch { }
    $binary = Get-Item $ExePath
    $info = @"
# WPF reference capture set

- Windows build: $($os.Caption) $($os.Version)
- GPU / driver: $($gpu.Name) $($gpu.DriverVersion)
- Primary display / scale: $($bounds.Width)x$($bounds.Height) @ $scaleTag%
- Secondary display: fill in
- Wallpaper: $Wallpaper
- WPF commit / version: $commit
- Binary: $($binary.Name) $([math]::Round($binary.Length / 1MB, 1)) MB
- Capture tool: capture-wpf-p0.ps1 + CopyFromScreen
- Date: $(Get-Date -Format "yyyy-MM-dd")
- Operator: $env:USERNAME

## Fixtures

- Media A:
- Media B:
- Media C:
- Media D:
- Camera:
- Audio:
- Brightness:

## Regions

- canvas: ${canvasWidth}x${canvasHeight} at ($canvasX, $canvasY)
- settings: fill in
- tray: fill in

## Tidak tertangkap

## Deviasi yang diketahui

- Tray menu pixels are OS-owned; only labels, order and checks are compared.
"@
    Write-TextNoBom -Path $infoPath -Text $info
    Write-Host "wrote $infoPath"
}

# --- launch and capture --------------------------------------------------

if (-not $SkipLaunch) {
    $process = Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
} else {
    $process = Get-Process -Name "Aster" -ErrorAction Stop | Select-Object -First 1
}

$handle = Wait-AsterWindow -ProcessId $process.Id
if ($handle -eq [IntPtr]::Zero) {
    throw "Aster window not found after 10 s."
}
Start-Sleep -Milliseconds 1200

$box = Get-WindowBox -Handle $handle
Write-Host "window      : $($box.Width)x$($box.Height) at ($($box.X),$($box.Y))"
if ($box.Width -ne $canvasWidth -or $box.Height -ne $canvasHeight) {
    Write-Warning "Window size differs from ${canvasWidth}x${canvasHeight}; using the reported rect."
}
if ($box.X -ne $canvasX -or $box.Y -ne $canvasY) {
    Write-Warning "Window is not at the expected centre; using the reported origin."
}
# The window rect is authoritative: it is the 760x460 DIP effect canvas.
$canvasX = $box.X
$canvasY = $box.Y
$canvasWidth = $box.Width
$canvasHeight = $box.Height
$islandCenterX = $canvasX + [int]($canvasWidth / 2)
$islandCenterY = $canvasY + [int](19 * $scale)

# Cursor away -> compact state, captured before the 8 s mascot swap.
Move-Cursor -X ($bounds.Left + 20) -Y ($bounds.Top + $bounds.Height - 20)
Start-Sleep -Milliseconds 700
$compactName = if ($MediaState -eq "playing") { "compact-media-playing.png" }
    elseif ($MediaState -eq "paused") { "compact-media-paused.png" }
    else { "compact-clock.png" }
Save-Region -Path (Join-Path $folder $compactName) `
    -X $canvasX -Y $canvasY -Width $canvasWidth -Height $canvasHeight

# Hover the island -> expanded.
Move-Cursor -X $islandCenterX -Y $islandCenterY
Start-Sleep -Milliseconds 1200
$expandedName = if ($MediaState -eq "playing") { "expanded-music-playing.png" }
    elseif ($MediaState -eq "paused") { "expanded-music-paused.png" }
    else { "expanded-music-empty.png" }
Save-Region -Path (Join-Path $folder $expandedName) `
    -X $canvasX -Y $canvasY -Width $canvasWidth -Height $canvasHeight

if ($Videos) {
    $ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($ffmpeg) {
        if (-not (Test-Path $videoDir)) { New-Item -ItemType Directory -Force -Path $videoDir | Out-Null }
        $videoPath = Join-Path $videoDir "expand-hover.mp4"
        $ffArgs = @(
            "-y", "-f", "gdigrab", "-framerate", "60",
            "-offset_x", "$canvasX", "-offset_y", "$canvasY",
            "-video_size", "${canvasWidth}x${canvasHeight}",
            "-i", "desktop", "-t", "8", "-c:v", "libx264", "-crf", "18",
            "-pix_fmt", "yuv420p", $videoPath
        ) | ForEach-Object {
            if ($_ -match "\s") { "`"$_`"" } else { $_ }
        }
        $recorder = Start-Process -FilePath $ffmpeg.Source -ArgumentList $ffArgs -PassThru -WindowStyle Hidden
        Start-Sleep -Milliseconds 1200
        Move-Cursor -X ($bounds.Left + 20) -Y ($bounds.Top + $bounds.Height - 20)
        Start-Sleep -Milliseconds 700
        Move-Cursor -X $islandCenterX -Y $islandCenterY
        Start-Sleep -Milliseconds 1500
        Move-Cursor -X ($bounds.Left + 20) -Y ($bounds.Top + $bounds.Height - 20)
        [void]$recorder.WaitForExit(20000)
        Write-Host "wrote $videoPath"
    } else {
        Write-Warning "ffmpeg not found; skip the hover video."
    }
}

# Minimal notch: apply the preset, restart, capture the collapsed bar.
if (-not $SkipLaunch) {
    Stop-Aster
    Write-Preset -Name "02-minimal-notch" -Overrides @{ MinimalNotch = $true } -Apply
    $process = Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
    $handle = Wait-AsterWindow -ProcessId $process.Id
    if ($handle -eq [IntPtr]::Zero) {
        throw "Aster window not found after the minimal-notch restart."
    }
    Start-Sleep -Milliseconds 1200
    $box = Get-WindowBox -Handle $handle
    $canvasX = $box.X
    $canvasY = $box.Y
    $canvasWidth = $box.Width
    $canvasHeight = $box.Height
    Move-Cursor -X ($bounds.Left + 20) -Y ($bounds.Top + $bounds.Height - 20)
    Start-Sleep -Milliseconds 700
    Save-Region -Path (Join-Path $folder "minimal-notch.png") `
        -X $canvasX -Y $canvasY -Width $canvasWidth -Height $canvasHeight

    # Restore the default preset and leave the app stopped.
    Stop-Aster
    Write-Preset -Name "01-default" -Overrides @{} -Apply
} else {
    Write-Warning "SkipLaunch set; minimal-notch capture skipped."
}

Write-Host ""
Write-Host "Aster is stopped and 01-default is restored."
Write-Host "Automated P0 subset done. Still manual: settings pages, tray, 200% duplicates,"
Write-Host "light wallpaper set, compact-eyes, and the click/chevron/tab videos."
Write-Host "Copy the folder to the Linux host, then run: ./scripts/check-wpf-captures.sh --set p0"
