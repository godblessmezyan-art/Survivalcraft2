[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$DatabaseDir
)

if (-not $ProjectRoot) { $ProjectRoot = Split-Path -Parent $PSScriptRoot }
if (-not $DatabaseDir) { $DatabaseDir = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Database' }

# SCIE-Port database static validator.
#
# WHY THIS EXISTS
#   Survivalcraft merges every mod *.xdb into the shipped vanilla <DatabaseObjects>
#   tree using ModsManager.CombineDataBase. That routine matches elements by
#   LocalName AND Guid. If a mod declares a folder such as <Folder Name="Entities">
#   with a GUID that does NOT equal the game's real vanilla Entities folder GUID,
#   the merge does NOT fold the content into the vanilla folder; it appends a
#   second, rogue same-name <Folder> node instead (empirically reproduced against
#   the Net_x25.01.17 assembly). That pollutes <DatabaseObjects>, files the entity
#   under a non-standard container, and diverges from the structure used by the
#   proven-working reference mod (Builder Assistant Speedboat.xdb). This script
#   turns that failure into a build-time error.
#
# CHECKS (all hard failures, non-zero exit; nothing is swallowed)
#   1. Root may be <DatabaseObjects> or a custom mod container, but a custom root
#      must not contain a nested <DatabaseObjects> child.
#   2. Any folder whose Name matches a known vanilla folder must reuse the real
#      vanilla Guid (no same-name/different-GUID rogue folder).
#   3. No two <Folder> nodes inside one file may share a Name with different Guids.
#   4. Every InheritanceParent must resolve: either to a Guid defined in the SAME
#      file, or to a curated vanilla parent Guid resolved by the shipped database.
#      An unresolvable/unknown parent is a fatal error (it is what would throw
#      "Database object ... not found" during load).

$ErrorActionPreference = 'Stop'
$failures = New-Object System.Collections.Generic.List[string]

# Authoritative vanilla folder identities (Name -> real Guid). Cross-checked against
# the working Builder Assistant Speedboat.xdb and the Net_x25.01.17 assembly.
$VanillaFolders = @{
    'Entities'       = '5fcdf35f-7001-442c-883e-e0502b15d291'
    'Gameplay'       = 'd3d4b692-acc9-4128-9b99-a5acf1de1fbb'
    'BlockBehaviors' = '00c97f0f-731e-481c-9909-eae9cc5ee940'
}

# Vanilla parent templates referenced through InheritanceParent. These live in the
# shipped game database (Content.scpak) and are NOT reproduced by a mod; listing
# them here means any NEW or unknown parent is flagged instead of silently failing
# at in-game load time. Friendly names are for diagnostics only.
$KnownVanillaParents = @{
    '472b36f3-6ee6-442a-900f-b4caba94be39' = 'Boat (ComponentTemplate)'
    'cc5a4e72-0df9-4acf-a42e-ecb5a2a7baf4' = 'SimpleModel (ComponentTemplate)'
    '65268743-4076-4ee3-aefe-f02a54d16b36' = 'Spawn (ComponentTemplate)'
    '93590d78-63b9-4cd6-bc41-e66864cbb050' = 'OnFire (ComponentTemplate)'
    'd0b14127-39ce-4490-af00-fb4111e9c51e' = 'Mount (ComponentTemplate)'
    '8edd1986-0dcc-424f-893a-491c089b821e' = 'Body (ComponentTemplate)'
    '17bb7b53-159c-4eb3-b4bc-137b10bba88a' = 'Damage (ComponentTemplate)'
    'b05700ed-7e4e-4679-98f5-b597f421496b' = 'Drive base (ComponentTemplate)'
    'fefb9590-4972-4893-b02a-76063611b745' = 'Boat block behavior base (SubsystemTemplate)'
    '85023bf8-1c90-4dd1-9442-e6c13691d078' = 'Project (ProjectTemplate)'
    '49a9845c-3810-4092-ba53-df213d10d563' = 'Wolf (EntityTemplate)'
    '8e62c6e4-a544-4c09-a3fb-59fd37d5b998' = 'Alpaca (EntityTemplate)'
}

if (-not (Test-Path -LiteralPath $DatabaseDir -PathType Container)) {
    throw "Database directory not found: $DatabaseDir"
}

$xdbFiles = @(Get-ChildItem -LiteralPath $DatabaseDir -Recurse -Filter *.xdb -File)
if ($xdbFiles.Count -eq 0) {
    throw "No .xdb files found under: $DatabaseDir"
}

Write-Host "SCIE-Port database validator"
Write-Host "Scanning: $DatabaseDir"
Write-Host ("Files: " + (($xdbFiles | ForEach-Object { $_.Name }) -join ', '))
Write-Host ""

foreach ($file in $xdbFiles) {
    Write-Host "== $($file.Name) =="

    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.Load($file.FullName)

    $root = $xml.DocumentElement

    # 1. Root container contract.
    $nestedDatabaseObjects = @($root.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and $_.Name -eq 'DatabaseObjects' })
    if ($root.Name -ne 'DatabaseObjects' -and $nestedDatabaseObjects.Count -gt 0) {
        $failures.Add("$($file.Name): would create a nested <DatabaseObjects> node.")
    }

    # Gather folders.
    $folders = @($xml.GetElementsByTagName('Folder'))

    # 2. Known vanilla folder must reuse the real Guid.
    foreach ($folder in $folders) {
        $name = $folder.GetAttribute('Name')
        $guid = $folder.GetAttribute('Guid')
        if ($VanillaFolders.ContainsKey($name)) {
            $expected = $VanillaFolders[$name]
            if (-not [string]::Equals($guid, $expected, [StringComparison]::OrdinalIgnoreCase)) {
                $failures.Add("$($file.Name): <Folder Name='$name'> Guid=$guid does not match the vanilla folder Guid=$expected. ModsManager.CombineDataBase matches by Name+Guid, so this creates a rogue duplicate folder instead of merging.")
            } else {
                Write-Host "  OK  folder '$name' uses vanilla Guid $guid"
            }
        }
    }

    # 3. Same-name / different-GUID folder collision inside one file.
    $byName = @{}
    foreach ($folder in $folders) {
        $name = $folder.GetAttribute('Name')
        $guid = $folder.GetAttribute('Guid')
        if ($byName.ContainsKey($name)) {
            if (-not [string]::Equals($byName[$name], $guid, [StringComparison]::OrdinalIgnoreCase)) {
                $failures.Add("$($file.Name): duplicate <Folder Name='$name'> with different Guids ($($byName[$name]) vs $guid).")
            }
        } else {
            $byName[$name] = $guid
        }
    }

    # 4. InheritanceParent reference completeness.
    $definedGuids = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
    foreach ($n in $xml.GetElementsByTagName('*')) {
        if ($n.NodeType -ne 'Element') { continue }
        $g = $n.GetAttribute('Guid')
        if ($g) { [void]$definedGuids.Add($g) }
    }

    $parentNodes = @($xml.GetElementsByTagName('*') | Where-Object { $_.NodeType -eq 'Element' -and $_.HasAttribute('InheritanceParent') })
    foreach ($node in $parentNodes) {
        $parent = $node.GetAttribute('InheritanceParent')
        $label = "$($node.Name) '$($node.GetAttribute('Name'))'"
        if ([string]::IsNullOrWhiteSpace($parent)) {
            $failures.Add("$($file.Name): $label has an empty InheritanceParent attribute.")
            continue
        }
        if ($definedGuids.Contains($parent)) {
            Write-Host "  OK  $label -> internal $parent"
            continue
        }
        if ($KnownVanillaParents.ContainsKey($parent)) {
            Write-Host "  OK  $label -> vanilla '$($KnownVanillaParents[$parent])' $parent (resolved by shipped DB at load)"
            continue
        }
        $failures.Add("$($file.Name): $label InheritanceParent=$parent is NOT defined in this file and is NOT a known vanilla parent. This would throw at database load.")
    }

    Write-Host ""
}

if ($failures.Count -gt 0) {
    Write-Host "DATABASE VALIDATION FAILED ($($failures.Count) problem(s)):" -ForegroundColor Red
    foreach ($f in $failures) { Write-Host "  - $f" }
    exit 1
}

Write-Host "DATABASE VALIDATION PASSED" -ForegroundColor Green
