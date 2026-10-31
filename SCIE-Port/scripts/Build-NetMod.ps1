[CmdletBinding()]
param(
    [string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' }),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDir = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $projectRoot 'src\SCIEPort'
$projectFile = Join-Path $projectDir 'SCIEPort.csproj'
$upstream = Join-Path $projectRoot 'upstream\SCIE'
$artifacts = if ([string]::IsNullOrWhiteSpace($OutputDir)) { Join-Path $projectRoot 'artifacts' } else { $OutputDir }
$build = Join-Path $artifacts 'build'
$stage = Join-Path $artifacts 'package'
$zip = Join-Path $artifacts '荒野探险家.zip'
$package = Join-Path $artifacts '荒野探险家.netmod'

& (Join-Path $PSScriptRoot 'Test-Environment.ps1') -GameDir $GameDir
& (Join-Path $PSScriptRoot 'Test-OmniscientEye.ps1') -GameDir $GameDir
& (Join-Path $PSScriptRoot 'Test-Poseidon.ps1') -GameDir $GameDir
& (Join-Path $PSScriptRoot 'Test-PoseidonV2Assets.ps1') -GameDir $GameDir
& (Join-Path $PSScriptRoot 'Test-PortInventory.ps1') -GameDir $GameDir

# --- .netmod 品牌与打包契约（自 v0.36.0 起，每次打包强制校验）-----------------
# 游戏内显示名固定为「荒野探险家」（英文 Wilderness Explorer，作者 zyan）。
# `SCIE-Port` 只作为仓库工作区目录名与内部标识保留：PackageName、AssemblyName、
# RootNamespace、方块索引、GUID、网络包 ID 与存档兼容逻辑一律不得改中文名。
$modInfoPath = Join-Path $projectDir 'modinfo.json'
$modInfo = Get-Content -Raw -Encoding UTF8 $modInfoPath | ConvertFrom-Json
$projectXml = [xml](Get-Content -Raw -Encoding UTF8 $projectFile)
$propertyGroup = $projectXml.Project.PropertyGroup | Select-Object -First 1

$brandName = '荒野探险家'
$brandAuthor = 'zyan'
$brandEnglish = 'Wilderness Explorer'
$brandPackageName = 'SCIEPort'

if ($modInfo.Name -ne $brandName) {
    throw "游戏内显示名必须是「$brandName」，当前为「$($modInfo.Name)」。"
}
if ($modInfo.Author -ne $brandAuthor) {
    throw "作者字段必须保持 $brandAuthor，当前为 $($modInfo.Author)。"
}
if ($modInfo.PackageName -ne $brandPackageName) {
    throw "PackageName 是内部标识，必须保持 $brandPackageName，当前为 $($modInfo.PackageName)。"
}
if ($propertyGroup.AssemblyName -ne $brandPackageName -or $propertyGroup.RootNamespace -ne $brandPackageName) {
    throw "AssemblyName/RootNamespace 必须保持 $brandPackageName，当前为 $($propertyGroup.AssemblyName)/$($propertyGroup.RootNamespace)。"
}
if ($modInfo.Description -notmatch [regex]::Escape($brandName) -or $modInfo.Description -notmatch [regex]::Escape($brandEnglish)) {
    throw "modinfo Description 必须同时写明中文名「$brandName」与英文名 $brandEnglish。"
}
if ($propertyGroup.Version -ne $modInfo.Version) {
    throw "modinfo Version ($($modInfo.Version)) 必须与 csproj Version ($($propertyGroup.Version)) 一致。"
}
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md'))) {
    throw 'THIRD-PARTY-NOTICES.md 缺失：署名文件必须随包发布。'
}
# 上游原名原样保留：移植/预览等状态后缀写进 docs，不污染物品名与创造栏。
foreach ($brandCsv in Get-ChildItem -LiteralPath (Join-Path $projectDir 'Assets\Blocks') -Filter '*.csv') {
    $csvText = Get-Content -Raw -Encoding UTF8 $brandCsv.FullName
    foreach ($bannedSuffix in @('（移植）', '（授权移植预览）', '(Port)', 'Audit Build', '(Audit Build)')) {
        if ($csvText.Contains($bannedSuffix)) {
            throw "$($brandCsv.Name) 的物品/方块显示名不得带发布状态后缀 $bannedSuffix。"
        }
    }
}
Write-Host "Brand contract: $brandName / $brandEnglish / author $brandAuthor / PackageName $brandPackageName (internal)"
& python (Join-Path $PSScriptRoot 'Test-AbyssGeometry.py')
if ($LASTEXITCODE -ne 0) { throw 'Abyss A3 geometry/runtime asset integrity failed.' }
& (Join-Path $PSScriptRoot 'Test-WorldCut.ps1') -GameDir $GameDir
& (Join-Path $PSScriptRoot 'Test-AbyssReaper.ps1') -GameDir $GameDir
& (Join-Path $PSScriptRoot 'Test-AbyssHud.ps1') -GameDir $GameDir
& (Join-Path $PSScriptRoot 'Test-AbyssSpaceV2.ps1') -GameDir $GameDir
& dotnet run --project (Join-Path $projectRoot 'tests/SCIEPort.Abyss.Tests/SCIEPort.Abyss.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Abyss combat regression failed' }
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-DevouringEye.ps1') -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Devouring Eye native API/model checks failed.' }

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

& (Join-Path $PSScriptRoot 'Test-GungnirAssets.ps1') -GameDir $GameDir
& (Join-Path $PSScriptRoot 'Test-FourSaintsAssets.ps1') -ProjectDir $projectDir
& (Join-Path $PSScriptRoot 'Test-QinglongCrown.ps1') -GameDir $GameDir
& (Join-Path $PSScriptRoot 'Test-FourSaintsArmorV3.ps1') -GameDir $GameDir
$fourSaintsClothingSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\FourSaints\ClothingBlock.cs')
foreach ($clothingCompatibilityRule in @(
    'public sealed class ClothingBlock : Game.ClothingBlock',
    'public new const int Index = 203',
    'NonNullInDisplayOrder')) {
    if (-not $fourSaintsClothingSource.Contains($clothingCompatibilityRule)) {
        throw "Four Saints sparse clothing compatibility guard missing: $clothingCompatibilityRule"
    }
}
$fourSaintsClothingXml = [xml](Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\FourSaints.clo'))
$fourSaintsClothes = @($fourSaintsClothingXml.SelectNodes('//ClothingData'))
if ($fourSaintsClothes.Count -ne 4 -or @($fourSaintsClothes | Where-Object { $_.Layer -ne '10' -or $_.IsOuter -ne 'True' }).Count -ne 0) {
    throw 'All four Four Saints pieces must remain wearable outermost armor at Layer 10.'
}
& $dotnetPath run --project (Join-Path $projectRoot 'tests/SCIEPort.Gungnir.Tests') --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Gungnir rule tests failed' }
& $dotnetPath run --project (Join-Path $projectRoot 'tests/SCIEPort.Poseidon.Tests') --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Poseidon rule/layout tests failed' }
& $dotnetPath run --project (Join-Path $projectRoot 'tests/SCIEPort.Inventory.Tests') --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Inventory category/skin rules failed' }
& $dotnetPath run --project (Join-Path $projectRoot 'tests/SCIEPort.Stormhammer.Tests') --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Stormhammer rule tests failed' }
& $dotnetPath run --project (Join-Path $projectRoot 'tests/SCIEPort.FourSaints.Tests') --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Four Saints rule/layout tests failed' }
$fourSaintsSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\FourSaints\SubsystemFourSaints.cs')
foreach ($coldCause in @('LanguageControl.Get(ComponentVitalStats.fName, 25)', 'LanguageControl.Get(ComponentVitalStats.fName, 26)')) {
    if (-not $fourSaintsSource.Contains($coldCause)) { throw "Four Saints cold immunity is missing exact x25.01.17 cause: $coldCause" }
}
if ($fourSaintsSource.Contains('LanguageControl.Get(ComponentVitalStats.fName, 35)') -or $fourSaintsSource.Contains('ComponentFlu.fName')) {
    throw 'Xuanwu must not suppress heat or flu damage.'
}
$fourSaintsVisualSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\FourSaints\FourSaintsVisuals.cs')
foreach ($visualRule in @('AbsoluteBoneTransformsForCamera', 'camera.InvertedViewMatrix', 'Textures/FourSaintsEffects', 'visibleDistance = quality == 0 ? 32f : 48f')) {
    if (-not $fourSaintsVisualSource.Contains($visualRule)) { throw "Four Saints V2 bone-bound visual rule missing: $visualRule" }
}
$fourSaintsPackageSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\FourSaints\FourSaintsPackage.cs')
foreach ($protocolRule in @('public byte ID => 245', 'AwakenRemaining, CooldownRemaining, RebirthRemaining')) {
    if (-not $fourSaintsPackageSource.Contains($protocolRule)) { throw "Four Saints package 245 compatibility changed: $protocolRule" }
}

$kellyTests = Join-Path $projectRoot 'tests\SCIEPort.KellyPlants.Tests\SCIEPort.KellyPlants.Tests.csproj'
& $dotnetPath run --project $kellyTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Kelly plant rule tests failed: $LASTEXITCODE" }
$atvTests = Join-Path $projectRoot 'tests\SCIEPort.LandVehicles.Tests\SCIEPort.LandVehicles.Tests.csproj'
& $dotnetPath run --project $atvTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "ATV drive rule tests failed: $LASTEXITCODE" }
$offroadTests = Join-Path $projectRoot 'tests\SCIEPort.OffroadVehicles.Tests\SCIEPort.OffroadVehicles.Tests.csproj'
& $dotnetPath run --project $offroadTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Off-road car rule tests failed: $LASTEXITCODE" }
$watercraftTests = Join-Path $projectRoot 'tests\SCIEPort.Watercraft.Tests\SCIEPort.Watercraft.Tests.csproj'
& $dotnetPath run --project $watercraftTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Watercraft rule tests failed: $LASTEXITCODE" }
$airVehicleTests = Join-Path $projectRoot 'tests\SCIEPort.AirVehicles.Tests\SCIEPort.AirVehicles.Tests.csproj'
& $dotnetPath run --project $airVehicleTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Helicopter flight rule tests failed: $LASTEXITCODE" }
$tankTests = Join-Path $projectRoot 'tests\SCIEPort.Tanks.Tests\SCIEPort.Tanks.Tests.csproj'
& $dotnetPath run --project $tankTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Tank drive rule tests failed: $LASTEXITCODE" }
$ecologyTests = Join-Path $projectRoot 'tests\SCIEPort.Ecology.Tests\SCIEPort.Ecology.Tests.csproj'
& $dotnetPath run --project $ecologyTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Ecology rule tests failed: $LASTEXITCODE" }
$cropsTests = Join-Path $projectRoot 'tests\SCIEPort.Crops.Tests\SCIEPort.Crops.Tests.csproj'
& $dotnetPath run --project $cropsTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Corn crop rule tests failed: $LASTEXITCODE" }
$bambooTests = Join-Path $projectRoot 'tests\SCIEPort.Bamboo.Tests\SCIEPort.Bamboo.Tests.csproj'
& $dotnetPath run --project $bambooTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Bamboo rule tests failed: $LASTEXITCODE" }
$flaxTests = Join-Path $projectRoot 'tests\SCIEPort.Flax.Tests\SCIEPort.Flax.Tests.csproj'
& $dotnetPath run --project $flaxTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Flax crop rule tests failed: $LASTEXITCODE" }
$sunflowerTests = Join-Path $projectRoot 'tests\SCIEPort.Sunflower.Tests\SCIEPort.Sunflower.Tests.csproj'
& $dotnetPath run --project $sunflowerTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Sunflower crop rule tests failed: $LASTEXITCODE" }
$appleTests = Join-Path $projectRoot 'tests\SCIEPort.Apple.Tests\SCIEPort.Apple.Tests.csproj'
& $dotnetPath run --project $appleTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Apple plant rule tests failed: $LASTEXITCODE" }
$activationTests = Join-Path $projectRoot 'tests\SCIEPort.VehicleActivation.Tests\SCIEPort.VehicleActivation.Tests.csproj'
& $dotnetPath run --project $activationTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Vehicle activation rule tests failed: $LASTEXITCODE" }
$fortressTests = Join-Path $projectRoot 'tests\SCIEPort.PortableFortress.Tests\SCIEPort.PortableFortress.Tests.csproj'
& $dotnetPath run --project $fortressTests --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Portable fortress rule tests failed: $LASTEXITCODE" }
# Every drivable vehicle must allow the local main player to drive on a client;
# ordinary observers must not run the drive loop. These source guards catch a
# regression to the former Local-only/Client-return gates before packaging.
$driveSources = @(
    'Features\LandVehicles\ComponentAtvDrive.cs',
    'Features\OffroadVehicles\ComponentOffroadCarDrive.cs',
    'Features\Watercraft\ComponentSteamBoatDrive.cs',
    'Features\Watercraft\ComponentAirshipDrive.cs',
    'Features\AirVehicles\ComponentHelicopterDrive.cs',
    'Features\Rail\ComponentRailVehicle.cs',
    'Features\Tanks\ComponentTankDrive.cs'
)
foreach ($relativeDriveSource in $driveSources) {
    $driveSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir $relativeDriveSource)
    if (-not $driveSource.Contains('PlayerData.IsMainPlayer')) {
        throw "$relativeDriveSource does not gate drive simulation to the local main player."
    }
    if (-not $driveSource.Contains('VehicleActivationService.EnsureActivated')) {
        throw "$relativeDriveSource does not require the vehicle activation session, so an already-spawned vehicle could be driven without authorization."
    }
    if ($driveSource -match 'WorkType\s*!=\s*WorkType\.Local\)\s*return' -or
        $driveSource -match 'WorkType\s*==\s*WorkType\.Client\)\s*\{\s*return;') {
        throw "$relativeDriveSource regressed to a single-player-only drive gate."
    }
}
$spawnSources = @(
    'Features\LandVehicles\SubsystemAtvBlockBehavior.cs',
    'Features\OffroadVehicles\SubsystemOffroadCarBlockBehavior.cs',
    'Features\Watercraft\SubsystemSteamBoatBlockBehavior.cs',
    'Features\Watercraft\SubsystemAirshipBlockBehavior.cs',
    'Features\AirVehicles\SubsystemHelicopterBlockBehavior.cs',
    'Features\Rail\SubsystemRailVehicleBlockBehavior.cs',
    'Features\Tanks\SubsystemTankBlockBehavior.cs'
)
foreach ($relativeSpawnSource in $spawnSources) {
    $spawnSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir $relativeSpawnSource)
    if (-not $spawnSource.Contains('VehicleActivationService.EnsureActivated')) {
        throw "$relativeSpawnSource does not gate deployment behind the vehicle activation session."
    }
    $activationIndex = $spawnSource.IndexOf('VehicleActivationService.EnsureActivated')
    $consumeIndex = $spawnSource.IndexOf('RemoveActiveTool(1)')
    if ($consumeIndex -ge 0 -and $activationIndex -gt $consumeIndex) {
        throw "$relativeSpawnSource must reject an unauthorized attempt before RemoveActiveTool so no item is consumed."
    }
}
$railBlockSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Rail\RailVehicleBlocks.cs')
if ($railBlockSource.Contains('FindMesh(') -or -not $railBlockSource.Contains('foreach (ModelMesh mesh in model.Meshes)')) {
    throw 'Rail vehicle inventory rendering must enumerate every imported model mesh.'
}
$tankDriveSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Tanks\ComponentTankDrive.cs')
$tankDriveUpdateIndex = $tankDriveSource.IndexOf('UpdateDrive(t, rider, input, throttle, terrainGrounded, bodyGrounded);')
$tankFireGateIndex = $tankDriveSource.IndexOf('bool wantsFire = TankRules.CanFire')
if ($tankDriveUpdateIndex -lt 0 -or $tankFireGateIndex -lt 0 -or $tankDriveUpdateIndex -gt $tankFireGateIndex -or
    -not $tankDriveSource.Contains('* TankRules.RecoilSpeed')) {
    throw 'Tank recoil must be applied by the successful shot path after drive integration.'
}
$tankActivationIndex = $tankDriveSource.IndexOf('VehicleActivationService.EnsureActivated')
if ($tankActivationIndex -lt 0 -or $tankActivationIndex -gt $tankFireGateIndex) {
    throw 'Tank drive must require the vehicle activation session before the main-gun fire path so an unactivated tank cannot shoot.'
}
$projectileRemovedBody = [regex]::Match($tankDriveSource, '(?s)private void OnProjectileRemoved\(Projectile projectile\)(.*?)private void OnShellHit').Groups[1].Value
$shellHitBody = [regex]::Match($tankDriveSource, '(?s)private void OnShellHit\(Projectile projectile, CellFace\? cellFace, ComponentBody hitBody\)(.*?)private void ExplodeShell').Groups[1].Value
if ($projectileRemovedBody.Contains('ExplodeShell') -or -not $shellHitBody.Contains('ExplodeShell(state, impactPosition)')) {
    throw 'Tank shells must explode only on first physical impact, never on ordinary projectile removal.'
}
$tankRulesSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Tanks\TankRules.cs')
foreach ($removedRangeGate in @('ShellArmingDistance', 'ShellVehicleSafetyClearance', 'CanShellExplode')) {
    if ($tankRulesSource.Contains($removedRangeGate)) {
        throw "Tank shell range gate $removedRangeGate must not suppress close-impact explosions."
    }
}
$seatManagerSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Vehicles\ComponentTwoSeatManager.cs')
if (-not $seatManagerSource.Contains('MoveDriverBodyToFront') -or -not $seatManagerSource.Contains('m_childBodies.Insert(0, driverBody)')) {
    throw 'Two-seat vehicles must keep the driver first for native parent-body network authority.'
}
if (-not $seatManagerSource.Contains('SuppressLocalPassengerBodyEcho') -or
    -not $seatManagerSource.Contains('m_body.SendPosition = null') -or
    -not $seatManagerSource.Contains('UpdateOrder.CreatureModels')) {
    throw 'Two-seat passenger peers must suppress delayed parent-body echo after ComponentBody.Update.'
}
$helicopterDriveSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\AirVehicles\ComponentHelicopterDrive.cs')
foreach ($requiredPresentationSource in @(
    'CreateSound("Audio/SCIEPortHelicopterLoop")',
    'FindBone("VisualRoot"',
    'FindBone("MainRotorBlur"',
    'm_rotorSound.Dispose()',
    'UpdatePresentation(t, grounded)')) {
    if (-not $helicopterDriveSource.Contains($requiredPresentationSource)) {
        throw "Helicopter V0.4 presentation lifecycle is missing source evidence: $requiredPresentationSource"
    }
}
if ($helicopterDriveSource.Contains('CreateSound("Audio/SCIEPortHelicopterLoop")') -and
    (-not $helicopterDriveSource.Contains('m_rotorSound.DisposeOnStop = false') -or
     -not $helicopterDriveSource.Contains('m_rotorSound.Stop()'))) {
    throw 'Helicopter rotor sound must remain reusable while active and be stopped before component disposal.'
}
foreach ($driverInputGuard in @('DriverJump', 'DriverSneakMove', 'm_driverJump = input.Jump', 'm_driverSneakMove = input.SneakMove')) {
    if (-not $seatManagerSource.Contains($driverInputGuard)) {
        throw "Two-seat input cache is missing helicopter driver input guard: $driverInputGuard"
    }
}
$loaderSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Mod\SCIEPortLoader.cs')
foreach ($packageType in @('TwoSeatAssignmentPackage', 'TankControlPackage', 'VehicleActivationPackage')) {
    if (-not $loaderSource.Contains("RegisterPackage(new $packageType())")) {
        throw "SCIEPortLoader does not register $packageType."
    }
}
$tankControlSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Tanks\TankControlPackage.cs')
if (-not $tankControlSource.Contains('VehicleActivationService.IsGranted(From.GUID)')) {
    throw 'TankControlPackage must require the host-side per-client activation grant before applying remote control or fire.'
}
& $dotnetPath run --project (Join-Path $projectRoot 'tests\SCIEPort.OmniscientEye.Tests\SCIEPort.OmniscientEye.Tests.csproj') --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Omniscient Eye layout tests failed: $LASTEXITCODE" }
& $dotnetPath run --project (Join-Path $projectRoot 'tests\SCIEPort.DevouringEye.Tests\SCIEPort.DevouringEye.Tests.csproj') --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Devouring Eye safety/transaction tests failed.' }

