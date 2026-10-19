[CmdletBinding()]
param(
    [string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' }),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $projectRoot 'src\SCIEPort'
$projectFile = Join-Path $projectDir 'SCIEPort.csproj'
$upstream = Join-Path $projectRoot 'upstream\SCIE'
$artifacts = Join-Path $projectRoot 'artifacts'
$build = Join-Path $artifacts 'build'
$stage = Join-Path $artifacts 'package'
$zip = Join-Path $artifacts 'SCIEPort.zip'
$package = Join-Path $artifacts 'SCIEPort.netmod'

& (Join-Path $PSScriptRoot 'Test-Environment.ps1') -GameDir $GameDir

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$resolvedArtifacts = (Resolve-Path -LiteralPath $artifacts).Path
foreach ($directory in @($build, $stage)) {
    if (Test-Path -LiteralPath $directory) {
        $resolved = (Resolve-Path -LiteralPath $directory).Path
        if (-not $resolved.StartsWith($resolvedArtifacts, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean path outside artifacts: $resolved"
        }
        Remove-Item -Recurse -Force -LiteralPath $resolved
    }
    New-Item -ItemType Directory -Path $directory | Out-Null
}

$dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
$dotnetSdkRoot = Join-Path (Split-Path -Parent $dotnetPath) 'sdk'
$sdkMsBuild = Get-ChildItem -LiteralPath $dotnetSdkRoot -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' -and (Test-Path -LiteralPath (Join-Path $_.FullName 'MSBuild.dll')) } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Get-Item -LiteralPath (Join-Path $_.FullName 'MSBuild.dll') } |
    Select-Object -First 1
if ($null -eq $sdkMsBuild) { throw "No installed .NET SDK MSBuild.dll found under $dotnetSdkRoot" }

# Invoke the installed SDK's MSBuild directly. This net472 project does not need
# the repository-wide SDK pinned by global.json, and direct invocation keeps the
# port build usable when that optional SDK is not installed locally.
& $dotnetPath $sdkMsBuild.FullName $projectFile /restore /t:Build "/p:Configuration=$Configuration" "/p:OutputPath=$build\" "/p:SCNetGameDir=$GameDir" /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed: $LASTEXITCODE" }

& (Join-Path $PSScriptRoot 'Test-Database.ps1')
if ($LASTEXITCODE -ne 0) { throw "Database validation failed: $LASTEXITCODE" }

Copy-Item -LiteralPath (Join-Path $build 'SCIEPort.dll') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectDir 'modinfo.json') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $stage
Copy-Item -Recurse -LiteralPath (Join-Path $projectDir 'Assets') -Destination (Join-Path $stage 'Assets')
# Keep the authorized WebP source for provenance, but do not ship it. The
# x25.01.17 Android content manager did not register it as Texture2D and the
# creative inventory crashed when a Kelly fruit icon was drawn.
$stagedKellyWebp = Join-Path $stage 'Assets\Textures\KellyExpansion.webp'
if (Test-Path -LiteralPath $stagedKellyWebp) {
    Remove-Item -Force -LiteralPath $stagedKellyWebp
}
$authorizedAssets = @(
    # Use the project-owned Y_UP derivative, not the upstream Z_UP model.
    @{ Source = (Join-Path $projectDir 'Assets\Models\SteamBoat.dae'); Destination = (Join-Path $stage 'Assets\Models\SteamBoat.dae') },
    @{ Source = (Join-Path $upstream 'IndustrialMod\Textures\SteamBoat.png'); Destination = (Join-Path $stage 'Assets\Textures\SteamBoat.png') },
    @{ Source = (Join-Path $projectDir 'Assets\Models\Airship.dae'); Destination = (Join-Path $stage 'Assets\Models\Airship.dae') },
    @{ Source = (Join-Path $projectDir 'Assets\Models\Train.dae'); Destination = (Join-Path $stage 'Assets\Models\Train.dae') },
    @{ Source = (Join-Path $upstream 'IndustrialMod\Models\Minecart.dae'); Destination = (Join-Path $stage 'Assets\Models\Minecart.dae') },
    @{ Source = (Join-Path $upstream 'IndustrialMod\Textures\Train.png'); Destination = (Join-Path $stage 'Assets\Textures\Train.png') },
    # Cylinder.dae is already Y_UP, so it is packaged without a derived copy.
    @{ Source = (Join-Path $upstream 'IndustrialMod\Models\Cylinder.dae'); Destination = (Join-Path $stage 'Assets\Models\Cylinder.dae') }
)
foreach ($asset in $authorizedAssets) {
    if (-not (Test-Path -LiteralPath $asset.Source)) {
        throw "Missing authorized upstream asset: $($asset.Source)"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $asset.Destination) | Out-Null
    Copy-Item -LiteralPath $asset.Source -Destination $asset.Destination
}

foreach ($old in @($zip, $package)) {
    if (Test-Path -LiteralPath $old) { Remove-Item -Force -LiteralPath $old }
}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length).TrimStart('\', '/').Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose() }
Move-Item -LiteralPath $zip -Destination $package

$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entries = @($archive.Entries | ForEach-Object FullName | Sort-Object)
    foreach ($requiredEntry in @(
        'SCIEPort.dll',
        'modinfo.json',
        'THIRD-PARTY-NOTICES.md',
        'Assets/Blocks/SCIEPort.csv',
        'Assets/Blocks/Watercraft.csv',
        'Assets/Blocks/KellyPlants.csv',
        'Assets/Blocks/IndustrialFurniture.csv',
        'Assets/Database/Watercraft.xdb',
        'Assets/Database/KellyPlants.xdb',
        'Assets/Models/Airship.dae',
        'Assets/Models/KellyFruits.dae',
        'Assets/Models/Gear.dae',
        'Assets/Models/GearMould.dae',
        'Assets/Models/Piston.dae',
        'Assets/Models/Train.dae',
        'Assets/Models/Wheel.dae',
        'Assets/Models/WheelMould.dae',
        'Assets/Models/Cylinder.dae',
        'Assets/Models/Minecart.dae',
        'Assets/Models/SteamBoat.dae',
        'Assets/Textures/Train.png',
        'Assets/Textures/SteamBoat.png',
        'Assets/Textures/Airship.png',
        'Assets/Textures/KellyExpansion.png'
    )) {
        if ($entries -notcontains $requiredEntry) { throw "Package missing: $requiredEntry" }
    }
    if ($entries -contains 'Assets/Textures/KellyExpansion.webp') {
        throw 'Package must not contain KellyExpansion.webp; x25.01.17 Android cannot load it as Texture2D.'
    }
    $unsupportedTextures = @($entries | Where-Object {
        $_ -like 'Assets/Textures/*' -and [IO.Path]::GetExtension($_) -ne '.png'
    })
    if ($unsupportedTextures.Count -gt 0) {
        throw ("Package contains texture formats not validated as x25.01.17 Texture2D resources: " +
            ($unsupportedTextures -join ', '))
    }
    if ($entries | Where-Object { $_ -match '(^|/)(Survivalcraft\.exe|Engine\.dll|EntitySystem\.dll|Jint\.dll|System\.Reflection\.TypeExtensions\.dll)$' }) {
        throw 'Package contains a prohibited game or upstream dependency binary.'
    }

    $blockClassNames = New-Object System.Collections.Generic.List[string]
    $kellyDisplayNames = New-Object System.Collections.Generic.List[string]
    $furnitureDisplayNames = New-Object System.Collections.Generic.List[string]
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName -notmatch '^Assets/Blocks/.*\.csv$') { continue }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { $lines = @($reader.ReadToEnd() -split "`r?`n") }
        finally { $reader.Dispose() }
        for ($i = 1; $i -lt $lines.Count; $i++) {
            if ([string]::IsNullOrWhiteSpace($lines[$i])) { continue }
            $columns = $lines[$i] -split ';'
            if ($columns.Count -gt 0) { $blockClassNames.Add($columns[0].Trim()) }
            if ($entry.FullName -eq 'Assets/Blocks/KellyPlants.csv' -and $columns.Count -gt 1) {
                $kellyDisplayNames.Add($columns[1].Trim())
            }
            if ($entry.FullName -eq 'Assets/Blocks/IndustrialFurniture.csv' -and $columns.Count -gt 1) {
                $furnitureDisplayNames.Add($columns[1].Trim())
            }
        }
    }
    $duplicateClasses = @($blockClassNames | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
    if ($duplicateClasses.Count -gt 0) {
        throw ("Duplicate block class names in package CSVs: " + ($duplicateClasses -join ', '))
    }
    $kellyNameBytes = [Text.Encoding]::UTF8.GetBytes(($kellyDisplayNames -join '|'))
    $kellyNameHasher = [Security.Cryptography.SHA256]::Create()
    try {
        $kellyNamesHash = ($kellyNameHasher.ComputeHash($kellyNameBytes) | ForEach-Object { $_.ToString('x2') }) -join ''
    }
    finally { $kellyNameHasher.Dispose() }
    if ($kellyDisplayNames.Count -ne 14 -or $kellyNamesHash -ne '5b868dfafcb2fd858f71f73d0997c16779a2cbf7f5696848c1df341651cb3656') {
        throw ("Kelly inventory names differ from the authorized source names: " + ($kellyDisplayNames -join ', '))
    }
    if ($kellyDisplayNames | Where-Object { $_ -match '[\uFF08\uFF09()]' }) {
        throw ("Kelly inventory names must remain original and contain no port-marker parentheses: " + ($kellyDisplayNames -join ', '))
    }
    $furnitureNameBytes = [Text.Encoding]::UTF8.GetBytes(($furnitureDisplayNames -join '|'))
    $furnitureNameHasher = [Security.Cryptography.SHA256]::Create()
    try {
        $furnitureNamesHash = ($furnitureNameHasher.ComputeHash($furnitureNameBytes) | ForEach-Object { $_.ToString('x2') }) -join ''
    }
    finally { $furnitureNameHasher.Dispose() }
    if ($furnitureDisplayNames.Count -ne 6 -or $furnitureNamesHash -ne '258d7960b1fb60731d9754f786864dfcfa64cf0e4d37ce8f5f18cb5ec26caf18') {
        throw ("Industrial furniture names or order differ from the audited list: " + ($furnitureDisplayNames -join ', '))
    }
    if ($furnitureDisplayNames | Where-Object { $_ -match '[\uFF08\uFF09()]' }) {
        throw ("Industrial furniture names must contain no port-marker parentheses: " + ($furnitureDisplayNames -join ', '))
    }
    [xml]$watercraftDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\Watercraft.xdb')
    foreach ($parameter in $watercraftDatabase.SelectNodes("//Parameter[@Name='ModelName']")) {
        $modelEntry = 'Assets/' + $parameter.Value + '.dae'
        if ($entries -notcontains $modelEntry) {
            throw "Watercraft database references missing model resource: $modelEntry"
        }
    }
    foreach ($parameter in $watercraftDatabase.SelectNodes("//Parameter[@Name='TextureOverride']")) {
        # This carriage texture is supplied by the base game, not by the Mod.
        if ($parameter.Value -eq 'Textures/Creatures/Jaguar') { continue }
        $textureEntry = 'Assets/' + $parameter.Value + '.png'
        if ($entries -notcontains $textureEntry) {
            throw "Watercraft database references missing PNG Texture2D resource: $textureEntry"
        }
    }
    $airshipEntity = $watercraftDatabase.SelectSingleNode("//EntityTemplate[@Name='SCIEPortAirship']")
    $airshipMount = $airshipEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Mount']/Parameter[@Name='MountOffset']")
    $airshipDismount = $airshipEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Mount']/Parameter[@Name='DismountOffset']")
    $airshipBox = $airshipEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Body']/Parameter[@Name='BoxSize']")
    if ($null -eq $airshipMount -or $airshipMount.Value -ne '0,1,-0.8') {
        throw 'Airship MountOffset must remain reachable at 0,1,-0.8.'
    }
    if ($null -eq $airshipDismount -or $airshipDismount.Value -ne '1.75,0.5,0') {
        throw 'Airship DismountOffset must remain outside the cabin collision box.'
    }
    if ($null -eq $airshipBox -or $airshipBox.Value -ne '3,2,6') {
        throw 'Airship collision box must remain cabin-sized (3,2,6) so the native 2.5-block mount search can reach its seat.'
    }
    [xml]$steamBoatModel = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Models\SteamBoat.dae')
    $meshNode = $steamBoatModel.SelectSingleNode("//*[local-name()='node' and @name='Cylinder']")
    if ($null -eq $meshNode) {
        throw 'Authorized SteamBoat.dae is missing the required Cylinder mesh node.'
    }
    $upAxisNode = $steamBoatModel.SelectSingleNode("//*[local-name()='asset']/*[local-name()='up_axis']")
    if ($null -eq $upAxisNode -or $upAxisNode.InnerText -ne 'Y_UP') {
        throw 'SteamBoat.dae must be oriented Y_UP (the Z_UP -> Y_UP fix did not ship).'
    }
    $posArray = $steamBoatModel.SelectSingleNode("//*[local-name()='float_array'][contains(@id,'-positions-')]")
    if ($null -eq $posArray) {
        throw 'SteamBoat.dae is missing its vertex positions array.'
    }
    $posCount = (@($posArray.InnerText -split '\s+' | Where-Object { $_ -ne '' })).Count
    if ($posCount -ne 294) {
        throw "SteamBoat.dae positions array should hold 98 vertices (294 floats); found $posCount. The orientation fix may have altered geometry."
    }

    [xml]$airshipModel = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Models\Airship.dae')
    $airshipMeshNode = $airshipModel.SelectSingleNode("//*[local-name()='node' and @name='Airship']")
    if ($null -eq $airshipMeshNode) {
        throw 'Authorized Airship.dae is missing the required Airship mesh node.'
    }
    if ($airshipModel.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') {
        throw 'Airship.dae must be oriented Y_UP.'
    }
    $airshipPositions = $airshipModel.SelectSingleNode("//*[local-name()='float_array'][contains(@id,'-positions-')]")
    if ((@($airshipPositions.InnerText -split '\s+' | Where-Object { $_ })).Count -ne 2778) {
        throw 'Airship.dae positions array should hold 926 vertices (2778 floats).'
    }

    [xml]$trainModel = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Models\Train.dae')
    if ($null -eq $trainModel.SelectSingleNode("//*[local-name()='node' and @name='Cylinder']")) {
        throw 'Authorized Train.dae is missing the required Cylinder mesh node.'
    }
    if ($trainModel.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') {
        throw 'Train.dae must be oriented Y_UP.'
    }
    $trainPositions = $trainModel.SelectSingleNode("//*[local-name()='float_array'][contains(@id,'-positions-')]")
    if ((@($trainPositions.InnerText -split '\s+' | Where-Object { $_ })).Count -ne 660) {
        throw 'Train.dae positions array should hold 220 vertices (660 floats).'
    }
    [xml]$minecartModel = Get-Content -Raw -Encoding UTF8 (Join-Path $upstream 'IndustrialMod\Models\Minecart.dae')
    if ($null -eq $minecartModel.SelectSingleNode("//*[local-name()='node' and @name='obj1']")) {
        throw 'Authorized Minecart.dae is missing the required obj1 mesh node.'
    }
    if ($minecartModel.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') {
        throw 'Minecart.dae must be oriented Y_UP.'
    }

    $furnitureModels = @(
        @{ Name = 'Gear'; Node = 'Gear'; Floats = 192 },
        @{ Name = 'Wheel'; Node = 'Wheel'; Floats = 72 },
        @{ Name = 'GearMould'; Node = 'GearMould'; Floats = 216 },
        @{ Name = 'WheelMould'; Node = 'WheelMould'; Floats = 96 },
        @{ Name = 'Piston'; Node = 'Piston'; Floats = 144 },
        @{ Name = 'Cylinder'; Node = 'obj1'; Floats = 390 }
    )
    foreach ($furnitureModel in $furnitureModels) {
        $modelPath = Join-Path $stage ("Assets\Models\" + $furnitureModel.Name + '.dae')
        [xml]$modelDocument = Get-Content -Raw -Encoding UTF8 $modelPath
        if ($modelDocument.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') {
            throw "$($furnitureModel.Name).dae must be oriented Y_UP."
        }
        if ($null -eq $modelDocument.SelectSingleNode("//*[local-name()='node' and @name='$($furnitureModel.Node)']")) {
            throw "$($furnitureModel.Name).dae is missing required node $($furnitureModel.Node)."
        }
        $positions = @($modelDocument.SelectNodes("//*[local-name()='float_array'][contains(@id,'-positions-')]"))
        $positionFloatCount = 0
        foreach ($positionsArray in $positions) {
            $positionFloatCount += (@($positionsArray.InnerText -split '\s+' | Where-Object { $_ })).Count
        }
        if ($positionFloatCount -ne $furnitureModel.Floats) {
            throw "$($furnitureModel.Name).dae positions should contain $($furnitureModel.Floats) floats; found $positionFloatCount."
        }
    }

    $pngBytes = [IO.File]::ReadAllBytes((Join-Path $upstream 'IndustrialMod\Textures\SteamBoat.png'))
    if ($pngBytes.Length -lt 24 -or $pngBytes[0] -ne 137 -or $pngBytes[1] -ne 80 -or $pngBytes[2] -ne 78 -or $pngBytes[3] -ne 71) {
        throw 'Authorized SteamBoat.png does not have a valid PNG signature.'
    }
    $trainPngBytes = [IO.File]::ReadAllBytes((Join-Path $upstream 'IndustrialMod\Textures\Train.png'))
    if ($trainPngBytes.Length -lt 24 -or $trainPngBytes[0] -ne 137 -or $trainPngBytes[1] -ne 80 -or $trainPngBytes[2] -ne 78 -or $trainPngBytes[3] -ne 71) {
        throw 'Authorized Train.png does not have a valid PNG signature.'
    }
}
finally { $archive.Dispose() }

Write-Host "Package: $package"
Write-Host "Entries: $($entries -join ', ')"
Write-Host "SHA-256: $((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash)"
