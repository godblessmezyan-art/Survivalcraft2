param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,

    [string]$OutputPath = ""
)

$ErrorActionPreference = 'Stop'
$gameDirPath = (Resolve-Path -LiteralPath $GameDir).Path
$assemblyPath = Join-Path $gameDirPath 'Survivalcraft.exe'
$contentPath = Join-Path $gameDirPath 'Content.scpak'
if (!(Test-Path -LiteralPath $assemblyPath) -or !(Test-Path -LiteralPath $contentPath)) {
    throw "GameDir must contain Survivalcraft.exe and Content.scpak: $gameDirPath"
}

$expectedAssemblyHash = '3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD'
$assemblyHash = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash
if ($assemblyHash -ne $expectedAssemblyHash) {
    throw "Survivalcraft.exe is not the audited x25.01.17 public Windows assembly. Expected $expectedAssemblyHash, got $assemblyHash"
}

function Read-BlocksDataFromScpak([string]$path) {
    Add-Type -AssemblyName System.IO.Compression
    $source = [IO.File]::ReadAllBytes($path)
    $header = [Text.Encoding]::UTF8.GetBytes('再乱改就跑路，谁也别想玩！')
    if ($source.Length -lt $header.Length) { throw 'Content.scpak is truncated.' }
    for ($i = 0; $i -lt $header.Length; $i++) {
        if ($source[$i] -ne $header[$i]) {
            throw 'Content.scpak header/decipher format does not match x25.01.17.'
        }
    }

    # Exact inverse of SurvivalCraftModEntity.GetDecipherStream in the target assembly.
    $length = $source.Length - $header.Length
    $half = [int](($length + 1) / 2)
    $decoded = New-Object byte[] $length
    $even = 0
    $odd = 0
    for ($j = 0; $j -lt $length; $j++) {
        if (($j % 2) -eq 0) {
            $decoded[$j] = $source[$header.Length + $even]
            $even++
        }
        else {
            $decoded[$j] = $source[$header.Length + $half + $odd]
            $odd++
        }
    }

    $memory = New-Object IO.MemoryStream(,$decoded)
    $zip = New-Object IO.Compression.ZipArchive($memory, [IO.Compression.ZipArchiveMode]::Read, $false)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -eq 'Assets/BlocksData.txt' } | Select-Object -First 1
        if ($null -eq $entry) { throw 'Assets/BlocksData.txt is missing from Content.scpak.' }
        $reader = New-Object IO.StreamReader($entry.Open(), [Text.Encoding]::UTF8)
        try { return $reader.ReadToEnd() }
        finally { $reader.Dispose() }
    }
    finally {
        $zip.Dispose()
        $memory.Dispose()
    }
}

$blocksText = Read-BlocksDataFromScpak $contentPath
$rows = @($blocksText -split "`n" | ConvertFrom-Csv -Delimiter ';')
$rowsByName = @{}
foreach ($row in $rows) {
    if (![string]::IsNullOrEmpty($row.'Class Name')) { $rowsByName[$row.'Class Name'] = $row }
}

# Read the authoring allow-list from the actual Core metadata file so the audit cannot
# accidentally claim a type is open when the runtime registry does not contain it.
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$capabilitySource = Get-Content -Raw -Encoding UTF8 (Join-Path $repoRoot 'src\BuilderAssistant\Core\Blueprints\VanillaBlockCapabilities.cs')
$supported = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($match in [regex]::Matches($capabilitySource, '"([A-Za-z0-9]+Block)"')) {
    [void]$supported.Add($match.Groups[1].Value)
}

$resolve = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = New-Object Reflection.AssemblyName($eventArgs.Name)
    $candidate = Join-Path $gameDirPath ($name.Name + '.dll')
    if (Test-Path -LiteralPath $candidate) {
        return [Reflection.Assembly]::ReflectionOnlyLoadFrom($candidate)
    }
    return $null
}
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolve)
$assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($assemblyPath)
try { $types = $assembly.GetTypes() }
catch [Reflection.ReflectionTypeLoadException] { $types = @($_.Exception.Types | Where-Object { $null -ne $_ }) }