# V0.5 must package the analog cockpit, never the former two-button interface.
$hudWidgetSource = Get-Content -Raw (Join-Path $projectDir 'Features\AirVehicles\Hud\HelicopterFlightHud.cs')
foreach ($evidence in @('COLLECTIVE', 'HOVER ON', 'ALT / M', 'VS / M/S', 'SPD / M/S', 'CanvasWidget.SetPosition')) {
    if (-not $hudWidgetSource.Contains($evidence)) { throw "Collective cockpit missing: $evidence" }
}
if ($hudWidgetSource.Contains('IsClimbPressed') -or $hudWidgetSource.Contains('IsDivePressed')) {
    throw 'Legacy helicopter ascent/descent buttons must not return.'
}
$helicopterHudDriveSource = Get-Content -Raw (Join-Path $projectDir 'Features\AirVehicles\ComponentHelicopterDrive.cs')
foreach ($evidence in @('m_hover.Update(', 'm_flightInput.Update(', 'grounded ? Vector3.Zero', 'm_hud.Detach()', 'ComponentHealth.Health', 'ParentBody != m_body')) {
    if (-not $helicopterHudDriveSource.Contains($evidence)) { throw "Flight integration missing: $evidence" }
}
# Invoke the installed SDK's MSBuild directly. This net472 project does not need
# the repository-wide SDK pinned by global.json, and direct invocation keeps the
# port build usable when that optional SDK is not installed locally.
& $dotnetPath $sdkMsBuild.FullName $projectFile /restore /t:Build "/p:Configuration=$Configuration" "/p:OutputPath=$build\" "/p:SCNetGameDir=$GameDir" /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed: $LASTEXITCODE" }
& (Join-Path $PSScriptRoot 'Test-PoseidonRuntime.ps1') -GameDir $GameDir -BuiltAssembly (Join-Path $build 'SCIEPort.dll')
& (Join-Path $PSScriptRoot 'Test-PortInventory.ps1') -GameDir $GameDir -BuiltAssembly (Join-Path $build 'SCIEPort.dll')

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-HelicopterHud.ps1') -GameDir $GameDir -ModDll (Join-Path $build 'SCIEPort.dll')
if ($LASTEXITCODE -ne 0) { throw 'Target-assembly helicopter HUD/physics tests failed.' }
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-VoidHorizon.ps1') -GameDir $GameDir -AssemblyPath (Join-Path $build 'SCIEPort.dll')
if ($LASTEXITCODE -ne 0) { throw 'Abyss native input/domain regression' }
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-WorldCut.ps1') -GameDir $GameDir -AssemblyPath (Join-Path $build 'SCIEPort.dll')
if ($LASTEXITCODE -ne 0) { throw 'WorldCut native regression failed' }
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-AbyssSpaceV2.ps1') -GameDir $GameDir -AssemblyPath (Join-Path $build 'SCIEPort.dll')
if ($LASTEXITCODE -ne 0) { throw 'Abyss Space V2 native authority/landing/damage regression failed' }
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-AbyssHost.ps1') -GameDir $GameDir -AssemblyPath (Join-Path $build 'SCIEPort.dll')
if ($LASTEXITCODE -ne 0) { throw 'Abyss host targeting/input/portal regression failed.' }
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-VoidPrototype.ps1') -GameDir $GameDir -AssemblyPath (Join-Path $build 'SCIEPort.dll')
if ($LASTEXITCODE -ne 0) { throw 'Void prototype API/safety regression failed' }

& (Join-Path $PSScriptRoot 'Test-Database.ps1')
if ($LASTEXITCODE -ne 0) { throw "Database validation failed: $LASTEXITCODE" }

