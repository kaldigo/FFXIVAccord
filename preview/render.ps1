[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/preview'),
    [string]$Renderer
)
$ErrorActionPreference = 'Stop'
$null = New-Item -ItemType Directory -Force $OutputDirectory
if (!$Renderer) {
    $tools = Join-Path $PSScriptRoot '../artifacts/preview-tools/0.1.0-beta.3'
    $Renderer = Join-Path $tools 'glasswork-preview.exe'
    if (!(Test-Path $Renderer)) {
        dotnet tool install Glasswork.Preview --version 0.1.0-beta.3 --tool-path $tools
        if ($LASTEXITCODE) { throw 'Could not install Glasswork.Preview' }
    }
}
dotnet build (Join-Path $PSScriptRoot 'Accord.Preview.csproj') -c Release --nologo
if ($LASTEXITCODE) { throw 'Could not build Accord preview' }
$assembly = Join-Path $PSScriptRoot 'bin/Release/net10.0-windows/Accord.Preview.dll'
# A quiet backdrop makes the translucent controls legible without a game capture.
Add-Type -AssemblyName System.Drawing
$backdrop = Join-Path $OutputDirectory 'backdrop.png'
$bitmap = [Drawing.Bitmap]::new(640, 480)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$rect = [Drawing.Rectangle]::new(0, 0, 640, 480)
$brush = [Drawing.Drawing2D.LinearGradientBrush]::new($rect, [Drawing.Color]::FromArgb(22, 45, 63), [Drawing.Color]::FromArgb(53, 38, 61), 35.0)
try {
    $graphics.FillRectangle($brush, $rect)
    $bitmap.Save($backdrop, [Drawing.Imaging.ImageFormat]::Png)
} finally { $brush.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
foreach ($scene in 'accord-rail-hover', 'accord-play', 'accord-compact', 'accord-rules', 'accord-targeting', 'accord-hotbars', 'accord-tools', 'accord-healing', 'accord-mapping-compact') {
    & $Renderer --assembly $assembly --scene $scene --out (Join-Path $OutputDirectory "$scene.png") --backdrop $backdrop --warp --deterministic
    if ($LASTEXITCODE) { throw "Preview failed for $scene" }
}