function Get-AuditClass([string]$typeName, [string]$chain, [string]$baseName, [string]$interactive) {
    if ($supported.Contains($typeName)) { return '第一批已开放（受族/DATA限制）' }
    if ($chain -match 'FluidBlock|WaterPlantBlock|BottomSuckerBlock') { return '流体/水生子系统：暂不开放' }
    if ($chain -match 'MountedElectricElementBlock|WireThroughBlock' -or
        $typeName -match '^(Wire|Battery|Piston|PistonHead)') { return '电路/复杂状态：暂不开放' }
    if ($chain -match 'SignBlock|FurnaceBlock|GunpowderKegBlock' -or
        $typeName -match '^(Chest|Dispenser|CraftingTable|Furniture|Campfire)') { return '实体/容器/持久状态：暂不开放' }
    if ($chain -match 'TrapdoorBlock|FenceGateBlock|LadderBlock' -or
        $typeName -match '^(Torch|Ivy|Carpet|Snow)') { return '依附/新朝向族：待编解码与支撑校验' }
    if ($typeName -match '^(Sand|Gravel|Dirt|Soil|Grass|Ice|FallenLeaves|OakLeaves|BirchLeaves|MimosaLeaves|PoplarLeaves|TallGrass|Rye|RedFlower|PurpleFlower|WhiteFlower|DryBush|LargeDryBush|Sapling|Cactus|Pumpkin|RottenPumpkin|Seeds|Cotton)') {
        return '重力/生长/季节/世界更新：暂不开放'
    }
    if ($baseName -match 'CubeBlock|PaintedCubeBlock|AlphaTestCubeBlock') { return '静态候选：待逐类型行为复核' }
    if ($interactive -eq 'TRUE') { return '交互/复杂状态：暂不开放' }
    return '专用几何/行为：待取证'
}

$audit = @()
foreach ($type in $types) {
    $row = $rowsByName[$type.Name]
    if ($null -eq $row -or $row.IsPlaceable -ne 'TRUE') { continue }

    $chainParts = @()
    $baseType = $type.BaseType
    while ($null -ne $baseType -and $baseType.FullName -ne 'System.Object') {
        $chainParts += $baseType.Name
        $baseType = $baseType.BaseType
    }
    $chain = $chainParts -join '>'
    $indexField = $type.GetField('Index', [Reflection.BindingFlags]'Public,Static,DeclaredOnly')
    $index = -1
    if ($null -ne $indexField -and $indexField.IsLiteral) { $index = [int]$indexField.GetRawConstantValue() }
    $audit += [pscustomobject]@{
        Index = $index
        Type = $type.Name
        Base = $type.BaseType.Name
        GameCategory = $row.DefaultCategory
        Interactive = $row.DefaultIsInteractive
        AbiChain = $chain
        Decision = Get-AuditClass $type.Name $chain $type.BaseType.Name $row.DefaultIsInteractive
    }
}
$audit = @($audit | Sort-Object Index)
if ($audit.Count -ne 165) { throw "Expected 165 placeable rows, found $($audit.Count)." }

