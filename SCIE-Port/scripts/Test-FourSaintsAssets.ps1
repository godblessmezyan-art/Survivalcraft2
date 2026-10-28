[CmdletBinding()]
param([string]$ProjectDir = (Join-Path $PSScriptRoot '..\src\SCIEPort'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$projectDir = (Resolve-Path -LiteralPath $ProjectDir).Path
$moduleRoot = Split-Path (Split-Path $projectDir -Parent) -Parent
$artifactRoot = Join-Path $moduleRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
$tempRoot = Join-Path $artifactRoot ('four-saints-assets-' + [Guid]::NewGuid().ToString('N'))
$tempProject = Join-Path $tempRoot 'src\SCIEPort'
New-Item -ItemType Directory -Force -Path $tempProject | Out-Null

$assets = @(
    'Assets\Textures\Clothing\FourSaintsQinglong.png',
    'Assets\Textures\Clothing\FourSaintsXuanwu.png',
    'Assets\Textures\Clothing\FourSaintsBaihuLegs.png',
    'Assets\Textures\Clothing\FourSaintsZhuque.png',
    'Assets\Textures\FourSaintsBaihu.png',
    'Assets\Textures\FourSaintsEffects.png'
)

function Test-Png([string]$path, [int]$width, [int]$height, [int]$minimumColors, [int]$minimumOpaque) {
    $bitmap = [Drawing.Bitmap]::new($path)
    try {
        if ($bitmap.Width -ne $width -or $bitmap.Height -ne $height) { throw "Unexpected PNG dimensions: $path ($($bitmap.Width)x$($bitmap.Height))" }
        $colors = [Collections.Generic.HashSet[int]]::new(); $opaque = 0; $transparent = 0
        for ($y=0; $y -lt $bitmap.Height; $y++) { for ($x=0; $x -lt $bitmap.Width; $x++) { $pixel=$bitmap.GetPixel($x,$y); if($pixel.A -gt 0){$opaque++; [void]$colors.Add($pixel.ToArgb())}else{$transparent++} } }
        if ($colors.Count -lt $minimumColors) { throw "Texture lacks authored material variation: $path ($($colors.Count) colors)" }
        if ($opaque -lt $minimumOpaque -or $transparent -eq 0) { throw "Texture alpha coverage is invalid: $path (opaque=$opaque transparent=$transparent)" }
    }
    finally { $bitmap.Dispose() }
}

try {
    & (Join-Path $PSScriptRoot 'New-FourSaintsAssets.ps1') -ProjectDir $tempProject
    foreach ($asset in $assets) {
        $actual = Join-Path $projectDir $asset; $generated = Join-Path $tempProject $asset
        if (-not (Test-Path -LiteralPath $actual) -or -not (Test-Path -LiteralPath $generated)) { throw "Missing Four Saints asset: $asset" }
        # Windows GDI+ encoders may emit different PNG chunks/compression for identical pixels.
        # Compare decoded ARGB (including fully transparent RGB) rather than container bytes.
        $originalBitmap = [Drawing.Bitmap]::new($actual)
        $generatedBitmap = [Drawing.Bitmap]::new($generated)
        try {
            if ($originalBitmap.Width -ne $generatedBitmap.Width -or $originalBitmap.Height -ne $generatedBitmap.Height) { throw "Four Saints dimensions are not reproducible: $asset" }
            for ($y=0; $y -lt $originalBitmap.Height; $y++) {
                for ($x=0; $x -lt $originalBitmap.Width; $x++) {
                    if ($originalBitmap.GetPixel($x,$y).ToArgb() -ne $generatedBitmap.GetPixel($x,$y).ToArgb()) { throw "Four Saints pixel is not reproducible: $asset ($x,$y)" }
                }
            }
        } finally { $originalBitmap.Dispose(); $generatedBitmap.Dispose() }
    }
    foreach ($name in @('FourSaintsQinglong.png','FourSaintsXuanwu.png','FourSaintsBaihuLegs.png','FourSaintsZhuque.png')) {
        Test-Png (Join-Path $projectDir ('Assets\Textures\Clothing\'+$name)) 64 64 6 45
    }
    Test-Png (Join-Path $projectDir 'Assets\Textures\FourSaintsBaihu.png') 96 96 10 500
    Test-Png (Join-Path $projectDir 'Assets\Textures\FourSaintsEffects.png') 256 64 12 1000
    Write-Host 'Four Saints V2 assets: 4 native 64x64 UV overlays + HUD + effect atlas are reproducible and structurally valid.'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        $resolvedTemp = (Resolve-Path -LiteralPath $tempRoot).Path
        $resolvedArtifacts = (Resolve-Path -LiteralPath $artifactRoot).Path
        if (-not $resolvedTemp.StartsWith($resolvedArtifacts, [StringComparison]::OrdinalIgnoreCase)) { throw "Refusing to clean outside artifacts: $resolvedTemp" }
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
    }
}
