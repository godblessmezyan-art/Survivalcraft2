param([string]$ProjectDir = (Join-Path $PSScriptRoot '..\src\SCIEPort'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$textureDir = Join-Path $ProjectDir 'Assets\Textures\Clothing'
New-Item -ItemType Directory -Force -Path $textureDir | Out-Null

function New-ClothingTexture([string]$Name, [Drawing.Color]$Base, [Drawing.Color]$Accent, [int]$Seed) {
    $bitmap = [Drawing.Bitmap]::new(64,64,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([Drawing.Color]::Transparent)
    $brush = New-Object Drawing.SolidBrush $Base
    $pen = New-Object Drawing.Pen $Accent,1
    foreach ($rect in @((New-Object Drawing.Rectangle 0,0,64,16),(New-Object Drawing.Rectangle 16,16,32,32),(New-Object Drawing.Rectangle 0,16,16,32),(New-Object Drawing.Rectangle 48,16,16,32),(New-Object Drawing.Rectangle 16,48,16,16),(New-Object Drawing.Rectangle 32,48,16,16))) { $graphics.FillRectangle($brush,$rect); $graphics.DrawRectangle($pen,$rect) }
    for ($i=0; $i -lt 8; $i++) { $x = (($i * 11 + $Seed) % 58) + 3; $graphics.DrawLine($pen,$x,3,$x-3,13) }
    $graphics.Dispose(); $brush.Dispose(); $pen.Dispose()
    $bitmap.Save((Join-Path $textureDir ($Name + '.png')),[Drawing.Imaging.ImageFormat]::Png); $bitmap.Dispose()
}
New-ClothingTexture 'FourSaintsQinglong' ([Drawing.Color]::FromArgb(255,18,80,78)) ([Drawing.Color]::FromArgb(255,169,217,181)) 3
New-ClothingTexture 'FourSaintsXuanwu' ([Drawing.Color]::FromArgb(255,25,31,38)) ([Drawing.Color]::FromArgb(255,170,145,87)) 7
New-ClothingTexture 'FourSaintsBaihuLegs' ([Drawing.Color]::FromArgb(255,197,204,207)) ([Drawing.Color]::FromArgb(255,87,77,57)) 11
New-ClothingTexture 'FourSaintsZhuque' ([Drawing.Color]::FromArgb(255,125,28,22)) ([Drawing.Color]::FromArgb(255,244,180,65)) 17

$icon = [Drawing.Bitmap]::new(96,96,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [Drawing.Graphics]::FromImage($icon); $g.SmoothingMode = 'AntiAlias'; $g.Clear([Drawing.Color]::Transparent)
$gold = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255,178,137,62)),5
$silver = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255,235,238,235)),5
$g.DrawEllipse($gold,6,6,84,84); $g.DrawEllipse($silver,13,13,70,70)
$g.DrawArc($silver,24,24,48,42,205,130); $g.DrawLine($silver,28,58,39,70); $g.DrawLine($silver,68,58,57,70)
$g.DrawLine($gold,30,34,42,44); $g.DrawLine($gold,66,34,54,44); $g.DrawLine($silver,42,54,48,60); $g.DrawLine($silver,54,54,48,60)
$g.Dispose(); $gold.Dispose(); $silver.Dispose()
$icon.Save((Join-Path (Join-Path $ProjectDir 'Assets\Textures') 'FourSaintsBaihu.png'),[Drawing.Imaging.ImageFormat]::Png); $icon.Dispose()
Write-Host 'Generated original Four Saints clothing overlays and Baihu HUD emblem.'