# Portable Fortress registration guard. The three blocks are the only terrain /
# inventory footprint of the extension, so a wrong flag here would either show an
# invisible block in the creative inventory or let a player hand-place structure.
$fortressCsv = Join-Path $projectDir 'Assets\Blocks\PortableFortress.csv'
$fortressRows = @(Import-Csv -LiteralPath $fortressCsv -Delimiter ';' -Encoding UTF8)
$fortressItem = @($fortressRows | Where-Object { $_.'Class Name' -eq 'PortableFortressCoreItemBlock' })
$fortressCore = @($fortressRows | Where-Object { $_.'Class Name' -eq 'PortableFortressCoreBlock' })
$fortressThreshold = @($fortressRows | Where-Object { $_.'Class Name' -eq 'PortableFortressThresholdBlock' })
if ($fortressItem.Count -ne 1 -or $fortressCore.Count -ne 1 -or $fortressThreshold.Count -ne 1) {
    throw 'PortableFortress.csv must declare exactly one item, one control core and one threshold row.'
}
if ($fortressItem[0].Behaviors -ne 'SCIEPortPortableFortressBehavior') {
    throw 'The fortress deploy item must be handled by SCIEPortPortableFortressBehavior.'
}
if ($fortressItem[0].IsCollidable -ne 'FALSE' -or $fortressItem[0].IsPlaceable -ne 'FALSE') {
    throw 'The fortress deploy item must stay non-collidable and non-placeable.'
}
if ($fortressCore[0].IsCollidable -ne 'FALSE') {
    throw 'The control core must be walk-through so it never blocks the rear door.'
}
if ($fortressThreshold[0].IsCollidable -ne 'TRUE' -or $fortressThreshold[0].IsPlaceable -ne 'FALSE') {
    throw 'The hidden threshold must be collidable but never hand-placeable.'
}
$fortressBlockIds = @()
foreach ($csvFile in @(Get-ChildItem -LiteralPath (Join-Path $projectDir 'Assets\Blocks') -Filter *.csv -File)) {
    foreach ($row in @(Import-Csv -LiteralPath $csvFile.FullName -Delimiter ';' -Encoding UTF8)) {
        $fortressBlockIds += , @($row.'Class Name', $csvFile.Name, $row.DisplayOrder)
    }
}
foreach ($blockClass in @('PortableFortressCoreItemBlock', 'PortableFortressCoreBlock', 'PortableFortressThresholdBlock')) {
    $sameClass = @($fortressBlockIds | Where-Object { $_[0] -eq $blockClass })
    if ($sameClass.Count -ne 1) { throw "$blockClass must appear in exactly one block CSV." }
}
$lightningSceneSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\LightningScene.cs')
if (-not $lightningSceneSource.Contains('QueueRibbon')) {
    throw 'LightningScene must expand bolts into ribbon quads.'
}
if (-not $lightningSceneSource.Contains('LightningRune')) {
    throw 'LightningScene must render the ground rune seal.'
}
$lightningRulesSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\LightningRules.cs')
if (-not $lightningRulesSource.Contains('BuildPath')) {
    throw 'LightningRules must build continuous bolt paths.'
}
$awakenedSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\SubsystemAwakenedStormhammerBlockBehavior.cs')
if ($awakenedSource.Contains('UltimateTarget')) {
    throw 'The V3.2 multi-target ultimate sweep must not come back; V4 uses one main strike.'
}
if (-not $awakenedSource.Contains('UpdatePendingJudgment')) {
    throw 'Thunderous Judgment must resolve on its authoritative charge timeline.'
}
if (-not $awakenedSource.Contains('LightningRules.TribulationDamage')) {
    throw 'The main strike must use the V4 damage table.'
}
$thunderGodSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\SubsystemAwakenedStormhammerBlockBehavior.cs')
if (-not $thunderGodSource.Contains('ThunderGodDuration = 12f')) {
    throw 'The Thunder God window must stay at 12 seconds.'
}
if (-not $thunderGodSource.Contains('ThunderGodPowerMultiplier = 2f')) {
    throw 'The Thunder God lightning multiplier must stay at 2x.'
}
if (-not $thunderGodSource.Contains('SetCooldown(id, StormhammerSkill.ThunderousJudgment, 0f);')) {
    throw 'Casting the ultimate must refresh the Thunderous Judgment cooldown, not the ultimate cooldown.'
}
# Physical melee power is handed to the native path verbatim; the multiplier may only
# ever reach the delayed lightning add-on.
if ($thunderGodSource -notmatch [regex]::Escape('attackPower = PhysicalMeleePower;')) {
    throw 'The awakened melee power must be passed to the native path unmodified.'
}
if ($thunderGodSource -match 'attackPower\s*=\s*[^;]*ThunderGodPowerMultiplier') {
    throw 'Physical melee damage must never be multiplied by the Thunder God window.'
}
if ($thunderGodSource -notmatch 'DiagnosticDamage\("retaliation"[^;]*RetaliationPower, 1f') {
    throw 'The fixed 5 point retaliation must never be multiplied by the Thunder God window.'
}
if ($thunderGodSource -match '"终焉主雷"[^;]*ThunderGodPowerMultiplier' -or $thunderGodSource -match '"雷霆余震"[^;]*ThunderGodPowerMultiplier') {
    throw 'The ultimate main strike and its aftershock must not benefit from their own buff.'
}
$visualsSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\AwakenedStormhammerVisuals.cs')
if (-not $visualsSource.Contains('BuildStormSchedule')) {
    throw 'The backdrop storm must be driven by the deterministic schedule.'
}
if ($visualsSource.Contains('EmitSkyBolt')) {
    throw 'The particle sky bolt must not return; V4 draws real geometry.'
}
if (-not $visualsSource.Contains('StormReleaseTime = 12.0f')) {
    throw 'The storm sky and backdrop must cover the 12 s Thunder God window.'
}
if (-not $visualsSource.Contains('s_overcharge') -or -not $visualsSource.Contains('NextFootstep')) {
    throw 'The overcharge avatar arcs and lightning footprints must exist.'
}
if (-not $visualsSource.Contains('BuildStormSchedule(seed, phases)')) {
    throw 'The backdrop storm must use the phased density schedule.'
}
$hudSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\StormhammerSkillHud.cs')
# --- Mjolnir rework guards -------------------------------------------------
if (-not $awakenedSource.Contains('JudgmentCooldown = 8f')) {
    throw 'Thunderous Judgment must stay on the shortened 8 s cooldown.'
}
$equipmentRulesSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\StormhammerEquipmentRules.cs')
foreach ($equipmentRule in @('MovementFactor = 1.2f', 'AttackSpeedFactor = 1.1f', 'NativeAttackInterval / AttackSpeedFactor')) {
    if (-not $equipmentRulesSource.Contains($equipmentRule)) { throw "Mjolnir equipment bonus missing: $equipmentRule" }
}
if (-not $awakenedSource.Contains('ApplyEquipmentModifiers()') -or -not $awakenedSource.Contains('miner.m_lastHitTime -= StormhammerEquipmentRules.LastHitTimeAdvance')) {
    throw 'Mjolnir movement or native melee-speed application is missing.'
}
$gungnirRulesSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Gungnir\GungnirRules.cs')
$gungnirBehaviorSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Gungnir\SubsystemGungnir.cs')
foreach ($gungnirRule in @('BlessingCooldown = 8f', 'JudgmentCooldown = 50f', 'HolyDamageDelay = 0.4f', 'ShouldConsumeAim')) {
    if (-not $gungnirRulesSource.Contains($gungnirRule)) { throw "Gungnir V5.2 control/balance rule missing: $gungnirRule" }
}
foreach ($gungnirBehaviorRule in @('public override bool OnAim(Ray3 aim, ComponentMiner miner, AimState state) => false;', 'UpdateThrowInput(p, s)', 'ComponentAimingSights.ShowAimingSights', 'GungnirRules.ThrowLease', 'ComponentHealthPackage(position, velocity, color, text)', 'new Color(255, 205, 92, 255)')) {
    if (-not $gungnirBehaviorSource.Contains($gungnirBehaviorRule)) { throw "Gungnir aim, throw reliability or divine hit feedback missing: $gungnirBehaviorRule" }
}
# v0.40.5: badges are icon-only. Skill names and the former engraved nameplate must not return.
$gungnirHudSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Gungnir\GungnirHud.cs')
foreach ($retiredNameplateRule in @('卢恩神佑', '永恒裁决', 'NamePlate', 'Caption', 'NameBandHeight')) {
    if ($gungnirHudSource.Contains($retiredNameplateRule)) { throw "Retired Gungnir skill-name UI returned: $retiredNameplateRule" }
}
# v0.40.1: Game.Random.Int(min, max) is INCLUSIVE on both ends. Rolling Int(0, 16) for a local
# chunk cell returns 16 about once in seventeen rolls and reads past the chunk arrays
# (IndexOutOfRangeException on a fresh world). ChanceMet consumes a 0..99 band, so Int(0, 100)
# silently disables the spawn. Both ranges are pinned by the shared EcologyRules constants.
$creatureSpawnSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Creatures\CreatureWorldSpawn.cs')
foreach ($creatureSpawnRule in @(
    'EcologyRules.LastChunkCellIndex',
    'EcologyRules.LastChanceRoll',
    'EcologyRules.IsInsideChunk',
    'EcologyRules.ClampChunkCell',
    'chunk.State < TerrainChunkState.InvalidLight'
)) {
    if (-not $creatureSpawnSource.Contains($creatureSpawnRule)) { throw "Creature spawner range/queue safety missing: $creatureSpawnRule" }
}
foreach ($badRoll in @('Int(0, 16)', 'Int(0, 100)')) {
    if ($creatureSpawnSource.Contains($badRoll)) { throw "Creature spawner uses the inclusive out-of-range roll $badRoll." }
}
$loaderSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Mod\SCIEPortLoader.cs')
if (-not $loaderSource.Contains('CreatureWorldSpawn.Update();')) { throw 'Natural creatures must be drained after chunk generation, not created inside it.' }
$ecologyRulesSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Ecology\EcologyRules.cs')
foreach ($ecologyRange in @('LastChunkCellIndex = 15', 'LastChanceRoll = 99')) {
    if (-not $ecologyRulesSource.Contains($ecologyRange)) { throw "Ecology spawn range constant changed: $ecologyRange" }
}
# v0.40.4: the `.clo` merge can silently drop outfit entries, so clothing must also be
# registered from code after blocks initialize. Both the fallback and its call site are pinned.
$fourSaintsClothingSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\FourSaints\FourSaintsClothing.cs')
foreach ($clothingRule in @(
    'FourSaintsRules.QinglongIndex',
    'FourSaintsRules.XuanwuIndex',
    'FourSaintsRules.BaihuIndex',
    'FourSaintsRules.ZhuqueIndex',
    'ContentManager.Get<Texture2D>',
    'block.m_clothingData[spec.Index] = new ClothingData'
)) {
    if (-not $fourSaintsClothingSource.Contains($clothingRule)) { throw "Four Saints clothing fallback registration missing: $clothingRule" }
}
if (-not $loaderSource.Contains('FourSaintsClothing.EnsureRegistered();')) {
    throw 'Four Saints clothing fallback must run after blocks initialize.'
}
$artifactDisplaySource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\MjolnirDisplay.cs')
foreach ($slotRule in @('PortInventoryRules.Select', 'PortInventoryRules.Colors', 'GetClothingIndex', 's_seen.Contains(slot)', 'RestoreColors', 'ResetAll();s_project=project;')) {
    if (-not $artifactDisplaySource.Contains($slotRule)) { throw "Unified equipment slot lifecycle missing: $slotRule" }
}
if (-not $visualsSource.Contains('MainStrikeTime = 0.28f')) {
    throw 'The main bolt must land almost immediately (0.28 s) instead of after a long wind-up.'
}
if (-not $visualsSource.Contains('BuildGreatBoltSchedule')) {
    throw 'The heavy bolt layer is missing: the ultimate would only show thin backdrop strikes.'
}
if (-not $visualsSource.Contains('GreatSchedule') -or -not $visualsSource.Contains('state.MainSpawned && state.GreatSchedule')) {
    throw 'Heavy bolts must be gated on the main strike having landed.'
}
if (-not $lightningRulesSource.Contains('BuildGreatBoltSchedule')) {
    throw 'The great bolt schedule must be a deterministic rule, not ad hoc spawning.'
}
$awakenedBlockSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\StormhammerAwakenedBlock.cs')
if (-not $awakenedBlockSource.Contains('ContentManager.Get<Model>("Models/Stormhammer")')) {
    throw 'Mjolnir must reuse the base Stormhammer mesh; its identity comes from effects, not a second model.'
}
if ($awakenedBlockSource.Contains('Models/StormhammerAwakened')) {
    throw 'The old awakened DAE must not be loaded again.'
}
$hammerSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\StormhammerVisuals.cs')
if (-not $hammerSource.Contains('DriveCorona')) {
    throw 'The Mjolnir corona (orbit ring plus crackle arcs) is missing.'
}
if (-not $hammerSource.Contains('GetScene')) {
    throw 'The corona must emit real bolts through the shared scene, not particles.'
}
if ($hudSource.Contains('雷霆万钧') -or $hudSource.Contains('万象雷劫')) {
    throw 'The HUD must not render skill names any more.'
}
if ($hudSource -match 'OutlineThickness = size') {
    throw 'The HUD must not draw the cyan outline frame any more.'
}
foreach ($csvFile in @('Stormhammer.csv', 'StormhammerAwakened.csv')) {
    $rows = @(Import-Csv -LiteralPath (Join-Path (Join-Path $projectDir 'Assets\Blocks') $csvFile) -Delimiter ';' -Encoding UTF8)
    if ($rows.Count -lt 1) { throw "$csvFile is empty." }
}
if (-not (Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Blocks\Stormhammer.csv')).Contains('雷神之锤·伪')) {
    throw 'The base hammer must be named 雷神之锤·伪.'
}
if (-not (Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Blocks\StormhammerAwakened.csv')).Contains('雷神之锤·妙尔尼尔')) {
    throw 'The awakened hammer must be named 雷神之锤·妙尔尼尔.'
}
# --- Slot skin, hit values and the fake hammer add-on -----------------------
# NOTE: read the behavior source locally; $awakenedBehavior is only defined much later
# in the V3.2 contract block and would be $null here.
$displayBehavior = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\SubsystemAwakenedStormhammerBlockBehavior.cs')
$mjolnirDisplay = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\MjolnirDisplay.cs')
if (-not $mjolnirDisplay.Contains('PortInventoryRules.Select')) {
    throw 'Slot skins must use the unified data-aware equipment registry.'
}
if (-not $mjolnirDisplay.Contains('BevelColor')) {
    throw 'MjolnirDisplay must restore the bevel it overrides.'
}
if (-not $mjolnirDisplay.Contains('ResetAll')) {
    throw 'MjolnirDisplay must be able to undo its paints when the subsystem dies.'
}
foreach ($displayRule in @('BaseHammerLightningPower = 5f','LightningDisplayDelay = 0.4f')) {
    if (-not $displayBehavior.Contains($displayRule)) { throw "Stormhammer display rule missing: $displayRule" }
}
if (-not $displayBehavior.Contains('ShowHitValue')) {
    throw 'The lightning add-on must spawn a visible hit value; health.Injure alone shows nothing.'
}
if (-not $displayBehavior.Contains('ComponentHealthPackage')) {
    throw 'Remote clients must receive the hit value package.'
}
if (-not $displayBehavior.Contains('contents == StormhammerBlock.Index')) {
    throw 'The fake hammer melee add-on is missing.'
}
if (-not $displayBehavior.Contains('MjolnirDisplay.Update')) {
    throw 'The slot decorator must be driven from the subsystem update loop.'
}
if (-not $displayBehavior.Contains('MjolnirDisplay.ResetAll')) {
    throw 'Slot skins must be restored on dispose.'
}
if (-not $hudSource.Contains('GetThunderGodRemaining')) {
    throw 'The HUD must surface the Thunder God countdown.'
}
$fortressSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\PortableFortress\PortableFortressBlocks.cs')
foreach ($expectedIndex in @('Index = 944', 'Index = 945', 'Index = 946')) {
    if (-not $fortressSource.Contains($expectedIndex)) { throw "Portable fortress block id drifted: $expectedIndex" }
}
$fortressGeometry = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\PortableFortress\PortableFortressGeometry.cs')
if (-not $fortressGeometry.Contains('FloorTop = 0.36f')) {
    throw 'The level-1 walking surface must stay at 0.36 so the player never floats above the foundation.'
}
if (-not $fortressGeometry.Contains('MaxSegmentHalfDiagonal = 4f')) {
    throw 'Collision proxies must stay small enough for SubsystemBodies.FindBodiesAroundPoint(4f).'
}
$fortressDeploy = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\PortableFortress\FortressDeploymentRules.cs')
if (-not $fortressDeploy.Contains('MaxTerrainCellsPerDeployment = 4')) {
    throw 'Deployment must stay limited to three threshold cells and the core cell.'
}
$fortressBehavior = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\PortableFortress\SubsystemPortableFortressBlockBehavior.cs')
if (-not $fortressBehavior.Contains('WorkType.Client')) {
    throw 'Fortress deployment must refuse non-authoritative multiplayer clients.'
}
if ($fortressBehavior.IndexOf('FortressDeploymentRules.Evaluate') -gt $fortressBehavior.IndexOf('m_fortresses.TryBeginDeploy')) {
    throw 'The preflight must run before any fortress state is created.'
}

# --- Hearthguard art pass guards -------------------------------------------
# The entrance fix depends on three things staying true together. Each one was a
# real defect at some point, so each is asserted rather than reviewed by eye.
if (-not $fortressGeometry.Contains('StairRise = 0.435f')) {
    throw 'StairRise must be (DeckTop - FloorTop) / 8 = 0.435 so the last tread is flush with the deck.'
}
if (-not $fortressGeometry.Contains('DoorInnerZ = 6.20f')) {
    throw 'The level-1 floor must stop at the inner face of the rear wall (6.20) or it puts a 0.36 body lip across the doorway.'
}
if (-not $fortressGeometry.Contains('FortressZone.Glazing')) {
    throw 'The glazing proxies are missing: the observation windows would be walk-through holes.'
}
if (-not $fortressGeometry.Contains('EntrySillMaxZ = RearFaceZ')) {
    throw 'The bounded entrance sill assist must stay on the hidden threshold cells.'
}
if (-not $fortressDeploy.Contains('MaxRiseSpeed = 4.5f')) {
    throw 'The rise assist must stay clamped to the native 4.5 m/s smooth rise.'
}
if (-not $fortressDeploy.Contains('DoorHalfClearanceX')) {
    throw 'The entrance sill assist must be limited to the doorway width.'
}
$fortressGlazing = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\PortableFortress\FortressGlazingRenderer.cs')
if (-not $fortressGlazing.Contains('BlendState.AlphaBlend')) {
    throw 'The glazing pass must use alpha blending; the component model shader has no blended pass.'
}
if (-not $fortressGlazing.Contains('DepthStencilState.DepthRead')) {
    throw 'The glazing pass must not write depth or it would clip the world behind the pane.'
}
$fortressOptions = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\PortableFortress\FortressOptions.cs')
if (-not $fortressOptions.Contains('public static bool Diagnostics')) {
    throw 'The entrance trace switch must exist so an Android run can be diagnosed without a rebuild.'
}
if (-not (Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\PortableFortress\ComponentPortableFortress.cs')).Contains('EmissionColor')) {
    throw 'The fortress model needs its low warm emission floor or the interior is unreadable at night.'
}

# The atlas must be a real 1024 atlas with every declared region painted; a regression
# to the old 64x64 single-panel texture would silently turn the fortress dark again.
Add-Type -AssemblyName System.Drawing
$fortressAtlasPath = Join-Path $projectDir 'Assets\Textures\PortableFortress.png'
$fortressAtlas = [Drawing.Bitmap]::FromFile($fortressAtlasPath)
try {
    if ($fortressAtlas.Width -ne 1024 -or $fortressAtlas.Height -ne 1024) {
        throw "PortableFortress.png must be the 1024x1024 UV atlas, got $($fortressAtlas.Width)x$($fortressAtlas.Height)."
    }
    $fortressColours = @{}
    for ($y = 0; $y -lt 1024; $y += 16) {
        for ($x = 0; $x -lt 1024; $x += 16) {
            $pixel = $fortressAtlas.GetPixel($x, $y)
            if ($pixel.A -ne 255) { throw "PortableFortress.png must be fully opaque; found alpha $($pixel.A) at $x,$y." }
            $fortressColours[('{0},{1},{2}' -f $pixel.R, $pixel.G, $pixel.B)] = $true
        }
    }
    if ($fortressColours.Count -lt 48) {
        throw "PortableFortress.png has only $($fortressColours.Count) distinct colours over a 16 px grid; the per-material regions are not painted."
    }
}
finally { $fortressAtlas.Dispose() }
foreach ($paneFile in @('FortressGlass.png', 'FortressGlow.png')) {
    $paneBitmap = [Drawing.Bitmap]::FromFile((Join-Path (Join-Path $projectDir 'Assets\Textures') $paneFile))
    try {
        if ($paneBitmap.Width -ne 64 -or $paneBitmap.Height -ne 64) { throw "$paneFile must be 64x64." }
        $translucent = 0
        for ($y = 0; $y -lt 64; $y += 4) {
            for ($x = 0; $x -lt 64; $x += 4) {
                if ($paneBitmap.GetPixel($x, $y).A -lt 200) { $translucent++ }
            }
        }
        if ($translucent -lt 16) { throw "$paneFile must carry real translucent pixels, not a solid plate." }
    }
    finally { $paneBitmap.Dispose() }
}


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

# The authorized Minecart.dae encodes VERTEX/NORMAL/TEXCOORD with the same
# polygon offset and a single shared index stream. x25.01.17's actual Collada
# loader expects one interleaved index per declared input and otherwise throws
# ArgumentOutOfRangeException in Collada.LoadPolygons, leaving the carriage
# invisible. Produce a deterministic package-only compatibility derivative;
# the ignored upstream tree remains read-only and byte-identical.
$stagedMinecart = Join-Path $stage 'Assets\Models\Minecart.dae'
[xml]$minecartDerivative = Get-Content -Raw -Encoding UTF8 $stagedMinecart
$minecartNamespaces = New-Object Xml.XmlNamespaceManager($minecartDerivative.NameTable)
$minecartNamespaces.AddNamespace('c', 'http://www.collada.org/2005/11/COLLADASchema')
$minecartPolylists = @($minecartDerivative.SelectNodes('//c:geometry/c:mesh/c:polylist', $minecartNamespaces))
if ($minecartPolylists.Count -ne 1) {
    throw "Authorized Minecart.dae must contain exactly one polylist; found $($minecartPolylists.Count)."
}
foreach ($polylist in $minecartPolylists) {
    $inputs = @($polylist.SelectNodes('./c:input', $minecartNamespaces))
    if ($inputs.Count -ne 3 -or ($inputs | ForEach-Object { $_.GetAttribute('semantic') }) -join ',' -ne 'VERTEX,NORMAL,TEXCOORD') {
        throw 'Authorized Minecart.dae polylist inputs differ from the audited VERTEX/NORMAL/TEXCOORD layout.'
    }
    $indicesNode = $polylist.SelectSingleNode('./c:p', $minecartNamespaces)
    $indices = @($indicesNode.InnerText -split '\s+' | Where-Object { $_ })
    $vertexCount = (@($polylist.SelectSingleNode('./c:vcount', $minecartNamespaces).InnerText -split '\s+' | Where-Object { $_ }) |
        Measure-Object -Sum).Sum
    $offsets = @($inputs | ForEach-Object { $_.GetAttribute('offset') })
    if (($offsets -join ',') -eq '0,0,0' -and $indices.Count -eq $vertexCount) {
        for ($i = 0; $i -lt $inputs.Count; $i++) { $inputs[$i].SetAttribute('offset', [string]$i) }
        $indicesNode.InnerText = (($indices | ForEach-Object { "$_ $_ $_" }) -join ' ')
    }
    elseif (($offsets -join ',') -ne '0,1,2' -or $indices.Count -ne 3 * $vertexCount) {
        throw 'Minecart.dae index layout is neither the audited upstream form nor the normalized SCNET form.'
    }
}
$minecartWriterSettings = New-Object Xml.XmlWriterSettings
$minecartWriterSettings.Indent = $true
$minecartWriterSettings.Encoding = New-Object Text.UTF8Encoding($false)
$minecartWriter = [Xml.XmlWriter]::Create($stagedMinecart, $minecartWriterSettings)
try { $minecartDerivative.Save($minecartWriter) }
finally { $minecartWriter.Dispose() }

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
    foreach ($resource in @('Assets/Models/AbyssReaper.dae','Assets/Textures/AbyssReaper.png','Assets/Blocks/AbyssReaper.csv','Assets/Database/AbyssReaper.xdb')) { if ($entries -notcontains $resource) { throw "Missing Abyss resource: $resource" } }
    foreach ($requiredEntry in @(
        'Assets/Models/PoseidonTrident.dae',
        'Assets/Textures/PoseidonTrident.png',
        'Assets/Textures/PoseidonSkills.png',
        'Assets/Textures/PoseidonTideShieldV2.png',
        'Assets/Textures/PoseidonTidalWaveV2.png',
        'Assets/Textures/PoseidonSeaDomainV2.png',
        'Assets/Blocks/Poseidon.csv',
        'Assets/Database/Poseidon.xdb',
        'Assets/Audio/Abyss/BlinkShift.wav',
        'Assets/Audio/Abyss/BlinkHunt.wav',
        'Assets/Audio/Abyss/HorizonWake.wav',
        'Assets/Audio/Abyss/HorizonRift.wav',
        'Assets/Audio/Abyss/HorizonCollapse.wav',
        'Assets/Audio/Abyss/HorizonBurst.wav',
        'Assets/Audio/Poseidon/Surge.wav',
        'Assets/Audio/Poseidon/Tsunami.wav',
        'SCIEPort.dll',
        'Assets/Models/OmniscientEye.dae',
        'Assets/Models/DevouringEye.dae',
        'Assets/Textures/DevouringEye.png',
        'Assets/Blocks/DevouringEye.csv',
        'Assets/Database/DevouringEye.xdb',
        'Assets/Textures/OmniscientEye.png',
        'Assets/Blocks/OmniscientEye.csv',
        'Assets/Database/OmniscientEye.xdb',
        'modinfo.json',
        'THIRD-PARTY-NOTICES.md',
        'Assets/Models/Gungnir.dae',
        'Assets/Textures/Gungnir.png',
        'Assets/Textures/GungnirSkills.png',
        'Assets/Textures/GungnirRunes.png',
        'Assets/Audio/Gungnir/Invoke.wav',
        'Assets/Audio/Gungnir/Impact.wav',
        'Assets/Blocks/Gungnir.csv',
        'Assets/Database/Gungnir.xdb',
        'Assets/FourSaints.clo',
        'Assets/Models/QinglongCrown.dae',
        'Assets/Textures/QinglongCrown.png',
        'Assets/Models/XuanwuArmorV3.dae',
        'Assets/Models/BaihuLegArmorV3.dae',
        'Assets/Models/ZhuqueBootsV3.dae',
        'Assets/Textures/FourSaintsArmorV3.png',
        'Assets/Database/FourSaints.xdb',
        'Assets/Textures/FourSaintsBaihu.png',
        'Assets/Textures/FourSaintsEffects.png',
        'Assets/Textures/Clothing/FourSaintsQinglong.png',
        'Assets/Textures/Clothing/FourSaintsXuanwu.png',
        'Assets/Textures/Clothing/FourSaintsBaihuLegs.png',
        'Assets/Textures/Clothing/FourSaintsZhuque.png',
        'Assets/Blocks/SCIEPort.csv',
        'Assets/Blocks/Watercraft.csv',
        'Assets/Blocks/KellyPlants.csv',
        'Assets/Blocks/IndustrialFurniture.csv',
        'Assets/Blocks/LandVehicles.csv',
        'Assets/Blocks/OffroadVehicles.csv',
        'Assets/Blocks/Tanks.csv',
        'Assets/Blocks/Creatures.csv',
        'Assets/Blocks/Crops.csv',
        'Assets/Blocks/Bamboo.csv',
        'Assets/Blocks/Flax.csv',
        'Assets/Blocks/Sunflower.csv',
        'Assets/Blocks/Apple.csv',
        'Assets/Blocks/Helicopter.csv',
        'Assets/Blocks/Stormhammer.csv',
        'Assets/Blocks/StormhammerAwakened.csv',
        'Assets/Blocks/PortableFortress.csv',
        'Assets/Database/Watercraft.xdb',
        'Assets/Database/KellyPlants.xdb',
        'Assets/Database/LandVehicles.xdb',
        'Assets/Database/OffroadVehicles.xdb',
        'Assets/Database/Tanks.xdb',
        'Assets/Database/Creatures.xdb',
        'Assets/Database/Crops.xdb',
        'Assets/Database/Bamboo.xdb',
        'Assets/Database/Flax.xdb',
        'Assets/Database/Sunflower.xdb',
        'Assets/Database/Apple.xdb',
        'Assets/Database/Helicopter.xdb',
        'Assets/Database/Stormhammer.xdb',
        'Assets/Database/StormhammerAwakened.xdb',
        'Assets/Database/PortableFortress.xdb',
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
        'Assets/Models/OffroadCar.dae',
        'Assets/Models/SteamBoat.dae',
        'Assets/Models/T34Tank.dae',
        'Assets/Models/Fox.dae',
        'Assets/Models/Rabbit.dae',
        'Assets/Models/Chicken.dae',
        'Assets/Models/Helicopter.dae',
        'Assets/Models/Stormhammer.dae',
        'Assets/Models/ThunderSpear.dae',
        'Assets/Models/PortableFortress.dae',
        'Assets/Models/FortressCore.dae',
        'Assets/Textures/Train.png',
        'Assets/Textures/SteamBoat.png',
        'Assets/Textures/Airship.png',
        'Assets/Textures/ATV.png',
        'Assets/Textures/OffroadCar.png',
        'Assets/Textures/KellyExpansion.png',
        'Assets/Textures/T34Tank.png',
        'Assets/Textures/Fox.png',
        'Assets/Textures/Rabbit.png',
        'Assets/Textures/Chicken.png',
        'Assets/Textures/Corn.png',
        'Assets/Textures/Bamboo.png',
        'Assets/Textures/Flax.png',
        'Assets/Textures/Sunflower.png',
        'Assets/Textures/PortableFortress.png',
        'Assets/Textures/FortressGlass.png',
        'Assets/Textures/FortressGlow.png',
        'Assets/Textures/FortressCore.png',
        'Assets/Textures/Apple.png',
        'Assets/Textures/Helicopter.png',
        'Assets/Audio/SCIEPortHelicopterLoop.wav',
        'Assets/Textures/Stormhammer.png',
        'Assets/Textures/StormhammerGlow.png',
        'Assets/Textures/StormhammerEffects.png',
        'Assets/Textures/StormhammerAwakened.png',
        'Assets/Textures/StormhammerAwakenedGlow.png',
        'Assets/Textures/StormhammerAwakenedEffects.png',
        'Assets/Textures/StormhammerAwakenedSkills.png',
        'Assets/Audio/Stormhammer/Swing.wav',
        'Assets/Audio/Stormhammer/Charge.wav',
        'Assets/Audio/Stormhammer/Thunder.wav',
        'Assets/Audio/Stormhammer/Strike.wav',
        'Assets/Audio/Stormhammer/Rumble.wav',
        'Assets/Textures/LightningBeam.png',
        'Assets/Textures/LightningRune.png'
    )) {
        if ($entries -notcontains $requiredEntry) { throw "Package missing: $requiredEntry" }
    }
    if ($entries -contains 'Assets/Textures/KellyExpansion.webp') {
        throw 'Package must not contain KellyExpansion.webp; x25.01.17 Android cannot load it as Texture2D.'
    }
    # 打包后再核一次品牌字段：显示名必须是中文品牌名，PackageName 必须仍是内部标识。
    $packagedModInfoEntry = $archive.GetEntry('modinfo.json')
    if ($null -eq $packagedModInfoEntry) { throw 'Package is missing modinfo.json.' }
    $modInfoStream = $packagedModInfoEntry.Open()
    try {
        $streamReader = New-Object System.IO.StreamReader($modInfoStream, [Text.Encoding]::UTF8)
        try { $packagedModInfo = $streamReader.ReadToEnd() | ConvertFrom-Json } finally { $streamReader.Dispose() }
    }
    finally { $modInfoStream.Dispose() }
    if ($packagedModInfo.Name -ne $brandName -or $packagedModInfo.Author -ne $brandAuthor -or $packagedModInfo.PackageName -ne $brandPackageName) {
        throw ("Packaged modinfo branding drifted: Name='$($packagedModInfo.Name)' Author='$($packagedModInfo.Author)' PackageName='$($packagedModInfo.PackageName)'.")
    }
    if ($packagedModInfo.Version -ne $propertyGroup.Version) {
        throw "Packaged modinfo Version ($($packagedModInfo.Version)) must equal csproj Version ($($propertyGroup.Version))."
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
    $packagedDlls = @($entries | Where-Object { [IO.Path]::GetExtension($_) -eq '.dll' })
    if ($packagedDlls.Count -ne 1 -or $packagedDlls[0] -ne 'SCIEPort.dll') {
        throw ("Package must contain only SCIEPort.dll and no SCAPI/game/third-party DLL: " + ($packagedDlls -join ', '))
    }
    $unsupportedCreatureAssets = @($entries | Where-Object { $_ -match '(?i)\.(gltf|glb)$' })
    if ($unsupportedCreatureAssets.Count -gt 0) {
        throw ("Package contains glTF/GLB assets unsupported by fixed SCNET x25.01.17: " + ($unsupportedCreatureAssets -join ', '))
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
    [xml]$offroadDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\OffroadVehicles.xdb')
    $offroadEntity = $offroadDatabase.SelectSingleNode("//EntityTemplate[@Name='SCIEPortOffroadCar']")
    if ($null -eq $offroadEntity) { throw 'OffroadVehicles database is missing SCIEPortOffroadCar.' }
    foreach ($component in @('TwoSeatManager','OffroadCarDrive','SimpleModel','Spawn','Mount','Body','Damage')) {
        if ($null -eq $offroadEntity.SelectSingleNode("./MemberComponentTemplate[@Name='$component']")) {
            throw "Off-road car entity is missing component: $component"
        }
    }
    if (@($offroadEntity.SelectNodes("./MemberComponentTemplate[@Name='Mount']")).Count -ne 1) {
        throw 'Off-road car must keep exactly one native Mount; passenger role is managed by association.'
    }
    $offroadSeats = $offroadEntity.SelectSingleNode("./MemberComponentTemplate[@Name='TwoSeatManager']")
    $offroadMount = $offroadEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Mount']")
    $offroadBody = $offroadEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Body']")
    if ($offroadSeats.SelectSingleNode("./Parameter[@Name='DriverOffset']").Value -ne '-0.45,0.86,0.18' -or
        $offroadSeats.SelectSingleNode("./Parameter[@Name='PassengerOffset']").Value -ne '0.45,0.86,0.18') {
        throw 'Off-road car seat offsets differ from the audited side-by-side layout.'
    }
    if ($offroadMount.SelectSingleNode("./Parameter[@Name='DismountOffset']").Value -ne '1.72,0.45,0.18' -or
        $offroadBody.SelectSingleNode("./Parameter[@Name='BoxSize']").Value -ne '2.4,1.55,4' -or
        $offroadBody.SelectSingleNode("./Parameter[@Name='MaxSmoothRiseHeight']").Value -ne '1.05') {
        throw 'Off-road car collision, step or dismount parameters differ from the audited layout.'
    }
    if ($offroadEntity.SelectSingleNode("./MemberComponentTemplate[@Name='SimpleModel']/Parameter[@Name='ModelName']").Value -ne 'Models/OffroadCar' -or
        $offroadEntity.SelectSingleNode("./MemberComponentTemplate[@Name='SimpleModel']/Parameter[@Name='TextureOverride']").Value -ne 'Textures/OffroadCar') {
        throw 'Off-road car model or texture resource mapping is invalid.'
    }
    [xml]$helicopterDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\Helicopter.xdb')
    $helicopterEntity = $helicopterDatabase.SelectSingleNode("//EntityTemplate[@Name='SCIEPortHelicopter']")
    if ($null -eq $helicopterEntity) { throw 'Helicopter database is missing SCIEPortHelicopter.' }
    foreach ($component in @('TwoSeatManager','HelicopterDrive','SimpleModel','Spawn','Mount','Body','Damage')) {
        if ($null -eq $helicopterEntity.SelectSingleNode("./MemberComponentTemplate[@Name='$component']")) {
            throw "Helicopter entity is missing component: $component"
        }
    }
    if (@($helicopterEntity.SelectNodes("./MemberComponentTemplate[@Name='Mount']")).Count -ne 1) {
        throw 'Helicopter must keep exactly one native Mount; passenger role is managed by association.'
    }
    $helicopterSeats = $helicopterEntity.SelectSingleNode("./MemberComponentTemplate[@Name='TwoSeatManager']")
    $helicopterMount = $helicopterEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Mount']")
    $helicopterBody = $helicopterEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Body']")
    if ($helicopterSeats.SelectSingleNode("./Parameter[@Name='DriverOffset']").Value -ne '-0.42,1.05,-0.55' -or
        $helicopterSeats.SelectSingleNode("./Parameter[@Name='PassengerOffset']").Value -ne '0.42,1.05,-0.55' -or
        $helicopterMount.SelectSingleNode("./Parameter[@Name='DismountOffset']").Value -ne '1.65,0.45,-0.4' -or
        $helicopterBody.SelectSingleNode("./Parameter[@Name='BoxSize']").Value -ne '2.4,2.2,5.4') {
        throw 'Helicopter seats, collision body or safe dismount differ from the audited layout.'
    }
    if ($helicopterEntity.SelectSingleNode("./MemberComponentTemplate[@Name='SimpleModel']/Parameter[@Name='ModelName']").Value -ne 'Models/Helicopter' -or
        $helicopterEntity.SelectSingleNode("./MemberComponentTemplate[@Name='SimpleModel']/Parameter[@Name='TextureOverride']").Value -ne 'Textures/Helicopter') {
        throw 'Helicopter model or texture resource mapping is invalid.'
    }
    $helicopterDriveTemplate = $helicopterDatabase.SelectSingleNode("//ComponentTemplate[@Name='SCIEPortHelicopterDrive']")
    foreach ($parameterName in @('SpoolUpTime','SpoolDownTime','Gravity','VerticalResponse','MaximumAscentSpeed','MaximumDescentSpeed','AutoLandSpeed','HorizontalAcceleration','BrakeAcceleration','HorizontalDrag','LateralGrip','MaximumForwardSpeed','MaximumReverseSpeed','TurnRate','MaximumWorldY','VisualPitchLimit','VisualRollLimit','VisualResponse','VisualLateralReferenceSpeed','MainRotorAngularSpeed','TailRotorSpeedMultiplier','RotorBlurThreshold','SoundMinDistance','SoundRolloffFactor')) {
        if ($null -eq $helicopterDriveTemplate.SelectSingleNode("./Parameter[@Name='$parameterName']")) {
            throw "Helicopter configurable flight parameter is missing: $parameterName"
        }
    }
    [xml]$tankDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\Tanks.xdb')
    $tankEntity = $tankDatabase.SelectSingleNode("//EntityTemplate[@Name='SCIEPortT34Tank']")
    if ($null -eq $tankEntity) { throw 'Tank database is missing SCIEPortT34Tank.' }
    $tankShellBehavior = $tankDatabase.SelectSingleNode("//SubsystemTemplate[@Name='SCIEPortTankShellBehavior']")
    $tankShellMember = $tankDatabase.SelectSingleNode("//ProjectTemplate[@Name='Project']/MemberSubsystemTemplate[@Name='SCIEPortTankShellBehavior']")
    if ($null -eq $tankShellBehavior -or $null -eq $tankShellMember -or
        $tankShellMember.InheritanceParent -ne $tankShellBehavior.Guid) {
        throw 'Tank shell first-impact behavior is missing or not attached to Project.'
    }
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
    $tankDamage = $tankEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Damage']")
    if ($tankDamage.InheritanceParent -ne '17bb7b53-159c-4eb3-b4bc-137b10bba88a' -or
        $tankDamage.SelectSingleNode("./Parameter[@Name='Hitpoints']").Value -ne '1' -or
        $tankDamage.SelectSingleNode("./Parameter[@Name='AttackResilience']").Value -ne '600') {
        throw 'Tank durability must use audited ComponentDamage with Hitpoints=1 and AttackResilience=600.'
    }
    if ($null -ne $tankEntity.SelectSingleNode("./MemberComponentTemplate[@Name='OnFire']")) {
        throw 'Tank must not contain ComponentOnFire.'
    }
    if ($tankBox.Value -ne '3.6,2.1,5.9' -or $tankMount.Value -ne '0,1.72,0.2' -or $tankDismount.Value -ne '2.25,0.4,0') {
        throw 'Tank collision or seat parameters differ from the audited low-poly model envelope.'
    }
    $tankRows = @(Import-Csv -Delimiter ';' -Encoding UTF8 (Join-Path $projectDir 'Assets\Blocks\Tanks.csv'))
    $tankShellRow = @($tankRows | Where-Object { $_.'Class Name' -eq 'TankShellBlock' })
    if ($tankShellRow.Count -ne 1 -or $tankShellRow[0].Behaviors -ne 'SCIEPortTankShellBehavior' -or
        $tankShellRow[0].DisintegratesOnHit -ne 'TRUE' -or $tankShellRow[0].IsStickable -ne 'FALSE' -or
        $tankShellRow[0].ProjectileDamping -ne '1') {
        throw 'TankShellBlock must use the audited non-bouncing first-impact projectile settings.'
    }
    [xml]$creaturesDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\Creatures.xdb')
    $foxEntity = $creaturesDatabase.SelectSingleNode("//EntityTemplate[@Name='SCIEPortFox']")
    if ($null -eq $foxEntity -or $foxEntity.InheritanceParent -ne '49a9845c-3810-4092-ba53-df213d10d563') {
        throw 'SCIEPortFox must inherit the audited native Wolf entity template.'
    }
    foreach ($component in @('Creature', 'Body', 'Locomotion', 'Health', 'Spawn', 'HerdBehavior', 'FourLeggedModel')) {
        if ($null -eq $foxEntity.SelectSingleNode("./MemberComponentTemplate[@Name='$component']")) {
            throw "SCIEPortFox is missing its audited override component: $component"
        }
    }
    $foxModelParameter = $foxEntity.SelectSingleNode("./MemberComponentTemplate[@Name='FourLeggedModel']/Parameter[@Name='ModelName']")
    $foxTextureParameter = $foxEntity.SelectSingleNode("./MemberComponentTemplate[@Name='FourLeggedModel']/Parameter[@Name='TextureOverride']")
    $foxAutoDespawn = $foxEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Spawn']/Parameter[@Name='AutoDespawn']")
    if ($foxModelParameter.Value -ne 'Models/Fox' -or $foxTextureParameter.Value -ne 'Textures/Fox' -or $foxAutoDespawn.Value -ne 'False') {
        throw 'Fox model, texture or persistence mapping differs from the audited phase-one values.'
    }
    if ($null -ne $foxEntity.SelectSingleNode("./ParameterSet[@Name='CreatureEggData']")) {
        throw 'Phase-one fox must not register an inherited/duplicate native creature egg index.'
    }
    $foxChase = $foxEntity.SelectSingleNode("./MemberComponentTemplate[@Name='ChaseBehavior']")
    if ($null -eq $foxChase) {
        throw 'SCIEPortFox must carry an explicit ChaseBehavior override so it hunts the rabbit via the native AI.'
    }
    $foxAutoChaseMask = $foxChase.SelectSingleNode("./Parameter[@Name='AutoChaseMask']")
    $foxChaseProbability = $foxChase.SelectSingleNode("./Parameter[@Name='ChaseNonPlayerProbability']")
    if ($null -eq $foxAutoChaseMask -or ($foxAutoChaseMask.Value -split ',') -notcontains 'LandOther') {
        throw 'Fox ChaseBehavior AutoChaseMask must include LandOther (the rabbit category) for predation to target rabbits.'
    }
    $foxChaseProbabilityValue = 0.0
    if ($null -eq $foxChaseProbability -or -not [double]::TryParse($foxChaseProbability.Value, [ref] $foxChaseProbabilityValue) -or $foxChaseProbabilityValue -le 0.03) {
        throw 'Fox ChaseNonPlayerProbability must exceed the vanilla wolf baseline (0.03) so predation is observable.'
    }
    $foxBehavior = $creaturesDatabase.SelectSingleNode("//SubsystemTemplate[@Name='SCIEPortFoxSpawnBehavior']")
    $foxBehaviorMember = $creaturesDatabase.SelectSingleNode("//ProjectTemplate[@Name='Project']/MemberSubsystemTemplate[@Name='SCIEPortFoxSpawnBehavior']")
    if ($null -eq $foxBehavior -or $null -eq $foxBehaviorMember -or $foxBehaviorMember.InheritanceParent -ne $foxBehavior.Guid) {
        throw 'Manual fox spawn behavior is missing or not attached to Project.'
    }
    $foxRows = @(Import-Csv -Delimiter ';' -Encoding UTF8 (Join-Path $projectDir 'Assets\Blocks\Creatures.csv'))
    $foxSpawnerRow = @($foxRows | Where-Object { $_.'Class Name' -eq 'FoxSpawnBlock' })
    if ($foxSpawnerRow.Count -ne 1 -or $foxSpawnerRow[0].Behaviors -ne 'SCIEPortFoxSpawnBehavior' -or
        $foxSpawnerRow[0].DefaultCategory -ne 'SCIEPort.Creatures') {
        throw 'FoxSpawnBlock must use SCIEPort.Creatures and only SCIEPortFoxSpawnBehavior.'
    }
    $rabbitEntity = $creaturesDatabase.SelectSingleNode("//EntityTemplate[@Name='SCIEPortRabbit']")
    if ($null -eq $rabbitEntity -or $rabbitEntity.InheritanceParent -ne '8e62c6e4-a544-4c09-a3fb-59fd37d5b998') {
        throw 'SCIEPortRabbit must inherit the audited native Alpaca grazer entity template.'
    }
    foreach ($component in @('Creature', 'Body', 'Locomotion', 'Health', 'Spawn', 'HerdBehavior', 'FourLeggedModel')) {
        if ($null -eq $rabbitEntity.SelectSingleNode("./MemberComponentTemplate[@Name='$component']")) {
            throw "SCIEPortRabbit is missing its audited override component: $component"
        }
    }
    $rabbitModelParameter = $rabbitEntity.SelectSingleNode("./MemberComponentTemplate[@Name='FourLeggedModel']/Parameter[@Name='ModelName']")
    $rabbitTextureParameter = $rabbitEntity.SelectSingleNode("./MemberComponentTemplate[@Name='FourLeggedModel']/Parameter[@Name='TextureOverride']")
    $rabbitAutoDespawn = $rabbitEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Spawn']/Parameter[@Name='AutoDespawn']")
    if ($rabbitModelParameter.Value -ne 'Models/Rabbit' -or $rabbitTextureParameter.Value -ne 'Textures/Rabbit' -or $rabbitAutoDespawn.Value -ne 'False') {
        throw 'Rabbit model, texture or persistence mapping differs from the audited phase-one values.'
    }
    if ($null -ne $rabbitEntity.SelectSingleNode("./ParameterSet[@Name='CreatureEggData']")) {
        throw 'Phase-one rabbit must not register an inherited/duplicate native creature egg index.'
    }
    $rabbitBehavior = $creaturesDatabase.SelectSingleNode("//SubsystemTemplate[@Name='SCIEPortRabbitSpawnBehavior']")
    $rabbitBehaviorMember = $creaturesDatabase.SelectSingleNode("//ProjectTemplate[@Name='Project']/MemberSubsystemTemplate[@Name='SCIEPortRabbitSpawnBehavior']")
    if ($null -eq $rabbitBehavior -or $null -eq $rabbitBehaviorMember -or $rabbitBehaviorMember.InheritanceParent -ne $rabbitBehavior.Guid) {
        throw 'Manual rabbit spawn behavior is missing or not attached to Project.'
    }
    $rabbitSpawnerRow = @($foxRows | Where-Object { $_.'Class Name' -eq 'RabbitSpawnBlock' })
    if ($rabbitSpawnerRow.Count -ne 1 -or $rabbitSpawnerRow[0].Behaviors -ne 'SCIEPortRabbitSpawnBehavior' -or
        $rabbitSpawnerRow[0].DefaultCategory -ne 'SCIEPort.Creatures') {
        throw 'RabbitSpawnBlock must use SCIEPort.Creatures and only SCIEPortRabbitSpawnBehavior.'
    }
    $chickenEntity = $creaturesDatabase.SelectSingleNode("//EntityTemplate[@Name='SCIEPortChicken']")
    if ($null -eq $chickenEntity -or $chickenEntity.InheritanceParent -ne '8e62c6e4-a544-4c09-a3fb-59fd37d5b998') {
        throw 'SCIEPortChicken must inherit the audited native Alpaca grazer entity template.'
    }
    foreach ($component in @('Creature', 'Body', 'Locomotion', 'Health', 'Spawn', 'HerdBehavior', 'FourLeggedModel')) {
        if ($null -eq $chickenEntity.SelectSingleNode("./MemberComponentTemplate[@Name='$component']")) {
            throw "SCIEPortChicken is missing its audited override component: $component"
        }
    }
    $chickenModelParameter = $chickenEntity.SelectSingleNode("./MemberComponentTemplate[@Name='FourLeggedModel']/Parameter[@Name='ModelName']")
    $chickenTextureParameter = $chickenEntity.SelectSingleNode("./MemberComponentTemplate[@Name='FourLeggedModel']/Parameter[@Name='TextureOverride']")
    $chickenAutoDespawn = $chickenEntity.SelectSingleNode("./MemberComponentTemplate[@Name='Spawn']/Parameter[@Name='AutoDespawn']")
    if ($chickenModelParameter.Value -ne 'Models/Chicken' -or $chickenTextureParameter.Value -ne 'Textures/Chicken' -or $chickenAutoDespawn.Value -ne 'False') {
        throw 'Chicken model, texture or persistence mapping differs from the audited manual-spawn values.'
    }
    if ($null -ne $chickenEntity.SelectSingleNode("./ParameterSet[@Name='CreatureEggData']")) {
        throw 'Manual chicken must not register an inherited/duplicate native creature egg index.'
    }
    $chickenBehavior = $creaturesDatabase.SelectSingleNode("//SubsystemTemplate[@Name='SCIEPortChickenSpawnBehavior']")
    $chickenBehaviorMember = $creaturesDatabase.SelectSingleNode("//ProjectTemplate[@Name='Project']/MemberSubsystemTemplate[@Name='SCIEPortChickenSpawnBehavior']")
    if ($null -eq $chickenBehavior -or $null -eq $chickenBehaviorMember -or $chickenBehaviorMember.InheritanceParent -ne $chickenBehavior.Guid) {
        throw 'Manual chicken spawn behavior is missing or not attached to Project.'
    }
    $chickenSpawnerRow = @($foxRows | Where-Object { $_.'Class Name' -eq 'ChickenSpawnBlock' })
    if ($chickenSpawnerRow.Count -ne 1 -or $chickenSpawnerRow[0].Behaviors -ne 'SCIEPortChickenSpawnBehavior' -or
        $chickenSpawnerRow[0].DefaultCategory -ne 'SCIEPort.Creatures') {
        throw 'ChickenSpawnBlock must use SCIEPort.Creatures and only SCIEPortChickenSpawnBehavior.'
    }
    [xml]$cropsDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\Crops.xdb')
    $cornBehavior = $cropsDatabase.SelectSingleNode("//SubsystemTemplate[@Name='SCIEPortCornBehavior']")
    if ($null -eq $cornBehavior -or $cornBehavior.InheritanceParent -ne 'fefb9590-4972-4893-b02a-76063611b745') {
        throw 'SCIEPortCornBehavior must inherit the native pollable block behavior base.'
    }
    $cornBehaviorClass = $cornBehavior.SelectSingleNode("./Parameter[@Name='Class']")
    if ($null -eq $cornBehaviorClass -or $cornBehaviorClass.Value -ne 'SCIEPort.Features.Crops.SubsystemCornBlockBehavior') {
        throw 'SCIEPortCornBehavior must bind the corn pollable subsystem class.'
    }
    $cornBehaviorMember = $cropsDatabase.SelectSingleNode("//ProjectTemplate[@Name='Project']/MemberSubsystemTemplate[@Name='SCIEPortCornBehavior']")
    if ($null -eq $cornBehaviorMember -or $cornBehaviorMember.InheritanceParent -ne $cornBehavior.Guid) {
        throw 'Corn growth behavior is missing or not attached to Project.'
    }
    $cropRows = @(Import-Csv -Delimiter ';' -Encoding UTF8 (Join-Path $projectDir 'Assets\Blocks\Crops.csv'))
    $cornStalkRow = @($cropRows | Where-Object { $_.'Class Name' -eq 'CornStalkBlock' })
    $cornSeedRow = @($cropRows | Where-Object { $_.'Class Name' -eq 'CornSeedBlock' })
    $cornEarRow = @($cropRows | Where-Object { $_.'Class Name' -eq 'CornEarBlock' })
    if ($cornStalkRow.Count -ne 1 -or $cornSeedRow.Count -ne 1 -or $cornEarRow.Count -ne 1) {
        throw 'Crops.csv must contain exactly one row for CornStalkBlock, CornSeedBlock and CornEarBlock.'
    }
    foreach ($cropRow in @($cornStalkRow[0], $cornSeedRow[0], $cornEarRow[0])) {
        if ($cropRow.DefaultCategory -ne $(if ($cropRow.'Class Name' -eq 'CornEarBlock') {'SCIEPort.Food'} else {'SCIEPort.Plants'}) -or $cropRow.IsPlaceable -ne 'TRUE') {
            throw "Crops row $($cropRow.'Class Name') must be a placeable item in the Crops category."
        }
    }
    if ($cornStalkRow[0].DefaultTextureSlot -ne '0' -or $cornSeedRow[0].DefaultTextureSlot -ne '8' -or
        $cornEarRow[0].DefaultTextureSlot -ne '9') {
        throw 'Corn atlas slots must remain stalk 0..7, seed 8, ear 9 as drawn in Corn.png.'
    }
    [xml]$bambooDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\Bamboo.xdb')
    $bambooBehavior = $bambooDatabase.SelectSingleNode("//SubsystemTemplate[@Name='SCIEPortBambooBehavior']")
    if ($null -eq $bambooBehavior -or $bambooBehavior.InheritanceParent -ne 'fefb9590-4972-4893-b02a-76063611b745') {
        throw 'SCIEPortBambooBehavior must inherit the native pollable block behavior base.'
    }
    $bambooBehaviorClass = $bambooBehavior.SelectSingleNode("./Parameter[@Name='Class']")
    if ($null -eq $bambooBehaviorClass -or $bambooBehaviorClass.Value -ne 'SCIEPort.Features.Bamboo.SubsystemBambooBlockBehavior') {
        throw 'SCIEPortBambooBehavior must bind the bamboo pollable subsystem class.'
    }
    $bambooBehaviorMember = $bambooDatabase.SelectSingleNode("//ProjectTemplate[@Name='Project']/MemberSubsystemTemplate[@Name='SCIEPortBambooBehavior']")
    if ($null -eq $bambooBehaviorMember -or $bambooBehaviorMember.InheritanceParent -ne $bambooBehavior.Guid) {
        throw 'Bamboo growth behavior is missing or not attached to Project.'
    }
    $bambooRows = @(Import-Csv -Delimiter ';' -Encoding UTF8 (Join-Path $projectDir 'Assets\Blocks\Bamboo.csv'))
    $bambooPlantRow = @($bambooRows | Where-Object { $_.'Class Name' -eq 'BambooPlantBlock' })
    $bambooBlockRow = @($bambooRows | Where-Object { $_.'Class Name' -eq 'BambooBlock' })
    if ($bambooPlantRow.Count -ne 1 -or $bambooBlockRow.Count -ne 1) {
        throw 'Bamboo.csv must contain exactly one row for BambooPlantBlock and BambooBlock.'
    }
    if ($bambooPlantRow[0].DefaultCategory -ne 'SCIEPort.Plants' -or $bambooPlantRow[0].IsPlaceable -ne 'TRUE' -or
        $bambooPlantRow[0].DefaultTextureSlot -ne '0') {
        throw 'BambooPlantBlock must be a placeable Crops item drawn on atlas slot 0.'
    }
    if ($bambooBlockRow[0].IsPlaceable -ne 'TRUE' -or $bambooBlockRow[0].IsCollidable -ne 'TRUE' -or
        $bambooBlockRow[0].IsTransparent -ne 'FALSE' -or $bambooBlockRow[0].DefaultTextureSlot -ne '2') {
        throw 'BambooBlock must be a solid, collidable, opaque building cube drawn on atlas slot 2.'
    }
    [xml]$flaxDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\Flax.xdb')
    $flaxBehavior = $flaxDatabase.SelectSingleNode("//SubsystemTemplate[@Name='SCIEPortFlaxBehavior']")
    if ($null -eq $flaxBehavior -or $flaxBehavior.InheritanceParent -ne 'fefb9590-4972-4893-b02a-76063611b745') {
        throw 'SCIEPortFlaxBehavior must inherit the native pollable block behavior base.'
    }
    $flaxBehaviorClass = $flaxBehavior.SelectSingleNode("./Parameter[@Name='Class']")
    if ($null -eq $flaxBehaviorClass -or $flaxBehaviorClass.Value -ne 'SCIEPort.Features.Flax.SubsystemFlaxBlockBehavior') {
        throw 'SCIEPortFlaxBehavior must bind the flax pollable subsystem class.'
    }
    $flaxBehaviorMember = $flaxDatabase.SelectSingleNode("//ProjectTemplate[@Name='Project']/MemberSubsystemTemplate[@Name='SCIEPortFlaxBehavior']")
    if ($null -eq $flaxBehaviorMember -or $flaxBehaviorMember.InheritanceParent -ne $flaxBehavior.Guid) {
        throw 'Flax growth behavior is missing or not attached to Project.'
    }
    $flaxRows = @(Import-Csv -Delimiter ';' -Encoding UTF8 (Join-Path $projectDir 'Assets\Blocks\Flax.csv'))
    $flaxPlantRow = @($flaxRows | Where-Object { $_.'Class Name' -eq 'FlaxPlantBlock' })
    $flaxFiberRow = @($flaxRows | Where-Object { $_.'Class Name' -eq 'FlaxFiberBlock' })
    if ($flaxPlantRow.Count -ne 1 -or $flaxFiberRow.Count -ne 1) {
        throw 'Flax.csv must contain exactly one row for FlaxPlantBlock and FlaxFiberBlock.'
    }
    if ($flaxPlantRow[0].DefaultCategory -ne 'SCIEPort.Plants' -or $flaxPlantRow[0].IsPlaceable -ne 'TRUE' -or
        $flaxPlantRow[0].DefaultTextureSlot -ne '0') {
        throw 'FlaxPlantBlock must be a placeable Crops item drawn on atlas slot 0.'
    }
    if ($flaxFiberRow[0].DefaultCategory -ne 'SCIEPort.Materials' -or $flaxFiberRow[0].IsPlaceable -ne 'TRUE' -or
        $flaxFiberRow[0].DefaultTextureSlot -ne '8') {
        throw 'FlaxFiberBlock must be a placeable Crops item drawn on atlas slot 8.'
    }
    [xml]$sunflowerDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\Sunflower.xdb')
    $sunflowerBehavior = $sunflowerDatabase.SelectSingleNode("//SubsystemTemplate[@Name='SCIEPortSunflowerBehavior']")
    if ($null -eq $sunflowerBehavior -or $sunflowerBehavior.InheritanceParent -ne 'fefb9590-4972-4893-b02a-76063611b745') {
        throw 'SCIEPortSunflowerBehavior must inherit the native pollable block behavior base.'
    }
    $sunflowerBehaviorClass = $sunflowerBehavior.SelectSingleNode("./Parameter[@Name='Class']")
    if ($null -eq $sunflowerBehaviorClass -or $sunflowerBehaviorClass.Value -ne 'SCIEPort.Features.Sunflower.SubsystemSunflowerBlockBehavior') {
        throw 'SCIEPortSunflowerBehavior must bind the sunflower pollable subsystem class.'
    }
    $sunflowerBehaviorMember = $sunflowerDatabase.SelectSingleNode("//ProjectTemplate[@Name='Project']/MemberSubsystemTemplate[@Name='SCIEPortSunflowerBehavior']")
    if ($null -eq $sunflowerBehaviorMember -or $sunflowerBehaviorMember.InheritanceParent -ne $sunflowerBehavior.Guid) {
        throw 'Sunflower growth behavior is missing or not attached to Project.'
    }
    $sunflowerRows = @(Import-Csv -Delimiter ';' -Encoding UTF8 (Join-Path $projectDir 'Assets\Blocks\Sunflower.csv'))
    $sunflowerPlantRow = @($sunflowerRows | Where-Object { $_.'Class Name' -eq 'SunflowerPlantBlock' })
    $sunflowerSeedRow = @($sunflowerRows | Where-Object { $_.'Class Name' -eq 'SunflowerSeedBlock' })
    if ($sunflowerPlantRow.Count -ne 1 -or $sunflowerSeedRow.Count -ne 1) {
        throw 'Sunflower.csv must contain exactly one row for SunflowerPlantBlock and SunflowerSeedBlock.'
    }
    if ($sunflowerPlantRow[0].DefaultCategory -ne 'SCIEPort.Plants' -or $sunflowerPlantRow[0].IsPlaceable -ne 'TRUE' -or
        $sunflowerPlantRow[0].DefaultTextureSlot -ne '0') {
        throw 'SunflowerPlantBlock must be a placeable Crops item drawn on atlas slot 0.'
    }
    $sunflowerSeedNutrition = 0.0
    if ($sunflowerSeedRow[0].DefaultCategory -ne 'SCIEPort.Food' -or $sunflowerSeedRow[0].IsPlaceable -ne 'TRUE' -or
        $sunflowerSeedRow[0].DefaultTextureSlot -ne '8' -or
        -not [double]::TryParse($sunflowerSeedRow[0].DefaultNutritionalValue, [ref] $sunflowerSeedNutrition) -or $sunflowerSeedNutrition -le 0.0) {
        throw 'SunflowerSeedBlock must be a placeable Food item drawn on atlas slot 8 with a positive nutritional value.'
    }
    [xml]$appleDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\Apple.xdb')
    $appleBehavior = $appleDatabase.SelectSingleNode("//SubsystemTemplate[@Name='SCIEPortAppleBehavior']")
    if ($null -eq $appleBehavior -or $appleBehavior.InheritanceParent -ne 'fefb9590-4972-4893-b02a-76063611b745') {
        throw 'SCIEPortAppleBehavior must inherit the native pollable block behavior base.'
    }
    $appleBehaviorClass = $appleBehavior.SelectSingleNode("./Parameter[@Name='Class']")
    if ($null -eq $appleBehaviorClass -or $appleBehaviorClass.Value -ne 'SCIEPort.Features.Apple.SubsystemAppleBlockBehavior') {
        throw 'SCIEPortAppleBehavior must bind the apple pollable subsystem class.'
    }
    $appleBehaviorMember = $appleDatabase.SelectSingleNode("//ProjectTemplate[@Name='Project']/MemberSubsystemTemplate[@Name='SCIEPortAppleBehavior']")
    if ($null -eq $appleBehaviorMember -or $appleBehaviorMember.InheritanceParent -ne $appleBehavior.Guid) {
        throw 'Apple growth behavior is missing or not attached to Project.'
    }
    $appleRows = @(Import-Csv -Delimiter ';' -Encoding UTF8 (Join-Path $projectDir 'Assets\Blocks\Apple.csv'))
    $applePlantRow = @($appleRows | Where-Object { $_.'Class Name' -eq 'ApplePlantBlock' })
    $appleFruitRow = @($appleRows | Where-Object { $_.'Class Name' -eq 'AppleBlock' })
    if ($applePlantRow.Count -ne 1 -or $appleFruitRow.Count -ne 1) {
        throw 'Apple.csv must contain exactly one row for ApplePlantBlock and AppleBlock.'
    }
    if ($applePlantRow[0].DefaultCategory -ne 'SCIEPort.Plants' -or $applePlantRow[0].IsPlaceable -ne 'TRUE' -or
        $applePlantRow[0].DefaultTextureSlot -ne '0') {
        throw 'ApplePlantBlock must be a placeable Crops item drawn on atlas slot 0.'
    }
    $appleFruitNutrition = 0.0
    if ($appleFruitRow[0].DefaultCategory -ne 'SCIEPort.Food' -or $appleFruitRow[0].IsPlaceable -ne 'TRUE' -or
        $appleFruitRow[0].DefaultTextureSlot -ne '9' -or
        -not [double]::TryParse($appleFruitRow[0].DefaultNutritionalValue, [ref] $appleFruitNutrition) -or $appleFruitNutrition -le 0.0) {
        throw 'AppleBlock must be a placeable Food item drawn on atlas slot 9 with a positive nutritional value.'
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
    $atvParts = @('BikeFrontWheel', 'BikeRearWheel', 'BikeFrontFork', 'BikeHandlebar', 'BikeHeadlight', 'BikeFrame', 'BikeEngine', 'BikeFuelTank', 'BikeSeat', 'BikeSidePanels', 'BikeExhaust', 'BikeSwingarm', 'BikeFenders')
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
    $atvTriangleBudget = 6000
    $atvTotalTriangles = 0
    foreach ($requiredNode in $atvParts) {
        if ($atvGeometryInstances -notcontains "$requiredNode-mesh") {
            throw "ATV.dae root does not reference required geometry $requiredNode-mesh."
        }
        $geometry = $atvModelDocument.SelectSingleNode("//*[local-name()='geometry' and @name='$requiredNode']")
        if ($null -eq $geometry) { throw "ATV.dae is missing independent geometry $requiredNode." }

        $posAccessor = $geometry.SelectSingleNode(".//*[local-name()='source'][contains(@id,'-positions')]/*[local-name()='technique_common']/*[local-name()='accessor']")
        $nrmAccessor = $geometry.SelectSingleNode(".//*[local-name()='source'][contains(@id,'-normals')]/*[local-name()='technique_common']/*[local-name()='accessor']")
        $uvAccessor = $geometry.SelectSingleNode(".//*[local-name()='source'][contains(@id,'-uv')]/*[local-name()='technique_common']/*[local-name()='accessor']")
        if ($null -eq $posAccessor -or $null -eq $nrmAccessor -or $null -eq $uvAccessor) {
            throw "ATV.dae geometry $requiredNode is missing a positions/normals/uv accessor."
        }
        if ($posAccessor.GetAttribute('stride') -ne '3' -or $nrmAccessor.GetAttribute('stride') -ne '3' -or $uvAccessor.GetAttribute('stride') -ne '2') {
            throw "ATV.dae geometry $requiredNode must use stride 3/3/2 for positions/normals/uv."
        }
        $vertexCount = [int]$posAccessor.GetAttribute('count')
        if ($vertexCount -lt 3 -or $vertexCount % 3 -ne 0) {
            throw "ATV.dae geometry $requiredNode vertex count ($vertexCount) must be a positive multiple of three."
        }
        if ([int]$nrmAccessor.GetAttribute('count') -ne $vertexCount -or [int]$uvAccessor.GetAttribute('count') -ne $vertexCount) {
            throw "ATV.dae geometry $requiredNode positions/normals/uv accessors disagree on the vertex count ($vertexCount)."
        }

        $posArray = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'positions-array')]")
        $nrmArray = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'normals-array')]")
        $uvArray = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'uv-array')]")
        if ($null -eq $posArray -or $null -eq $nrmArray -or $null -eq $uvArray) {
            throw "ATV.dae geometry $requiredNode is missing a positions/normals/uv float_array."
        }
        $posFloats = @($posArray.InnerText -split '\s+' | Where-Object { $_ })
        $nrmFloats = @($nrmArray.InnerText -split '\s+' | Where-Object { $_ })
        $uvFloats = @($uvArray.InnerText -split '\s+' | Where-Object { $_ })
        if ($posFloats.Count -ne ($vertexCount * 3) -or $nrmFloats.Count -ne ($vertexCount * 3)) {
            throw "ATV.dae geometry $requiredNode positions/normals must each hold $($vertexCount * 3) floats."
        }
        if ($uvFloats.Count -ne ($vertexCount * 2)) {
            throw "ATV.dae geometry $requiredNode uv array must hold $($vertexCount * 2) floats; found $($uvFloats.Count)."
        }
        foreach ($uvValue in $uvFloats) {
            $parsedUv = [double]::Parse($uvValue, [Globalization.CultureInfo]::InvariantCulture)
            if ($parsedUv -lt 0 -or $parsedUv -gt 1) {
                throw "ATV.dae geometry $requiredNode has a UV coordinate ($parsedUv) outside the 0..1 atlas."
            }
        }

        $triangles = $geometry.SelectSingleNode(".//*[local-name()='triangles']")
        if ($null -eq $triangles) { throw "ATV.dae geometry $requiredNode is missing a triangles element." }
        $triangleCount = [int]$triangles.GetAttribute('count')
        if ($triangleCount -ne ($vertexCount / 3)) {
            throw "ATV.dae geometry $requiredNode triangles count ($triangleCount) must equal vertexCount/3 ($($vertexCount / 3))."
        }
        foreach ($semantic in @('VERTEX', 'NORMAL', 'TEXCOORD')) {
            if ($null -eq $triangles.SelectSingleNode("./*[local-name()='input' and @semantic='$semantic']")) {
                throw "ATV.dae geometry $requiredNode is missing $semantic data."
            }
        }
        $faceIndices = @($triangles.SelectSingleNode("./*[local-name()='p']").InnerText -split '\s+' | Where-Object { $_ } | ForEach-Object { [int]$_ })
        if ($faceIndices.Count -ne ($triangleCount * 9)) {
            throw "ATV.dae geometry $requiredNode must hold $($triangleCount * 9) interleaved indices; found $($faceIndices.Count)."
        }
        $maxFaceIndex = $vertexCount - 1
        foreach ($faceIndex in $faceIndices) {
            if ($faceIndex -lt 0 -or $faceIndex -gt $maxFaceIndex) {
                throw "ATV.dae geometry $requiredNode has an out-of-range face index ($faceIndex); valid range 0..$maxFaceIndex."
            }
        }
        $atvTotalTriangles += $triangleCount
    }
    if ($atvTotalTriangles -gt $atvTriangleBudget) {
        throw "ATV.dae total triangle count ($atvTotalTriangles) exceeds the mobile budget of $atvTriangleBudget triangles."
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
        if ($atvBitmap.Width -ne 64 -or $atvBitmap.Height -ne 64) { throw 'ATV.png must be the 64x64 low-poly material atlas.' }
        $atvColors = New-Object System.Collections.Generic.HashSet[int]
        for ($x = 0; $x -lt $atvBitmap.Width; $x++) {
            for ($y = 0; $y -lt $atvBitmap.Height; $y++) {
                $pixel = $atvBitmap.GetPixel($x, $y)
                if ($pixel.A -ne 255) { throw "ATV.png contains transparent pixel at $x,$y." }
                [void]$atvColors.Add($pixel.ToArgb())
            }
        }
        if ($atvColors.Count -gt 96) { throw "ATV.png must stay within a 96-color opaque palette; found $($atvColors.Count)." }
    }
    finally { $atvBitmap.Dispose() }

    [xml]$foxModelDocument = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Models\Fox.dae')
    if ($foxModelDocument.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') {
        throw 'Fox.dae must be oriented Y_UP.'
    }
    $foxBoneNames = @($foxModelDocument.SelectNodes("//*[local-name()='visual_scene']//*[local-name()='node']") | ForEach-Object { $_.GetAttribute('name') })
    $requiredFoxBones = @('Body', 'Head', 'Leg1', 'Leg2', 'Leg3', 'Leg4')
    if ($foxBoneNames.Count -ne 6 -or ($requiredFoxBones | Where-Object { $foxBoneNames -notcontains $_ }).Count -ne 0) {
        throw "Fox.dae must contain exactly the six native procedural bones: $($requiredFoxBones -join ', ')."
    }
    $foxGeometries = @($foxModelDocument.SelectNodes("//*[local-name()='geometry']"))
    if ($foxGeometries.Count -ne 13) { throw "Fox.dae must contain 13 clean-room cuboid meshes; found $($foxGeometries.Count)." }
    foreach ($geometry in $foxGeometries) {
        $positions = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'positions-array')]")
        $triangles = $geometry.SelectSingleNode(".//*[local-name()='triangles']")
        if ((@($positions.InnerText -split '\s+' | Where-Object { $_ })).Count -ne 24 -or $triangles.GetAttribute('count') -ne '12') {
            throw "Fox.dae geometry $($geometry.GetAttribute('name')) is not an audited 8-vertex/12-triangle cuboid."
        }
        foreach ($semantic in @('VERTEX', 'NORMAL', 'TEXCOORD')) {
            if ($null -eq $triangles.SelectSingleNode("./*[local-name()='input' and @semantic='$semantic']")) {
                throw "Fox.dae geometry $($geometry.GetAttribute('name')) is missing $semantic data."
            }
        }
    }
    $foxModelStream = [IO.File]::OpenRead((Join-Path $projectDir 'Assets\Models\Fox.dae'))
    try { $targetFoxModel = $targetModelLoad.Invoke($null, @($foxModelStream)) }
    finally { $foxModelStream.Dispose() }
    if ($targetFoxModel.Bones.Count -ne 6 -or $targetFoxModel.Meshes.Count -ne 13) {
        throw "Target Engine must import Fox.dae as 6 bones/13 meshes; found $($targetFoxModel.Bones.Count) bones/$($targetFoxModel.Meshes.Count) meshes."
    }
    foreach ($boneName in $requiredFoxBones) {
        if (-not ($targetFoxModel.Bones | Where-Object { $_.Name -eq $boneName })) {
            throw "Target Engine did not import required fox bone $boneName."
        }
    }
    Add-Type -AssemblyName System.Drawing
    $foxBitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\Fox.png'))
    try {
        if ($foxBitmap.Width -ne 32 -or $foxBitmap.Height -ne 8) { throw 'Fox.png must be the audited 32x8 opaque palette.' }
        $foxColors = New-Object System.Collections.Generic.HashSet[int]
        for ($x = 0; $x -lt $foxBitmap.Width; $x++) {
            for ($y = 0; $y -lt $foxBitmap.Height; $y++) {
                $pixel = $foxBitmap.GetPixel($x, $y)
                if ($pixel.A -ne 255) { throw "Fox.png contains transparent pixel at $x,$y." }
                [void]$foxColors.Add($pixel.ToArgb())
            }
        }
        if ($foxColors.Count -ne 4) { throw "Fox.png must contain exactly four opaque palette colors; found $($foxColors.Count)." }
    }
    finally { $foxBitmap.Dispose() }

    [xml]$rabbitModelDocument = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Models\Rabbit.dae')
    if ($rabbitModelDocument.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') {
        throw 'Rabbit.dae must be oriented Y_UP.'
    }
    $rabbitBoneNames = @($rabbitModelDocument.SelectNodes("//*[local-name()='visual_scene']//*[local-name()='node']") | ForEach-Object { $_.GetAttribute('name') })
    $requiredRabbitBones = @('Body', 'Head', 'Leg1', 'Leg2', 'Leg3', 'Leg4')
    if ($rabbitBoneNames.Count -ne 6 -or ($requiredRabbitBones | Where-Object { $rabbitBoneNames -notcontains $_ }).Count -ne 0) {
        throw "Rabbit.dae must contain exactly the six native procedural bones: $($requiredRabbitBones -join ', ')."
    }
    $rabbitGeometries = @($rabbitModelDocument.SelectNodes("//*[local-name()='geometry']"))
    if ($rabbitGeometries.Count -ne 12) { throw "Rabbit.dae must contain 12 clean-room cuboid meshes; found $($rabbitGeometries.Count)." }
    foreach ($geometry in $rabbitGeometries) {
        $positions = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'positions-array')]")
        $triangles = $geometry.SelectSingleNode(".//*[local-name()='triangles']")
        if ((@($positions.InnerText -split '\s+' | Where-Object { $_ })).Count -ne 24 -or $triangles.GetAttribute('count') -ne '12') {
            throw "Rabbit.dae geometry $($geometry.GetAttribute('name')) is not an audited 8-vertex/12-triangle cuboid."
        }
        foreach ($semantic in @('VERTEX', 'NORMAL', 'TEXCOORD')) {
            if ($null -eq $triangles.SelectSingleNode("./*[local-name()='input' and @semantic='$semantic']")) {
                throw "Rabbit.dae geometry $($geometry.GetAttribute('name')) is missing $semantic data."
            }
        }
    }
    $rabbitModelStream = [IO.File]::OpenRead((Join-Path $projectDir 'Assets\Models\Rabbit.dae'))
    try { $targetRabbitModel = $targetModelLoad.Invoke($null, @($rabbitModelStream)) }
    finally { $rabbitModelStream.Dispose() }
    if ($targetRabbitModel.Bones.Count -ne 6 -or $targetRabbitModel.Meshes.Count -ne 12) {
        throw "Target Engine must import Rabbit.dae as 6 bones/12 meshes; found $($targetRabbitModel.Bones.Count) bones/$($targetRabbitModel.Meshes.Count) meshes."
    }
    foreach ($boneName in $requiredRabbitBones) {
        if (-not ($targetRabbitModel.Bones | Where-Object { $_.Name -eq $boneName })) {
            throw "Target Engine did not import required rabbit bone $boneName."
        }
    }
    Add-Type -AssemblyName System.Drawing
    $rabbitBitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\Rabbit.png'))
    try {
        if ($rabbitBitmap.Width -ne 32 -or $rabbitBitmap.Height -ne 8) { throw 'Rabbit.png must be the audited 32x8 opaque palette.' }
        $rabbitColors = New-Object System.Collections.Generic.HashSet[int]
        for ($x = 0; $x -lt $rabbitBitmap.Width; $x++) {
            for ($y = 0; $y -lt $rabbitBitmap.Height; $y++) {
                $pixel = $rabbitBitmap.GetPixel($x, $y)
                if ($pixel.A -ne 255) { throw "Rabbit.png contains transparent pixel at $x,$y." }
                [void]$rabbitColors.Add($pixel.ToArgb())
            }
        }
        if ($rabbitColors.Count -ne 4) { throw "Rabbit.png must contain exactly four opaque palette colors; found $($rabbitColors.Count)." }
    }
    finally { $rabbitBitmap.Dispose() }

    [xml]$chickenModelDocument = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Models\Chicken.dae')
    if ($chickenModelDocument.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') {
        throw 'Chicken.dae must be oriented Y_UP.'
    }
    $chickenBoneNames = @($chickenModelDocument.SelectNodes("//*[local-name()='visual_scene']//*[local-name()='node']") | ForEach-Object { $_.GetAttribute('name') })
    $requiredChickenBones = @('Body', 'Head', 'Leg1', 'Leg2', 'Leg3', 'Leg4')
    if ($chickenBoneNames.Count -ne 6 -or ($requiredChickenBones | Where-Object { $chickenBoneNames -notcontains $_ }).Count -ne 0) {
        throw "Chicken.dae must contain exactly the six native procedural bones: $($requiredChickenBones -join ', ')."
    }
    $chickenGeometries = @($chickenModelDocument.SelectNodes("//*[local-name()='geometry']"))
    if ($chickenGeometries.Count -ne 10) { throw "Chicken.dae must contain 10 clean-room cuboid meshes; found $($chickenGeometries.Count)." }
    foreach ($geometry in $chickenGeometries) {
        $positions = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'positions-array')]")
        $triangles = $geometry.SelectSingleNode(".//*[local-name()='triangles']")
        if ((@($positions.InnerText -split '\s+' | Where-Object { $_ })).Count -ne 24 -or $triangles.GetAttribute('count') -ne '12') {
            throw "Chicken.dae geometry $($geometry.GetAttribute('name')) is not an audited 8-vertex/12-triangle cuboid."
        }
        foreach ($semantic in @('VERTEX', 'NORMAL', 'TEXCOORD')) {
            if ($null -eq $triangles.SelectSingleNode("./*[local-name()='input' and @semantic='$semantic']")) {
                throw "Chicken.dae geometry $($geometry.GetAttribute('name')) is missing $semantic data."
            }
        }
    }
    $chickenModelStream = [IO.File]::OpenRead((Join-Path $projectDir 'Assets\Models\Chicken.dae'))
    try { $targetChickenModel = $targetModelLoad.Invoke($null, @($chickenModelStream)) }
    finally { $chickenModelStream.Dispose() }
    if ($targetChickenModel.Bones.Count -ne 6 -or $targetChickenModel.Meshes.Count -ne 10) {
        throw "Target Engine must import Chicken.dae as 6 bones/10 meshes; found $($targetChickenModel.Bones.Count) bones/$($targetChickenModel.Meshes.Count) meshes."
    }
    foreach ($boneName in $requiredChickenBones) {
        if (-not ($targetChickenModel.Bones | Where-Object { $_.Name -eq $boneName })) {
            throw "Target Engine did not import required chicken bone $boneName."
        }
    }
    $chickenBitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\Chicken.png'))
    try {
        if ($chickenBitmap.Width -ne 32 -or $chickenBitmap.Height -ne 8) { throw 'Chicken.png must be the audited 32x8 opaque palette.' }
        $chickenColors = New-Object System.Collections.Generic.HashSet[int]
        for ($x = 0; $x -lt $chickenBitmap.Width; $x++) {
            for ($y = 0; $y -lt $chickenBitmap.Height; $y++) {
                $pixel = $chickenBitmap.GetPixel($x, $y)
                if ($pixel.A -ne 255) { throw "Chicken.png contains transparent pixel at $x,$y." }
                [void]$chickenColors.Add($pixel.ToArgb())
            }
        }
        if ($chickenColors.Count -ne 4) { throw "Chicken.png must contain exactly four opaque palette colors; found $($chickenColors.Count)." }
    }
    finally { $chickenBitmap.Dispose() }

    Add-Type -AssemblyName System.Drawing
    $cornBitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\Corn.png'))
    try {
        if ($cornBitmap.Width -ne 512 -or $cornBitmap.Height -ne 512) { throw 'Corn.png must be the clean-room 512x512 crop atlas.' }
        if ($cornBitmap.PixelFormat -ne [Drawing.Imaging.PixelFormat]::Format32bppArgb) { throw 'Corn.png must be 32-bit RGBA (Format32bppArgb).' }
        for ($slot = 0; $slot -le 9; $slot++) {
            $originX = ($slot % 16) * 32
            $originY = [int]([Math]::Floor($slot / 16)) * 32
            $opaque = 0
            for ($x = 0; $x -lt 32; $x++) {
                for ($y = 0; $y -lt 32; $y++) {
                    if ($cornBitmap.GetPixel($originX + $x, $originY + $y).A -gt 0) { $opaque++ }
                }
            }
            if ($opaque -eq 0) { throw "Corn.png texture slot $slot is empty; the crop atlas must fill slots 0..9." }
        }
        foreach ($emptySlot in @(10, 15)) {
            $originX = ($emptySlot % 16) * 32
            $originY = [int]([Math]::Floor($emptySlot / 16)) * 32
            $any = $false
            for ($x = 0; $x -lt 32; $x++) {
                for ($y = 0; $y -lt 32; $y++) {
                    if ($cornBitmap.GetPixel($originX + $x, $originY + $y).A -gt 0) { $any = $true }
                }
            }
            if ($any) { throw "Corn.png slot $emptySlot must be transparent; only corn tiles 0..9 belong to this atlas." }
        }
    }
    finally { $cornBitmap.Dispose() }

    Add-Type -AssemblyName System.Drawing
    $bambooBitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\Bamboo.png'))
    try {
        if ($bambooBitmap.Width -ne 512 -or $bambooBitmap.Height -ne 512) { throw 'Bamboo.png must be the clean-room 512x512 bamboo atlas.' }
        if ($bambooBitmap.PixelFormat -ne [Drawing.Imaging.PixelFormat]::Format32bppArgb) { throw 'Bamboo.png must be 32-bit RGBA (Format32bppArgb).' }
        for ($slot = 0; $slot -le 2; $slot++) {
            $originX = ($slot % 16) * 32
            $originY = [int]([Math]::Floor($slot / 16)) * 32
            $opaque = 0
            for ($x = 0; $x -lt 32; $x++) {
                for ($y = 0; $y -lt 32; $y++) {
                    if ($bambooBitmap.GetPixel($originX + $x, $originY + $y).A -gt 0) { $opaque++ }
                }
            }
            if ($opaque -eq 0) { throw "Bamboo.png texture slot $slot is empty; the atlas must draw the base plant, upper culm and building cube in slots 0..2." }
            if ($slot -eq 2 -and $opaque -ne 1024) { throw 'Bamboo.png slot 2 (the solid building cube face) must be fully opaque.' }
        }
        $spareOriginX = (5 % 16) * 32
        $spareOriginY = [int]([Math]::Floor(5 / 16)) * 32
        $spareAny = $false
        for ($x = 0; $x -lt 32; $x++) {
            for ($y = 0; $y -lt 32; $y++) {
                if ($bambooBitmap.GetPixel($spareOriginX + $x, $spareOriginY + $y).A -gt 0) { $spareAny = $true }
            }
        }
        if ($spareAny) { throw 'Bamboo.png slot 5 must be transparent; only bamboo tiles 0..2 belong to this atlas.' }
    }
    finally { $bambooBitmap.Dispose() }

    Add-Type -AssemblyName System.Drawing
    $flaxBitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\Flax.png'))
    try {
        if ($flaxBitmap.Width -ne 512 -or $flaxBitmap.Height -ne 512) { throw 'Flax.png must be the clean-room 512x512 flax atlas.' }
        if ($flaxBitmap.PixelFormat -ne [Drawing.Imaging.PixelFormat]::Format32bppArgb) { throw 'Flax.png must be 32-bit RGBA (Format32bppArgb).' }
        for ($slot = 0; $slot -le 8; $slot++) {
            $originX = ($slot % 16) * 32
            $originY = [int]([Math]::Floor($slot / 16)) * 32
            $opaque = 0
            for ($x = 0; $x -lt 32; $x++) {
                for ($y = 0; $y -lt 32; $y++) {
                    if ($flaxBitmap.GetPixel($originX + $x, $originY + $y).A -gt 0) { $opaque++ }
                }
            }
            if ($opaque -eq 0) { throw "Flax.png texture slot $slot is empty; the atlas must fill plant stages 0..7 and the fiber bundle in slot 8." }
        }
        $flaxSpareX = (12 % 16) * 32
        $flaxSpareY = [int]([Math]::Floor(12 / 16)) * 32
        $flaxSpareAny = $false
        for ($x = 0; $x -lt 32; $x++) {
            for ($y = 0; $y -lt 32; $y++) {
                if ($flaxBitmap.GetPixel($flaxSpareX + $x, $flaxSpareY + $y).A -gt 0) { $flaxSpareAny = $true }
            }
        }
        if ($flaxSpareAny) { throw 'Flax.png slot 12 must be transparent; only flax tiles 0..8 belong to this atlas.' }
    }
    finally { $flaxBitmap.Dispose() }

    Add-Type -AssemblyName System.Drawing
    $sunflowerBitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\Sunflower.png'))
    try {
        if ($sunflowerBitmap.Width -ne 512 -or $sunflowerBitmap.Height -ne 512) { throw 'Sunflower.png must be the clean-room 512x512 sunflower atlas.' }
        if ($sunflowerBitmap.PixelFormat -ne [Drawing.Imaging.PixelFormat]::Format32bppArgb) { throw 'Sunflower.png must be 32-bit RGBA (Format32bppArgb).' }
        for ($slot = 0; $slot -le 8; $slot++) {
            $originX = ($slot % 16) * 32
            $originY = [int]([Math]::Floor($slot / 16)) * 32
            $opaque = 0
            for ($x = 0; $x -lt 32; $x++) {
                for ($y = 0; $y -lt 32; $y++) {
                    if ($sunflowerBitmap.GetPixel($originX + $x, $originY + $y).A -gt 0) { $opaque++ }
                }
            }
            if ($opaque -eq 0) { throw "Sunflower.png texture slot $slot is empty; the atlas must fill plant stages 0..7 and the seed pile in slot 8." }
        }
        $sunflowerSpareX = (12 % 16) * 32
        $sunflowerSpareY = [int]([Math]::Floor(12 / 16)) * 32
        $sunflowerSpareAny = $false
        for ($x = 0; $x -lt 32; $x++) {
            for ($y = 0; $y -lt 32; $y++) {
                if ($sunflowerBitmap.GetPixel($sunflowerSpareX + $x, $sunflowerSpareY + $y).A -gt 0) { $sunflowerSpareAny = $true }
            }
        }
        if ($sunflowerSpareAny) { throw 'Sunflower.png slot 12 must be transparent; only sunflower tiles 0..8 belong to this atlas.' }
    }
    finally { $sunflowerBitmap.Dispose() }

    Add-Type -AssemblyName System.Drawing
    $appleBitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\Apple.png'))
    try {
        if ($appleBitmap.Width -ne 512 -or $appleBitmap.Height -ne 512) { throw 'Apple.png must be the clean-room 512x512 apple atlas.' }
        if ($appleBitmap.PixelFormat -ne [Drawing.Imaging.PixelFormat]::Format32bppArgb) { throw 'Apple.png must be 32-bit RGBA (Format32bppArgb).' }
        for ($slot = 0; $slot -le 9; $slot++) {
            $originX = ($slot % 16) * 32
            $originY = [int]([Math]::Floor($slot / 16)) * 32
            $opaque = 0
            for ($x = 0; $x -lt 32; $x++) {
                for ($y = 0; $y -lt 32; $y++) {
                    if ($appleBitmap.GetPixel($originX + $x, $originY + $y).A -gt 0) { $opaque++ }
                }
            }
            if ($opaque -eq 0) { throw "Apple.png texture slot $slot is empty; the atlas must fill plant stages 0..7, the fruiting canopy in slot 8 and the apple fruit in slot 9." }
        }
        $appleSpareX = (12 % 16) * 32
        $appleSpareY = [int]([Math]::Floor(12 / 16)) * 32
        $appleSpareAny = $false
        for ($x = 0; $x -lt 32; $x++) {
            for ($y = 0; $y -lt 32; $y++) {
                if ($appleBitmap.GetPixel($appleSpareX + $x, $appleSpareY + $y).A -gt 0) { $appleSpareAny = $true }
            }
        }
        if ($appleSpareAny) { throw 'Apple.png slot 12 must be transparent; only apple tiles 0..9 belong to this atlas.' }
    }
    finally { $appleBitmap.Dispose() }

    [xml]$offroadModel = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Models\OffroadCar.dae')
    if ($offroadModel.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') { throw 'OffroadCar.dae must be Y_UP.' }
    $offroadNodes = @($offroadModel.SelectNodes("//*[local-name()='visual_scene']/*[local-name()='node']"))
    if ($offroadNodes.Count -ne 1 -or $offroadNodes[0].GetAttribute('name') -ne 'OffroadCar') {
        throw 'OffroadCar.dae must use exactly one OffroadCar scene node.'
    }
    $offroadMatrix = ($offroadNodes[0].SelectSingleNode("./*[local-name()='matrix']").InnerText -replace '\s+',' ').Trim()
    if ($offroadMatrix -ne '1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1') { throw 'OffroadCar.dae root matrix must be identity.' }
    $offroadGeometries = @($offroadModel.SelectNodes("//*[local-name()='geometry']"))
    $offroadInstances = @($offroadNodes[0].SelectNodes("./*[local-name()='instance_geometry']"))
    if ($offroadGeometries.Count -ne 24 -or $offroadInstances.Count -ne 24) {
        throw "OffroadCar.dae must contain/reference 24 clean-room parts; found $($offroadGeometries.Count)/$($offroadInstances.Count)."
    }
    $offroadGeometryIds = @($offroadGeometries | ForEach-Object { $_.GetAttribute('id') })
    $offroadInstanceIds = @($offroadInstances | ForEach-Object { $_.GetAttribute('url').TrimStart('#') })
    if (($offroadGeometryIds | Select-Object -Unique).Count -ne 24 -or
        ($offroadInstanceIds | Select-Object -Unique).Count -ne 24 -or
        @($offroadGeometryIds | Where-Object { $offroadInstanceIds -notcontains $_ }).Count -ne 0) {
        throw 'OffroadCar.dae geometry IDs must be unique and each must be referenced exactly once by the root node.'
    }
    $offroadCoordinates = New-Object System.Collections.Generic.List[double]
    foreach ($geometry in $offroadGeometries) {
        $positions = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'positions-array')]")
        $triangles = $geometry.SelectSingleNode(".//*[local-name()='triangles']")
        if ((@($positions.InnerText -split '\s+' | Where-Object { $_ })).Count -ne 24 -or $triangles.GetAttribute('count') -ne '12') {
            throw "OffroadCar.dae geometry $($geometry.GetAttribute('name')) is not an audited 8-vertex/12-triangle cuboid."
        }
        foreach ($semantic in @('VERTEX','NORMAL','TEXCOORD')) {
            if ($null -eq $triangles.SelectSingleNode("./*[local-name()='input' and @semantic='$semantic']")) {
                throw "OffroadCar.dae geometry $($geometry.GetAttribute('name')) is missing $semantic data."
            }
        }
        $indices = @($triangles.SelectSingleNode("./*[local-name()='p']").InnerText -split '\s+' | Where-Object { $_ } | ForEach-Object { [int]$_ })
        if ($indices.Count -ne 108) { throw "OffroadCar.dae geometry $($geometry.GetAttribute('name')) must contain 108 interleaved indices." }
        for ($i=0; $i -lt $indices.Count; $i+=3) {
            if ($indices[$i] -lt 0 -or $indices[$i] -gt 7 -or $indices[$i+1] -lt 0 -or $indices[$i+1] -gt 5 -or $indices[$i+2] -ne 0) {
                throw "OffroadCar.dae geometry $($geometry.GetAttribute('name')) contains an out-of-range face index."
            }
        }
        foreach ($coordinate in @($positions.InnerText -split '\s+' | Where-Object { $_ })) {
            $offroadCoordinates.Add([double]::Parse($coordinate,[Globalization.CultureInfo]::InvariantCulture))
        }
    }
    $offroadX = for ($i=0; $i -lt $offroadCoordinates.Count; $i+=3) { $offroadCoordinates[$i] }
    $offroadY = for ($i=1; $i -lt $offroadCoordinates.Count; $i+=3) { $offroadCoordinates[$i] }
    $offroadZ = for ($i=2; $i -lt $offroadCoordinates.Count; $i+=3) { $offroadCoordinates[$i] }
    if (($offroadX | Measure-Object -Minimum).Minimum -lt -1.2 -or ($offroadX | Measure-Object -Maximum).Maximum -gt 1.2 -or
        ($offroadY | Measure-Object -Minimum).Minimum -lt 0 -or ($offroadY | Measure-Object -Maximum).Maximum -gt 1.55 -or
        ($offroadZ | Measure-Object -Minimum).Minimum -lt -2 -or ($offroadZ | Measure-Object -Maximum).Maximum -gt 2) {
        throw 'OffroadCar.dae visible geometry exceeds the 2.4,1.55,4 ComponentBody.'
    }
    $offroadStream = [IO.File]::OpenRead((Join-Path $projectDir 'Assets\Models\OffroadCar.dae'))
    try { $targetOffroadModel = $targetModelLoad.Invoke($null,@($offroadStream)) }
    finally { $offroadStream.Dispose() }
    if ($targetOffroadModel.Bones.Count -ne 1 -or $targetOffroadModel.Meshes.Count -ne 24) {
        throw "Target Engine must import OffroadCar.dae as 1 bone/24 meshes; found $($targetOffroadModel.Bones.Count)/$($targetOffroadModel.Meshes.Count)."
    }
    $offroadBitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\OffroadCar.png'))
    try {
        if ($offroadBitmap.Width -ne 40 -or $offroadBitmap.Height -ne 8) { throw 'OffroadCar.png must be 40x8.' }
        $offroadColors = New-Object System.Collections.Generic.HashSet[int]
        for ($x=0; $x -lt 40; $x++) { for ($y=0; $y -lt 8; $y++) {
            $pixel = $offroadBitmap.GetPixel($x,$y)
            if ($pixel.A -ne 255) { throw "OffroadCar.png contains transparency at $x,$y." }
            [void]$offroadColors.Add($pixel.ToArgb())
        }}
        if ($offroadColors.Count -ne 5) { throw "OffroadCar.png must contain five opaque palette colors; found $($offroadColors.Count)." }
    }
    finally { $offroadBitmap.Dispose() }

    [xml]$helicopterModel = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Models\Helicopter.dae')
    if ($helicopterModel.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') { throw 'Helicopter.dae must be Y_UP.' }
    $helicopterRoot = $helicopterModel.SelectSingleNode("//*[local-name()='visual_scene']/*[local-name()='node' and @name='HelicopterRoot']")
    if ($null -eq $helicopterRoot) { throw 'Helicopter.dae is missing the HelicopterRoot bone.' }
    $visualRoot = $helicopterRoot.SelectSingleNode("./*[local-name()='node' and @name='VisualRoot']")
    $mainRotor = $visualRoot.SelectSingleNode("./*[local-name()='node' and @name='MainRotor']")
    $tailRotor = $visualRoot.SelectSingleNode("./*[local-name()='node' and @name='TailRotor']")
    $mainRotorBlur = $visualRoot.SelectSingleNode("./*[local-name()='node' and @name='MainRotorBlur']")
    $tailRotorBlur = $visualRoot.SelectSingleNode("./*[local-name()='node' and @name='TailRotorBlur']")
    if ($null -eq $visualRoot -or $null -eq $mainRotor -or $null -eq $tailRotor -or
        $null -eq $mainRotorBlur -or $null -eq $tailRotorBlur -or
        @($helicopterModel.SelectNodes("//*[local-name()='visual_scene']//*[local-name()='node']")).Count -ne 6) {
        throw 'Helicopter.dae must use exactly HelicopterRoot, VisualRoot, two rotor and two rotor-blur bones.'
    }
    if (($visualRoot.SelectSingleNode("./*[local-name()='translate']").InnerText -replace '\s+',' ').Trim() -ne '0 1 -0.55' -or
        ($mainRotor.SelectSingleNode("./*[local-name()='translate']").InnerText -replace '\s+',' ').Trim() -ne '0 0.80 0.25' -or
        ($tailRotor.SelectSingleNode("./*[local-name()='translate']").InnerText -replace '\s+',' ').Trim() -ne '0.30 0.20 2.92' -or
        ($mainRotorBlur.SelectSingleNode("./*[local-name()='translate']").InnerText -replace '\s+',' ').Trim() -ne '0 0.80 0.25' -or
        ($tailRotorBlur.SelectSingleNode("./*[local-name()='translate']").InnerText -replace '\s+',' ').Trim() -ne '0.30 0.20 2.92') {
        throw 'Helicopter visual attitude and rotor pivot transforms differ from the audited V0.4 layout.'
    }
    $helicopterGeometries = @($helicopterModel.SelectNodes("//*[local-name()='geometry']"))
    if ($helicopterGeometries.Count -ne 5) { throw "Helicopter.dae must contain five merged draw meshes; found $($helicopterGeometries.Count)." }
    $expectedHelicopterMeshes = @('VisualRoot','MainRotor','TailRotor','MainRotorBlur','TailRotorBlur')
    if ((@($helicopterGeometries | ForEach-Object { $_.GetAttribute('name') } | Sort-Object) -join ',') -ne (($expectedHelicopterMeshes | Sort-Object) -join ',')) {
        throw 'Helicopter.dae merged mesh names differ from the audited V0.4 layout.'
    }
    foreach ($geometry in $helicopterGeometries) {
        $positions = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'positions-array')]")
        $normals = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'normals-array')]")
        $uvs = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'uv-array')]")
        $triangles = $geometry.SelectSingleNode(".//*[local-name()='triangles']")
        $positionCount = @($positions.InnerText -split '\s+' | Where-Object { $_ }).Count
        $normalCount = @($normals.InnerText -split '\s+' | Where-Object { $_ }).Count
        $uvCount = @($uvs.InnerText -split '\s+' | Where-Object { $_ }).Count
        $triangleCount = [int]$triangles.GetAttribute('count')
        $indexCount = @($triangles.SelectSingleNode("./*[local-name()='p']").InnerText -split '\s+' | Where-Object { $_ }).Count
        if ($positionCount -le 0 -or ($positionCount % 9) -ne 0 -or $normalCount -ne $positionCount -or
            $uvCount -ne (($positionCount / 3) * 2) -or $indexCount -ne ($triangleCount * 9)) {
            throw "Helicopter geometry $($geometry.GetAttribute('name')) has inconsistent position, normal, UV or triangle-index data."
        }
        foreach ($semantic in @('VERTEX','NORMAL','TEXCOORD')) {
            if ($null -eq $triangles.SelectSingleNode("./*[local-name()='input' and @semantic='$semantic']")) {
                throw "Helicopter geometry $($geometry.GetAttribute('name')) is missing $semantic data."
            }
        }
    }
    $helicopterStream = [IO.File]::OpenRead((Join-Path $projectDir 'Assets\Models\Helicopter.dae'))
    try { $targetHelicopterModel = $targetModelLoad.Invoke($null,@($helicopterStream)) }
    finally { $helicopterStream.Dispose() }
    if ($targetHelicopterModel.Bones.Count -ne 6 -or $targetHelicopterModel.Meshes.Count -ne 5) {
        throw "Target Engine must import Helicopter.dae as 6 bones/5 meshes; found $($targetHelicopterModel.Bones.Count)/$($targetHelicopterModel.Meshes.Count)."
    }
    foreach ($boneName in @('HelicopterRoot','VisualRoot','MainRotor','TailRotor','MainRotorBlur','TailRotorBlur')) {
        if (-not ($targetHelicopterModel.Bones | Where-Object { $_.Name -eq $boneName })) { throw "Target Engine did not import helicopter bone $boneName." }
    }
    Add-Type -AssemblyName System.Drawing
    $helicopterBitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\Helicopter.png'))
    try {
        if ($helicopterBitmap.Width -ne 96 -or $helicopterBitmap.Height -ne 16) { throw 'Helicopter.png must be the audited 96x16 V0.4 palette.' }
        $helicopterColors = New-Object System.Collections.Generic.HashSet[int]
        for ($x=0;$x -lt 96;$x++) { for ($y=0;$y -lt 16;$y++) {
            $pixel=$helicopterBitmap.GetPixel($x,$y)
            if ($pixel.A -ne 255) { throw "Helicopter.png contains transparency at $x,$y." }
            [void]$helicopterColors.Add($pixel.ToArgb())
        }}
        if ($helicopterColors.Count -ne 12) { throw "Helicopter.png must contain twelve opaque palette colors; found $($helicopterColors.Count)." }
    }
    finally { $helicopterBitmap.Dispose() }
    $targetSoundDataType = $targetEngineAssembly.GetType('Engine.Media.SoundData', $true)
    $targetSoundLoad = $targetSoundDataType.GetMethod('Load', [Type[]]@([IO.Stream]))
    if ($null -eq $targetSoundLoad) { throw 'Target Engine.Media.SoundData.Load(Stream) ABI is missing.' }
    foreach ($cue in @('Wake','Rift','Collapse','Burst')) {
        $audioPath = Join-Path $projectDir "Assets\Audio\Abyss\Horizon$cue.wav"
        $audioStream = [IO.File]::OpenRead($audioPath)
        try { $decoded = $targetSoundLoad.Invoke($null,@($audioStream)) }
        finally { $audioStream.Dispose() }
        if ($decoded.ChannelsCount -ne 1 -or $decoded.SamplingFrequency -ne 22050 -or $decoded.Data.Length -ne 22050) { throw "Abyss horizon $cue must decode as one second of 22050 Hz mono PCM." }
        $entry = $archive.GetEntry("Assets/Audio/Abyss/Horizon$cue.wav")
        if ($null -eq $entry) { throw "Missing packaged horizon audio $cue." }
        $packaged = $entry.Open()
        try { $hash = [Security.Cryptography.SHA256]::Create(); try { $packageHash = [BitConverter]::ToString($hash.ComputeHash($packaged)) } finally { $hash.Dispose() } }
        finally { $packaged.Dispose() }
        $sourceHash = (Get-FileHash -LiteralPath $audioPath -Algorithm SHA256).Hash
        if ($packageHash.Replace('-','') -ne $sourceHash) { throw "Packaged horizon audio $cue differs from source." }
    }
    Write-Host 'Abyss horizon audio: four one-second sounds decode on target Engine and match package.'
    foreach ($cue in @('Shift','Hunt')) {
        $audioPath = Join-Path $projectDir "Assets\Audio\Abyss\Blink$cue.wav"
        $audioStream = [IO.File]::OpenRead($audioPath)
        try { $decoded = $targetSoundLoad.Invoke($null,@($audioStream)) } finally { $audioStream.Dispose() }
        if ($decoded.ChannelsCount -ne 1 -or $decoded.SamplingFrequency -ne 22050 -or $decoded.Data.Length -ne 22050) { throw "Abyss blink $cue must decode as one second of 22050 Hz mono PCM." }
        $entry = $archive.GetEntry("Assets/Audio/Abyss/Blink$cue.wav")
        if ($null -eq $entry) { throw "Missing packaged blink audio $cue." }
        $packaged = $entry.Open()
        try { $hash = [Security.Cryptography.SHA256]::Create(); try { $packageHash = [BitConverter]::ToString($hash.ComputeHash($packaged)) } finally { $hash.Dispose() } } finally { $packaged.Dispose() }
        if ($packageHash.Replace('-','') -ne (Get-FileHash -LiteralPath $audioPath -Algorithm SHA256).Hash) { throw "Packaged blink audio $cue differs from source." }
    }
    Write-Host 'Abyss blink audio: two sounds decode on target Engine and match package.'
    $helicopterSoundStream = [IO.File]::OpenRead((Join-Path $projectDir 'Assets\Audio\SCIEPortHelicopterLoop.wav'))
    try { $targetHelicopterSound = $targetSoundLoad.Invoke($null,@($helicopterSoundStream)) }
    finally { $helicopterSoundStream.Dispose() }
    if ($targetHelicopterSound.ChannelsCount -ne 1 -or $targetHelicopterSound.SamplingFrequency -ne 22050 -or $targetHelicopterSound.Data.Length -ne 22050) {
        throw "Target Engine must decode the helicopter loop as mono PCM, 22050 Hz, 22050 samples; found $($targetHelicopterSound.ChannelsCount) channels/$($targetHelicopterSound.SamplingFrequency) Hz/$($targetHelicopterSound.Data.Length) samples."
    }

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
    [xml]$minecartModel = Get-Content -Raw -Encoding UTF8 $stagedMinecart
    if ($null -eq $minecartModel.SelectSingleNode("//*[local-name()='node' and @name='obj1']")) {
        throw 'Normalized Minecart.dae is missing the required obj1 mesh node.'
    }
    if ($minecartModel.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') {
        throw 'Minecart.dae must be oriented Y_UP.'
    }
    $minecartInputs = @($minecartModel.SelectNodes("//*[local-name()='geometry']/*[local-name()='mesh']/*[local-name()='polylist']/*[local-name()='input']"))
    if (($minecartInputs | ForEach-Object { $_.GetAttribute('offset') }) -join ',' -ne '0,1,2') {
        throw 'Packaged Minecart.dae must use distinct VERTEX/NORMAL/TEXCOORD offsets 0,1,2.'
    }
    $minecartModelStream = [IO.File]::OpenRead($stagedMinecart)
    try { $targetMinecartModel = $targetModelLoad.Invoke($null, @($minecartModelStream)) }
    finally { $minecartModelStream.Dispose() }
    if ($targetMinecartModel.Bones.Count -ne 3 -or $targetMinecartModel.Meshes.Count -ne 1 -or
        $targetMinecartModel.Meshes[0].Name -ne 'obj1') {
        throw "Target Engine must import Minecart.dae as 3 bones/1 obj1 mesh; found $($targetMinecartModel.Bones.Count) bones/$($targetMinecartModel.Meshes.Count) meshes."
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

    # Stormhammer is an original production asset, not a placeholder. Lock its
    # topology budget, authored UVs, attachment metadata and target importer ABI.
    $stormhammerModelPath = Join-Path $projectDir 'Assets\Models\Stormhammer.dae'
    [xml]$stormhammerModel = Get-Content -Raw -Encoding UTF8 $stormhammerModelPath
    if ($stormhammerModel.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') {
        throw 'Stormhammer.dae must be Y_UP.'
    }
    $stormhammerRoots = @($stormhammerModel.SelectNodes("//*[local-name()='visual_scene']/*[local-name()='node']"))
    $stormhammerGeometries = @($stormhammerModel.SelectNodes("//*[local-name()='geometry']"))
    if ($stormhammerRoots.Count -ne 1 -or $stormhammerRoots[0].GetAttribute('name') -ne 'Stormhammer') {
        throw 'Stormhammer.dae must use one Stormhammer render bone.'
    }
    if ($stormhammerGeometries.Count -ne 30) {
        throw "Stormhammer.dae must contain 30 authored mesh structures; found $($stormhammerGeometries.Count)."
    }
    $stormhammerTriangleCount = 0
    foreach ($geometry in $stormhammerGeometries) {
        $triangles = $geometry.SelectSingleNode(".//*[local-name()='triangles']")
        $positions = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'-positions-array')]")
        $uvs = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'-uv-array')]")
        if ($null -eq $triangles -or $null -eq $positions -or $null -eq $uvs) {
            throw "Stormhammer geometry $($geometry.GetAttribute('name')) is missing triangles, positions or UVs."
        }
        $stormhammerTriangleCount += [int]$triangles.GetAttribute('count')
        $positionCount = [int]$positions.GetAttribute('count')
        $uvCount = [int]$uvs.GetAttribute('count')
        if ($positionCount % 9 -ne 0 -or $uvCount -ne ($positionCount / 3 * 2)) {
            throw "Stormhammer geometry $($geometry.GetAttribute('name')) has a non-bijective triangle UV layout."
        }
    }
    if ($stormhammerTriangleCount -ne 1368) {
        throw "Stormhammer triangle budget changed: expected 1368, found $stormhammerTriangleCount."
    }
    $attachmentNames = @($stormhammerModel.SelectNodes("//*[local-name()='attachment']") | ForEach-Object { $_.GetAttribute('name') })
    foreach ($attachment in @('Grip','StrikeLeft','StrikeRight','ArcFront','ArcBack','Pommel')) {
        if ($attachmentNames -notcontains $attachment) { throw "Stormhammer is missing $attachment attachment metadata." }
    }
    $stormhammerStream = [IO.File]::OpenRead($stormhammerModelPath)
    try { $targetStormhammerModel = $targetModelLoad.Invoke($null, @($stormhammerStream)) }
    finally { $stormhammerStream.Dispose() }
    if ($targetStormhammerModel.Bones.Count -ne 1 -or $targetStormhammerModel.Meshes.Count -ne 30) {
        throw "Target Engine must import Stormhammer.dae as 1 bone/30 meshes; found $($targetStormhammerModel.Bones.Count) bones/$($targetStormhammerModel.Meshes.Count) meshes."
    }

    $stormhammerTextureChecks = @(
        @{ Name = 'Stormhammer.png'; Width = 1024; Height = 1024 },
        @{ Name = 'StormhammerGlow.png'; Width = 1024; Height = 1024 },
        @{ Name = 'StormhammerEffects.png'; Width = 256; Height = 256 },
        @{ Name = 'LightningBeam.png'; Width = 128; Height = 16 },
        @{ Name = 'LightningRune.png'; Width = 256; Height = 256 }
    )
    foreach ($textureCheck in $stormhammerTextureChecks) {
        $bitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir ('Assets\Textures\' + $textureCheck.Name)))
        try {
            if ($bitmap.Width -ne $textureCheck.Width -or $bitmap.Height -ne $textureCheck.Height) {
                throw "$($textureCheck.Name) dimensions changed; expected $($textureCheck.Width)x$($textureCheck.Height)."
            }
        }
        finally { $bitmap.Dispose() }
    }
    $stormhammerAtlas = [Drawing.Bitmap]::FromFile((Join-Path $projectDir 'Assets\Textures\Stormhammer.png'))
    try {
        $atlasColors = New-Object System.Collections.Generic.HashSet[int]
        for ($y = 0; $y -lt 1024; $y += 8) {
            for ($x = 0; $x -lt 1024; $x += 8) { [void]$atlasColors.Add($stormhammerAtlas.GetPixel($x, $y).ToArgb()) }
        }
        if ($atlasColors.Count -lt 350) { throw "Stormhammer atlas lacks authored surface variation; sampled only $($atlasColors.Count) colors." }
    }
    finally { $stormhammerAtlas.Dispose() }
    foreach ($sound in @('Swing.wav','Charge.wav','Thunder.wav','Strike.wav','Rumble.wav')) {
        $soundBytes = [IO.File]::ReadAllBytes((Join-Path $projectDir ('Assets\Audio\Stormhammer\' + $sound)))
        if ($soundBytes.Length -lt 4096 -or [Text.Encoding]::ASCII.GetString($soundBytes, 0, 4) -ne 'RIFF' -or
            [Text.Encoding]::ASCII.GetString($soundBytes, 8, 4) -ne 'WAVE') {
            throw "Stormhammer audio $sound is not a production PCM WAV resource."
        }
    }

    # Mjolnir reuses Models/Stormhammer, so only the visible thunder spear still needs its
    # own production-asset contract. Validate it through the target importer, not XML alone.
    foreach ($awakenedModelCheck in @(
        @{ File = 'ThunderSpear.dae'; Root = 'ThunderSpear'; Meshes = 5; Triangles = 108; Attachments = @('Tip','Tail') }
    )) {
        $awakenedPath = Join-Path $projectDir ('Assets\Models\' + $awakenedModelCheck.File)
        [xml]$awakenedXml = Get-Content -Raw -Encoding UTF8 $awakenedPath
        if ($awakenedXml.SelectSingleNode("//*[local-name()='up_axis']").InnerText -ne 'Y_UP') { throw "$($awakenedModelCheck.File) must be Y_UP." }
        $root = @($awakenedXml.SelectNodes("//*[local-name()='visual_scene']/*[local-name()='node']"))
        $geometries = @($awakenedXml.SelectNodes("//*[local-name()='geometry']"))
        if ($root.Count -ne 1 -or $root[0].GetAttribute('name') -ne $awakenedModelCheck.Root) { throw "$($awakenedModelCheck.File) must have one $($awakenedModelCheck.Root) render bone." }
        if ($geometries.Count -ne $awakenedModelCheck.Meshes) { throw "$($awakenedModelCheck.File) mesh count changed: $($geometries.Count)." }
        $triangleCount = 0
        foreach ($geometry in $geometries) {
            $triangles = $geometry.SelectSingleNode(".//*[local-name()='triangles']")
            $positions = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'-positions-array')]")
            $uvs = $geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'-uv-array')]")
            if ($null -eq $triangles -or $null -eq $positions -or $null -eq $uvs) { throw "$($awakenedModelCheck.File) has incomplete geometry $($geometry.GetAttribute('name'))." }
            $triangleCount += [int]$triangles.GetAttribute('count')
            if ([int]$uvs.GetAttribute('count') -ne ([int]$positions.GetAttribute('count') / 3 * 2)) { throw "$($awakenedModelCheck.File) has invalid UV cardinality." }
        }
        if ($triangleCount -ne $awakenedModelCheck.Triangles) { throw "$($awakenedModelCheck.File) triangle count changed: $triangleCount." }
        $attachmentNames = @($awakenedXml.SelectNodes("//*[local-name()='attachment']") | ForEach-Object { $_.GetAttribute('name') })
        foreach ($attachment in $awakenedModelCheck.Attachments) { if ($attachmentNames -notcontains $attachment) { throw "$($awakenedModelCheck.File) missing attachment $attachment." } }
        $stream = [IO.File]::OpenRead($awakenedPath)
        try { $imported = $targetModelLoad.Invoke($null, @($stream)) } finally { $stream.Dispose() }
        if ($imported.Bones.Count -ne 1 -or $imported.Meshes.Count -ne $awakenedModelCheck.Meshes) { throw "Target Engine imports $($awakenedModelCheck.File) as $($imported.Bones.Count) bones/$($imported.Meshes.Count) meshes." }
    }

    foreach ($textureCheck in @(
        @{ Name = 'StormhammerAwakened.png'; Width = 1024; Height = 1024 },
        @{ Name = 'StormhammerAwakenedGlow.png'; Width = 1024; Height = 1024 },
        @{ Name = 'StormhammerAwakenedEffects.png'; Width = 256; Height = 256 },
        @{ Name = 'StormhammerAwakenedSkills.png'; Width = 2048; Height = 512 }
    )) {
        $bitmap = [Drawing.Bitmap]::FromFile((Join-Path $projectDir ('Assets\Textures\' + $textureCheck.Name)))
        try {
            if ($bitmap.Width -ne $textureCheck.Width -or $bitmap.Height -ne $textureCheck.Height) { throw "$($textureCheck.Name) dimensions changed." }
            if ($textureCheck.Name -eq 'StormhammerAwakenedSkills.png') {
                for ($slot = 0; $slot -lt 3; $slot++) {
                    $opaque = 0
                    for ($y = 0; $y -lt 512; $y += 8) { for ($x = $slot * 512; $x -lt ($slot + 1) * 512; $x += 8) { if ($bitmap.GetPixel($x,$y).A -gt 32) { $opaque++ } } }
                    if ($opaque -lt 500) { throw "Awakened skill icon slot $slot is empty or unreadable." }
                }
            }
        }
        finally { $bitmap.Dispose() }
    }

    $awakenedCsv = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Blocks\StormhammerAwakened.csv')
    if ($awakenedCsv -notmatch 'StormhammerAwakenedBlock;[^;]+;SCIEPort\.Artifacts;SCIEPortAwakenedStormhammerBehavior' -or $awakenedCsv -notmatch 'scieport-skywrath-v3' -or $awakenedCsv -notmatch ';FALSE;960;') {
        throw 'Skywrath V3.2 CSV identity, non-aimable skill-button mode, or durability changed.'
    }
    $awakenedSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\StormhammerSkillHud.cs')
    if (([regex]::Matches($awakenedSource, 'new SkillButton\(')).Count -ne 2) {
        throw 'Skywrath V3.2 HUD must instantiate exactly two active skills.'
    }
    $awakenedBehavior = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\SubsystemAwakenedStormhammerBlockBehavior.cs')
    foreach ($requiredCombatRule in @('PhysicalMeleePower = 40f','LightningMeleePower = 25f','RetaliationPower = 5f','JudgmentCooldown = 8f','UltimateCooldown = 55f')) {
        if (-not $awakenedBehavior.Contains($requiredCombatRule)) { throw "Skywrath V3.2 combat rule missing: $requiredCombatRule" }
    }
    foreach ($removedSkill in @('ThunderLance','ThunderDomain','雷狱天矛','雷霆王域')) {
        if ($awakenedBehavior.Contains($removedSkill) -or $awakenedSource.Contains($removedSkill)) { throw "Removed Skywrath skill remains active: $removedSkill" }
    }
    $skillPackageSource = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Features\Stormhammer\StormhammerSkillPackage.cs')
    if ($skillPackageSource -notmatch 'public\s+byte\s+ID\s*=>\s*243\s*;') {
        throw 'Skywrath V3.2 must use reserved package ID 243; ID 242 belongs to vehicle activation.'
    }
    foreach ($authoritativeField in @('TargetId','CastId','ServerTime')) {
        if (-not $skillPackageSource.Contains($authoritativeField)) { throw "Skywrath V3.2 authoritative event field missing: $authoritativeField" }
    }
    $packageIds = @{}
    Get-ChildItem -LiteralPath $projectDir -Recurse -Filter '*Package.cs' | ForEach-Object {
        $packageText = Get-Content -Raw -Encoding UTF8 $_.FullName
        $idMatch = [regex]::Match($packageText, 'public\s+byte\s+ID\s*=>\s*(\d+)\s*;')
        if ($idMatch.Success) {
            $packageId = [int]$idMatch.Groups[1].Value
            if ($packageIds.ContainsKey($packageId)) {
                throw "Duplicate SCIE-Port network package ID $packageId in $($_.Name) and $($packageIds[$packageId])."
            }
            $packageIds[$packageId] = $_.Name
        }
    }
    foreach ($requiredPackageId in @(240, 241, 242, 243)) {
        if (-not $packageIds.ContainsKey($requiredPackageId)) { throw "Required SCIE-Port network package ID $requiredPackageId is missing." }
    }
    $blockIds = @{}
    Get-ChildItem -LiteralPath $projectDir -Recurse -Filter '*.cs' | ForEach-Object {
        $blockText = Get-Content -Raw -Encoding UTF8 $_.FullName
        foreach ($blockMatch in [regex]::Matches($blockText, 'public\s+const\s+int\s+Index\s*=\s*(\d+)\s*;')) {
            $blockId = [int]$blockMatch.Groups[1].Value
            if ($blockIds.ContainsKey($blockId)) {
                throw "Duplicate SCIE-Port block ID $blockId in $($_.Name) and $($blockIds[$blockId])."
            }
            $blockIds[$blockId] = $_.Name
        }
    }
    if (-not $blockIds.ContainsKey(941) -or -not $blockIds.ContainsKey(942) -or -not $blockIds.ContainsKey(943)) {
        throw 'Helicopter, awakened Stormhammer, or base Stormhammer block ID is missing.'
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

if ([IO.Path]::GetFileName($package) -cne '荒野探险家.netmod') { throw 'Release filename must be 荒野探险家.netmod.' }
& (Join-Path $PSScriptRoot 'Test-PortInventory.ps1') -GameDir $GameDir -Package $package
$versionedPackage = Join-Path $artifacts ("荒野探险家-" + $modInfo.Version + '.netmod')
Copy-Item -LiteralPath $package -Destination $versionedPackage -Force
if ((Get-FileHash -LiteralPath $package).Hash -ne (Get-FileHash -LiteralPath $versionedPackage).Hash) { throw 'Versioned release differs from validated package.' }
& (Join-Path $PSScriptRoot 'Test-WorldCut.ps1') -GameDir $GameDir -Package $package
& (Join-Path $PSScriptRoot 'Test-AbyssHud.ps1') -GameDir $GameDir -Package $package
& (Join-Path $PSScriptRoot 'Test-AbyssSpaceV2.ps1') -GameDir $GameDir -Package $package
& (Join-Path $PSScriptRoot 'Test-PoseidonV2Assets.ps1') -GameDir $GameDir -Package $package

Write-Host "Package: $package"
Write-Host "Versioned package: $versionedPackage"
Write-Host "Entries: $($entries -join ', ')"
Write-Host "SHA-256: $((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash)"
