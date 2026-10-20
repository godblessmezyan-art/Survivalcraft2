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

$kellyTests = Join-Path $projectRoot 'tests\SCIEPort.KellyPlants.Tests\SCIEPort.KellyPlants.Tests.csproj'
& $dotnetPath run --project $kellyTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Kelly plant rule tests failed: $LASTEXITCODE" }
$atvTests = Join-Path $projectRoot 'tests\SCIEPort.LandVehicles.Tests\SCIEPort.LandVehicles.Tests.csproj'
& $dotnetPath run --project $atvTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "ATV drive rule tests failed: $LASTEXITCODE" }
$watercraftTests = Join-Path $projectRoot 'tests\SCIEPort.Watercraft.Tests\SCIEPort.Watercraft.Tests.csproj'
& $dotnetPath run --project $watercraftTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Watercraft rule tests failed: $LASTEXITCODE" }
$tankTests = Join-Path $projectRoot 'tests\SCIEPort.Tanks.Tests\SCIEPort.Tanks.Tests.csproj'
& $dotnetPath run --project $tankTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Tank drive rule tests failed: $LASTEXITCODE" }

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
        'Assets/Blocks/LandVehicles.csv',
        'Assets/Blocks/Tanks.csv',
        'Assets/Database/Watercraft.xdb',
        'Assets/Database/KellyPlants.xdb',
        'Assets/Database/LandVehicles.xdb',
        'Assets/Database/Tanks.xdb',
        'Assets/Models/ATV.dae',
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
        'Assets/Models/T34Tank.dae',
        'Assets/Textures/Train.png',
        'Assets/Textures/SteamBoat.png',
        'Assets/Textures/Airship.png',
        'Assets/Textures/ATV.png',
        'Assets/Textures/KellyExpansion.png',
        'Assets/Textures/T34Tank.png'
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
    $steamBoatEntity = $watercraftDatabase.SelectSingleNode("//EntityTemplate[@Name='SCIEPortSteamBoat']")
    if ($null -eq $steamBoatEntity) { throw 'Watercraft database is missing SCIEPortSteamBoat.' }
    foreach ($requiredSteamBoatComponent in @('TwoSeatManager', 'SteamBoatDrive', 'SimpleModel', 'Spawn', 'Mount', 'Body', 'Damage')) {
        if ($null -eq $steamBoatEntity.SelectSingleNode("./MemberComponentTemplate[@Name='$requiredSteamBoatComponent']")) {
            throw "Speedboat entity is missing component: $requiredSteamBoatComponent"
        }
    }
    if ($null -ne $steamBoatEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Boat']")) {
        throw 'Dual-seat speedboat must not include native ComponentBoat because passenger input would overwrite driver input.'
    }
    if (@($steamBoatEntity.SelectNodes("./MemberComponentTemplate[@Name='Mount']")).Count -ne 1) {
        throw 'Speedboat must keep exactly one native Mount; passenger role is managed by association.'
    }
    $steamBoatSeats = $steamBoatEntity.SelectSingleNode("./MemberComponentTemplate[@Name='TwoSeatManager']")
    $steamBoatMount = $steamBoatEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Mount']")
    $steamBoatBody = $steamBoatEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Body']")
    if ($steamBoatSeats.SelectSingleNode("./Parameter[@Name='DriverOffset']").Value -ne '-0.42,1,-0.45' -or
        $steamBoatSeats.SelectSingleNode("./Parameter[@Name='PassengerOffset']").Value -ne '0.42,1,-0.45') {
        throw 'Speedboat seat offsets differ from the audited side-by-side layout.'
    }
    if ($steamBoatMount.SelectSingleNode("./Parameter[@Name='MountOffset']").Value -ne '-0.42,1,-0.45' -or
        $steamBoatMount.SelectSingleNode("./Parameter[@Name='DismountOffset']").Value -ne '1.65,0.5,0') {
        throw 'Speedboat mount or safe side-dismount offset differs from the audited layout.'
    }
    if ($steamBoatBody.SelectSingleNode("./Parameter[@Name='BoxSize']").Value -ne '2.3,1.75,5.6') {
        throw 'Speedboat collision box must cover the authorized model at 2.3,1.75,5.6.'
    }
    [xml]$landVehiclesDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\LandVehicles.xdb')
    $atvEntity = $landVehiclesDatabase.SelectSingleNode("//EntityTemplate[@Name='SCIEPortATV']")
    if ($null -eq $atvEntity) { throw 'LandVehicles database is missing SCIEPortATV.' }
    $atvModel = $atvEntity.SelectSingleNode("./MemberComponentTemplate[@Name='SimpleModel']/Parameter[@Name='ModelName']")
    $atvTexture = $atvEntity.SelectSingleNode("./MemberComponentTemplate[@Name='SimpleModel']/Parameter[@Name='TextureOverride']")
    $atvStepHeight = $atvEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Body']/Parameter[@Name='MaxSmoothRiseHeight']")
    $atvBox = $atvEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Body']/Parameter[@Name='BoxSize']")
    $atvMount = $atvEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Mount']/Parameter[@Name='MountOffset']")
    if ($null -eq $atvModel -or $atvModel.Value -ne 'Models/ATV') { throw 'ATV ModelName must be Models/ATV.' }
    if ($null -eq $atvTexture -or $atvTexture.Value -ne 'Textures/ATV') { throw 'ATV TextureOverride must be the packaged Textures/ATV.' }
    if ($null -eq $atvStepHeight -or $atvStepHeight.Value -ne '1.05') { throw 'ATV MaxSmoothRiseHeight must provide the audited one-cell clearance of 1.05 cells.' }
    if ($null -eq $atvBox -or $atvBox.Value -ne '1.1,1.25,2.6') { throw 'ATV collision box must cover the tandem motorcycle model at 1.1,1.25,2.6.' }
    if ($null -eq $atvMount -or $atvMount.Value -ne '0,0.9,-0.3') { throw 'ATV driver mount must remain at the audited front-seat offset.' }
    if ($null -eq $atvEntity.SelectSingleNode("./MemberComponentTemplate[@Name='ATVSeatManager']")) { throw 'ATV entity is missing its authoritative seat manager.' }
    if (@($atvEntity.SelectNodes("./MemberComponentTemplate[@Name='Mount']")).Count -ne 1) { throw 'ATV must keep exactly one native Mount; passenger role is managed by association.' }
    [xml]$tankDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\Tanks.xdb')
    $tankEntity = $tankDatabase.SelectSingleNode("//EntityTemplate[@Name='SCIEPortT34Tank']")
    if ($null -eq $tankEntity) { throw 'Tank database is missing SCIEPortT34Tank.' }
    foreach ($requiredTankComponent in @('TankDrive', 'SimpleModel', 'Spawn', 'Mount', 'Body', 'Damage')) {
        if ($null -eq $tankEntity.SelectSingleNode("./MemberComponentTemplate[@Name='$requiredTankComponent']")) {
            throw "Tank entity is missing component: $requiredTankComponent"
        }
    }
    $tankModel = $tankEntity.SelectSingleNode("./MemberComponentTemplate[@Name='SimpleModel']/Parameter[@Name='ModelName']")
    $tankTexture = $tankEntity.SelectSingleNode("./MemberComponentTemplate[@Name='SimpleModel']/Parameter[@Name='TextureOverride']")
    $tankStep = $tankEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Body']/Parameter[@Name='MaxSmoothRiseHeight']")
    $tankBox = $tankEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Body']/Parameter[@Name='BoxSize']")
    $tankMount = $tankEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Mount']/Parameter[@Name='MountOffset']")
    $tankDismount = $tankEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Mount']/Parameter[@Name='DismountOffset']")
    if ($tankModel.Value -ne 'Models/T34Tank' -or $tankTexture.Value -ne 'Textures/T34Tank' -or $tankStep.Value -ne '1.05') {
        throw 'Tank entity model, texture or ground-step parameters are not the audited values.'
    }
    if ($tankBox.Value -ne '3.6,2.1,5.9' -or $tankMount.Value -ne '0,1.72,0.2' -or $tankDismount.Value -ne '2.25,0.4,0') {
        throw 'Tank collision or seat parameters differ from the audited low-poly model envelope.'
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
    $steamBoatCoordinates = @($posArray.InnerText -split '\s+' | Where-Object { $_ -ne '' } | ForEach-Object {
        [double]::Parse($_, [Globalization.CultureInfo]::InvariantCulture)
    })
    $steamBoatX = for ($i = 0; $i -lt $steamBoatCoordinates.Count; $i += 3) { $steamBoatCoordinates[$i] }
    $steamBoatY = for ($i = 1; $i -lt $steamBoatCoordinates.Count; $i += 3) { $steamBoatCoordinates[$i] }
    $steamBoatZ = for ($i = 2; $i -lt $steamBoatCoordinates.Count; $i += 3) { $steamBoatCoordinates[$i] }
    if (($steamBoatX | Measure-Object -Minimum).Minimum -lt -1.15 -or ($steamBoatX | Measure-Object -Maximum).Maximum -gt 1.15 -or
        ($steamBoatY | Measure-Object -Minimum).Minimum -lt -0.0001 -or ($steamBoatY | Measure-Object -Maximum).Maximum -gt 1.75 -or
        ($steamBoatZ | Measure-Object -Minimum).Minimum -lt -2.8 -or ($steamBoatZ | Measure-Object -Maximum).Maximum -gt 2.8) {
        throw 'SteamBoat.dae visible geometry exceeds the 2.3,1.75,5.6 ComponentBody collision box.'
    }
    $targetEngineAssembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'Engine.dll')))
    $targetModelDataType = $targetEngineAssembly.GetType('Engine.Media.ModelData', $true)
    $targetModelLoad = $targetModelDataType.GetMethod('Load', [Type[]]@([IO.Stream]))
    $steamBoatModelStream = [IO.File]::OpenRead((Join-Path $projectDir 'Assets\Models\SteamBoat.dae'))
    try { $targetSteamBoatModel = $targetModelLoad.Invoke($null, @($steamBoatModelStream)) }
    finally { $steamBoatModelStream.Dispose() }
    if ($targetSteamBoatModel.Bones.Count -ne 1 -or $targetSteamBoatModel.Meshes.Count -ne 1) {
        throw "Target Engine must import SteamBoat.dae as 1 bone/1 mesh; found $($targetSteamBoatModel.Bones.Count) bones/$($targetSteamBoatModel.Meshes.Count) meshes."
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

    [xml]$atvModelDocument = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Models\ATV.dae')
    if ($atvModelDocument.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') {
        throw 'ATV.dae must be oriented Y_UP.'
    }
    $atvParts = @('BikeFrame', 'BikeEngine', 'BikeFuelTank', 'BikeDriverSeat', 'BikePassengerSeat', 'BikeFrontFork', 'BikeHandlebarStem', 'BikeHandlebar', 'BikeFrontWheel', 'BikeRearWheel', 'BikeFrontFender', 'BikeRearFender')
    $atvSceneNodes = @($atvModelDocument.SelectNodes("//*[local-name()='visual_scene']/*[local-name()='node']"))
    if ($atvSceneNodes.Count -ne 1 -or $atvSceneNodes[0].GetAttribute('name') -ne 'ATV') {
        throw 'ATV.dae must use exactly one ATV scene node so ComponentSimpleModel stays within the SCNET seven-bone shader limit.'
    }
    $nodeMatrix = $atvSceneNodes[0].SelectSingleNode("./*[local-name()='matrix']")
    if ($null -eq $nodeMatrix -or ($nodeMatrix.InnerText -replace '\s+', ' ').Trim() -ne '1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1') {
        throw 'ATV.dae must use one identity root matrix; coordinates must be baked to prevent importer-dependent scaling.'
    }
    $atvGeometryInstances = @($atvSceneNodes[0].SelectNodes("./*[local-name()='instance_geometry']") | ForEach-Object { $_.GetAttribute('url').TrimStart('#') })
    if ($atvGeometryInstances.Count -ne $atvParts.Count) {
        throw "ATV.dae root must reference all $($atvParts.Count) independent geometries; found $($atvGeometryInstances.Count)."
    }
    foreach ($requiredNode in $atvParts) {
        if ($atvGeometryInstances -notcontains "$requiredNode-mesh") {
            throw "ATV.dae root does not reference required geometry $requiredNode-mesh."
        }
        $geometry = $atvModelDocument.SelectSingleNode("//*[local-name()='geometry' and @name='$requiredNode']")
        if ($null -eq $geometry) { throw "ATV.dae is missing independent geometry $requiredNode." }
        $positions = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'positions-array')]")
        if ((@($positions.InnerText -split '\s+' | Where-Object { $_ })).Count -ne 24) {
            throw "ATV.dae geometry $requiredNode must contain 8 cuboid positions (24 floats)."
        }
        $triangles = $geometry.SelectSingleNode(".//*[local-name()='triangles']")
        if ($null -eq $triangles -or $triangles.GetAttribute('count') -ne '12') {
            throw "ATV.dae geometry $requiredNode must contain exactly 12 triangles."
        }
        foreach ($semantic in @('VERTEX', 'NORMAL', 'TEXCOORD')) {
            if ($null -eq $triangles.SelectSingleNode("./*[local-name()='input' and @semantic='$semantic']")) {
                throw "ATV.dae geometry $requiredNode is missing $semantic data."
            }
        }
        $faceIndices = @($triangles.SelectSingleNode("./*[local-name()='p']").InnerText -split '\s+' | Where-Object { $_ } | ForEach-Object { [int]$_ })
        if ($faceIndices.Count -ne 108) { throw "ATV.dae geometry $requiredNode must contain 108 interleaved face indices; found $($faceIndices.Count)." }
        for ($i = 0; $i -lt $faceIndices.Count; $i += 3) {
            if ($faceIndices[$i] -lt 0 -or $faceIndices[$i] -gt 7 -or
                $faceIndices[$i + 1] -lt 0 -or $faceIndices[$i + 1] -gt 5 -or
                $faceIndices[$i + 2] -ne 0) {
                throw "ATV.dae geometry $requiredNode contains an out-of-range vertex/normal/UV face index at tuple $($i / 3)."
            }
        }
    }
    $atvGeometryNames = @($atvModelDocument.SelectNodes("//*[local-name()='geometry']") | ForEach-Object { $_.GetAttribute('name') })
    if (($atvGeometryNames | Select-Object -Unique).Count -ne $atvParts.Count) {
        throw 'ATV.dae parts must not share or duplicate geometry names.'
    }
    $atvModelStream = [IO.File]::OpenRead((Join-Path $projectDir 'Assets\Models\ATV.dae'))
    try {
        $targetAtvModel = $targetModelLoad.Invoke($null, @($atvModelStream))
    }
    finally { $atvModelStream.Dispose() }
    if ($targetAtvModel.Bones.Count -ne 1 -or $targetAtvModel.Meshes.Count -ne $atvParts.Count) {
        throw "Target Engine must import ATV.dae as 1 bone/$($atvParts.Count) meshes; found $($targetAtvModel.Bones.Count) bones/$($targetAtvModel.Meshes.Count) meshes. SCNET's default shader budget is seven bones."
    }
    $atvCoordinates = New-Object System.Collections.Generic.List[double]
    foreach ($positionArray in $atvModelDocument.SelectNodes("//*[local-name()='float_array'][contains(@id,'positions-array')]")) {
        foreach ($coordinate in @($positionArray.InnerText -split '\s+' | Where-Object { $_ })) {
            $atvCoordinates.Add([double]::Parse($coordinate, [Globalization.CultureInfo]::InvariantCulture))
        }
    }
    $atvX = for ($i = 0; $i -lt $atvCoordinates.Count; $i += 3) { $atvCoordinates[$i] }
    $atvY = for ($i = 1; $i -lt $atvCoordinates.Count; $i += 3) { $atvCoordinates[$i] }
    $atvZ = for ($i = 2; $i -lt $atvCoordinates.Count; $i += 3) { $atvCoordinates[$i] }
    if (($atvX | Measure-Object -Minimum).Minimum -lt -0.55 -or ($atvX | Measure-Object -Maximum).Maximum -gt 0.55 -or
        ($atvY | Measure-Object -Minimum).Minimum -lt 0 -or ($atvY | Measure-Object -Maximum).Maximum -gt 1.25 -or
        ($atvZ | Measure-Object -Minimum).Minimum -lt -1.3 -or ($atvZ | Measure-Object -Maximum).Maximum -gt 1.3) {
        throw 'ATV.dae visible geometry exceeds the tandem motorcycle 1.1,1.25,2.6 ComponentBody collision box.'
    }
    Add-Type -AssemblyName System.Drawing
    $atvBitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\ATV.png'))
    try {
        if ($atvBitmap.Width -ne 32 -or $atvBitmap.Height -ne 8) { throw 'ATV.png must be the audited 32x8 opaque palette.' }
        $atvColors = New-Object System.Collections.Generic.HashSet[int]
        for ($x = 0; $x -lt $atvBitmap.Width; $x++) {
            for ($y = 0; $y -lt $atvBitmap.Height; $y++) {
                $pixel = $atvBitmap.GetPixel($x, $y)
                if ($pixel.A -ne 255) { throw "ATV.png contains transparent pixel at $x,$y." }
                [void]$atvColors.Add($pixel.ToArgb())
            }
        }
        if ($atvColors.Count -ne 4) { throw "ATV.png must contain exactly four opaque palette colors; found $($atvColors.Count)." }
    }
    finally { $atvBitmap.Dispose() }

    [xml]$tankModelDocument = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Models\T34Tank.dae')
    if ($tankModelDocument.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') {
        throw 'T34Tank.dae must be oriented Y_UP.'
    }
    $tankRoot = $tankModelDocument.SelectSingleNode("//*[local-name()='visual_scene']/*[local-name()='node' and @name='TankHull']")
    if ($null -eq $tankRoot) { throw 'T34Tank.dae is missing the TankHull root.' }
    $tankTurret = $tankRoot.SelectSingleNode("./*[local-name()='node' and @name='TankTurret']")
    if ($null -eq $tankTurret) { throw 'T34Tank.dae is missing the TankTurret child.' }
    $tankGun = $tankTurret.SelectSingleNode("./*[local-name()='node' and @name='TankGun']")
    if ($null -eq $tankGun) {
        throw 'T34Tank.dae must use TankHull > TankTurret > TankGun articulated hierarchy.'
    }
    $tankTurretTranslation = ($tankTurret.SelectSingleNode("./*[local-name()='translate']").InnerText -replace '\s+', ' ').Trim()
    $tankGunTranslation = ($tankGun.SelectSingleNode("./*[local-name()='translate']").InnerText -replace '\s+', ' ').Trim()
    if ($tankTurretTranslation -ne '0 1.242 0' -or $tankGunTranslation -ne '0 0.324 -0.7425') {
        throw 'T34Tank.dae articulated pivots must retain the audited 1.35x model scale.'
    }
    $tankParts = @('LowerHull','UpperHull','Glacis','TrackLeft','TrackRight',
        'WheelL1','WheelL2','WheelL3','WheelL4','WheelL5',
        'WheelR1','WheelR2','WheelR3','WheelR4','WheelR5',
        'TurretBase','TurretRoof','Hatch','GunBarrel','GunMuzzle')
    foreach ($tankMeshName in $tankParts) {
        $tankGeometry = $tankModelDocument.SelectSingleNode("//*[local-name()='geometry' and @name='$tankMeshName']")
        if ($null -eq $tankGeometry) { throw "T34Tank.dae is missing geometry $tankMeshName." }
        $tankPositions = $tankGeometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'positions-array')]")
        if ((@($tankPositions.InnerText -split '\s+' | Where-Object { $_ })).Count -ne 24) {
            throw "T34Tank.dae geometry $tankMeshName must contain 8 cuboid positions."
        }
        $tankTriangles = $tankGeometry.SelectSingleNode(".//*[local-name()='triangles']")
        if ($null -eq $tankTriangles -or $tankTriangles.GetAttribute('count') -ne '12') {
            throw "T34Tank.dae geometry $tankMeshName must contain 12 low-poly triangles."
        }
        foreach ($semantic in @('VERTEX', 'NORMAL', 'TEXCOORD')) {
            if ($null -eq $tankTriangles.SelectSingleNode("./*[local-name()='input' and @semantic='$semantic']")) {
                throw "T34Tank.dae geometry $tankMeshName is missing $semantic data."
            }
        }
    }
    $tankModelStream = [IO.File]::OpenRead((Join-Path $projectDir 'Assets\Models\T34Tank.dae'))
    try { $targetTankModel = $targetModelLoad.Invoke($null, @($tankModelStream)) }
    finally { $tankModelStream.Dispose() }
    if ($targetTankModel.Bones.Count -ne 3 -or $targetTankModel.Meshes.Count -ne $tankParts.Count) {
        throw "Target Engine must import T34Tank.dae as 3 bones/$($tankParts.Count) meshes; found $($targetTankModel.Bones.Count) bones/$($targetTankModel.Meshes.Count) meshes."
    }
    Add-Type -AssemblyName System.Drawing
    $tankBitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\T34Tank.png'))
    try {
        if ($tankBitmap.Width -ne 64 -or $tankBitmap.Height -ne 8) { throw 'T34Tank.png must be the audited 64x8 opaque palette.' }
        $tankColors = New-Object System.Collections.Generic.HashSet[int]
        for ($x = 0; $x -lt $tankBitmap.Width; $x++) {
            for ($y = 0; $y -lt $tankBitmap.Height; $y++) {
                $pixel = $tankBitmap.GetPixel($x, $y)
                if ($pixel.A -ne 255) { throw "T34Tank.png contains transparent pixel at $x,$y." }
                [void]$tankColors.Add($pixel.ToArgb())
            }
        }
        if ($tankColors.Count -ne 8) { throw "T34Tank.png must contain exactly eight opaque palette colors; found $($tankColors.Count)." }
    }
    finally { $tankBitmap.Dispose() }

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