$lines = New-Object 'System.Collections.Generic.List[string]'
$lines.Add('# SCNET x25.01.17 原版可放置方块审计')
$lines.Add('')
$lines.Add('> 此表由 `scripts/Audit-BuilderAssistantBlocks.ps1` 从目标程序集与同包 `Content.scpak/Assets/BlocksData.txt` 生成。分类依据是实际 CLR 继承链、静态 `Index`、`IsPlaceable` 与交互字段；“开放”还必须进入代码能力目录并通过族/DATA校验。')
$lines.Add('')
$lines.Add('- `Survivalcraft.exe` SHA-256：`' + $assemblyHash + '`')
$lines.Add('- 具体 `Block` 类型：258；`BlocksData.IsPlaceable=TRUE`：165。')
$lines.Add('- 第一批开放：' + $supported.Count + ' 种；其余方块仍拒绝，不以类型名称猜测行为。')
$lines.Add('- 本证据对应公开 Windows x25.01.17 包，不替代 7723 Android 重打包哈希与真机验证。')
$lines.Add('')
$lines.Add('## 六个核心组件审计结论')
$lines.Add('')
$lines.Add('| 组件 | 审计发现 | v0.16.0 处置 |')
$lines.Add('|---|---|---|')
$lines.Add('| `VanillaBlockPalette` | 39 项硬编码表同时承担能力声明与运行时解析；解析只收 key+data，无法核对 cell 的 `f`。 | 拆成游戏无关 `VanillaBlockCapabilities` 与核对真实 `BlocksManager` 类型/基类的 `VanillaBlockRegistry`；palette 只负责严格解析。 |')
$lines.Add('| `BlockOrientation` | `FamilyFromTag` 对未知值回退 Cube；`Normalize` 会掩掉非法高位，可能隐藏颜色/状态损失。 | 增加严格 tag 解析、每族 authored DATA 范围与世界 DATA 解码；只允许剥离已证实的楼梯 corner 派生位。 |')
$lines.Add('| `BlueprintJson` | 只拒绝未知 `f` 和负数 `d`，未检查 `f` 与材料真实族，也忽略 `materials[].block` 冲突。 | 对族、DATA、key/block 映射逐格拒绝；schemaVersion 1/2 均保留。 |')
$lines.Add('| `BlueprintCopier` | 受支持类型的 live DATA 会经 Normalize 截断；彩色块和横向原木可能被低质量复制。 | 复制前按族解码；颜色、横向原木或未知位明确跳过并按 `Type(DATA=n)` 报告。 |')
$lines.Add('| `BuildPlanner` | 最终门只调用 `TryResolve(key,data)`，JSON 外构造的错误 family 可绕过前置一致性检查。 | 最终计划阶段改为 `TryResolve(key,family,data)`，任何入口都必须通过相同能力校验；10000/32/64、取消、撤销、区块与权限机制未改。 |')
$lines.Add('| `GameTerrainGateway` | `ChangeCell` 主线程写入、区块 Valid+Cells 检查和 exact terrain-value 撤销适合普通方块；不能保存容器实体、电路队列或流体级联。 | 第一阶段不改写入路径；复杂状态/实体/流体继续拒绝，直至能做跨子系统事务与撤销。 |')
$lines.Add('')
$lines.Add('| ID | CLR 类型 | 直接基类 | 游戏分类 | 交互 | 第一阶段结论 |')
$lines.Add('|---:|---|---|---|:---:|---|')
foreach ($item in $audit) {
    $interactiveLabel = if ($item.Interactive -eq 'TRUE') { '是' } else { '否' }
    $lines.Add('| ' + $item.Index + ' | `' + $item.Type + '` | `' + $item.Base + '` | ' +
        $item.GameCategory + ' | ' + $interactiveLabel + ' | ' + $item.Decision + ' |')
}
$lines.Add('')
$lines.Add('## 证据边界')
$lines.Add('')
$lines.Add('- `IsPlaceable` 只说明游戏允许放置，不说明仅靠 terrain DATA 就能完整复制；容器内容、方块实体、电路节点、随机生长、流体传播与依附完整性需要额外子系统。')
$lines.Add('- `CubeBlock` 继承关系也不等于“无状态立方体”；例如箱子、土壤、积雪、彩色方块仍有额外 DATA 或行为，因此不会自动开放。')
$lines.Add('- 第一批开放类型的精确族、合法 DATA 和复制限制以 `docs/blueprint-block-reference.md` 为准。')
$lines.Add('')
$lines.Add('## 剩余覆盖路线图')
$lines.Add('')
$lines.Add('1. **颜色状态与更多静态候选**：为 PaintedCube/Stairs/Slab/Fence 设计显式颜色状态（优先预留 schema v3 的 `state` 对象），逐一验证存读档、旋转与复制；在此之前带颜色 DATA 一律拒绝。')
$lines.Add('2. **简单朝向/依附族**：Trapdoor、FenceGate、Ladder、Torch、Carpet、Ivy 等分别建立 codec，并取证 `SubsystemTrapdoorBlockBehavior`、`SubsystemFenceGateBlockBehavior`、`SubsystemLadderBlockBehavior`、`SubsystemTorchBlockBehavior` 的支撑检查、邻格删除和撤销顺序。')
$lines.Add('3. **世界更新型方块**：重力、植物、树苗、雪/冰、季节叶片需要验证随机 tick/轮询、移动方块及级联变化；撤销必须捕获由子系统产生的额外格，不能只保存蓝图 footprint。')
$lines.Add('4. **容器/实体/文本**：Chest、Furnace、Dispenser、Sign、Furniture 等需要 `SubsystemBlockEntities`、对应 Behavior、组件库存/文本序列化和实体创建/删除事务；撤销必须同时恢复 terrain value 与实体 payload。')
$lines.Add('5. **电路**：需要 `SubsystemElectricity`/`ElectricElement` 的创建、连接、排队更新和持久状态接口；仅写 terrain DATA 不足以证明电路正确。')
$lines.Add('6. **流体与水生方块**：需要 `SubsystemFluidBlockBehavior`、`SubsystemWaterBlockBehavior`、`SubsystemMagmaBlockBehavior` 的传播/衰减边界和多格撤销模型；在能冻结传播并记录级联变化前保持不支持。')

if ([string]::IsNullOrEmpty($OutputPath)) {
    $lines -join "`r`n"
}
else {
    $fullOutput = [IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputPath))
    [IO.File]::WriteAllText($fullOutput, ($lines -join "`r`n") + "`r`n", (New-Object Text.UTF8Encoding($false)))
    Write-Output "Wrote $fullOutput ($($audit.Count) placeable blocks, $($supported.Count) enabled)."
}
