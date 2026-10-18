# Shared Builder Assistant XDB database-merge safety validator.
#
# WHY THIS EXISTS
#   Survivalcraft x25.01.17 merges every mod *.xdb into the shipped vanilla
#   <DatabaseObjects> tree using ModsManager.CombineDataBase, which matches
#   folders by Name + Guid. Three mistakes crash the in-game database load:
#     (A) A vanilla-named folder (Entities/Gameplay/BlockBehaviors) or the
#         vanilla Project template carrying a GUID that is NOT the real vanilla
#         value -> the merge appends a rogue same-name folder and the deserializer
#         throws 'Another database object with name "X" is already nested in
#         database object "Root".'  (This is exactly the historical SCIE-Port 0.3.1
#         failure; Builder Assistant must never regress into it.)
#     (B) A folder NAME declared under two DIFFERENT GUIDs across the package ->
#         same rogue-duplicate condition.
#     (C) An InheritanceParent that is neither defined inside the package nor a
#         known vanilla base -> throws 'database object not found' at load.
#   Empirical note: a NON-vanilla (custom) template/folder GUID is intentionally
#   re-declared in more than one *.xdb only when it equals a shared vanilla
#   folder/Project GUID (children fold into the SAME vanilla container). That
#   repeat is legitimate and must NOT be flagged; any OTHER GUID defined twice is
#   a genuine collision.
#
# The vanilla GUID values below were re-verified byte-for-byte on 2026-10-18
# against the real Assets/Database.xml extracted from a Net_x25.01.17
# Content.scpak via the game's own SurvivalCraftModEntity.GetDecipherStream.

function Get-XdbVanillaFolderGuids {
    return @{
        'Entities'       = '5fcdf35f-7001-442c-883e-e0502b15d291'
        'Gameplay'       = 'd3d4b692-acc9-4128-9b99-a5acf1de1fbb'
        'BlockBehaviors' = '00c97f0f-731e-481c-9909-eae9cc5ee940'
    }
}

function Get-XdbVanillaProjectGuid { '85023bf8-1c90-4dd1-9442-e6c13691d078' }

function Get-XdbKnownVanillaParents {
    $set = @{}
    @(
        '472b36f3-6ee6-442a-900f-b4caba94be39', # Boat (ComponentTemplate)
        'cc5a4e72-0df9-4acf-a42e-ecb5a2a7baf4', # SimpleModel (ComponentTemplate)
        '65268743-4076-4ee3-aefe-f02a54d16b36', # Spawn (ComponentTemplate)
        '93590d78-63b9-4cd6-bc41-e66864cbb050', # OnFire (ComponentTemplate)
        'd0b14127-39ce-4490-af00-fb4111e9c51e', # Mount (ComponentTemplate)
        '8edd1986-0dcc-424f-893a-491c089b821e', # Body (ComponentTemplate)
        '17bb7b53-159c-4eb3-b4bc-137b10bba88a', # Damage (ComponentTemplate)
        'b05700ed-7e4e-4679-98f5-b597f421496b', # Drive base (ComponentTemplate)
        'fefb9590-4972-4893-b02a-76063611b745', # BlockBehavior base (SubsystemTemplate)
        (Get-XdbVanillaProjectGuid)             # Project (ProjectTemplate)
    ) | ForEach-Object { $set[$_.ToLowerInvariant()] = $true }
    return $set
}

# Test-XdbMergeSafety
#   $XdbTexts : ordered hashtable { 'Assets/Database/Foo.xdb' = '<xml>' , ... }
#   Returns an array of human-readable failure strings. Empty array == safe.
function Test-XdbMergeSafety {
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$XdbTexts
    )

    $vanillaFolderGuids = Get-XdbVanillaFolderGuids
    $vanillaProjectGuid = Get-XdbVanillaProjectGuid
    $knownVanillaParents = Get-XdbKnownVanillaParents
    $failures = New-Object System.Collections.Generic.List[string]

    $folderRx = '<Folder\s+Name="([^"]+)"\s+Guid="([^"]+)"'
    $projectRx = '<ProjectTemplate\s+Name="Project"\s+Guid="([^"]+)"'
    $structuralRx = '<(?!Parameter\b)([A-Za-z]+Template|Folder)\s+[^>]*?Guid="([^"]+)"'
    $anyGuidRx = 'Guid="([^"]+)"'
    $parentRx = '<[A-Za-z]+\s+Name="([^"]+)"[^>]*InheritanceParent="([^"]+)"'

    # (A) vanilla folder / Project must carry the exact vanilla GUID.
    foreach ($name in $XdbTexts.Keys) {
        $xdb = [string]$XdbTexts[$name]
        foreach ($m in [regex]::Matches($xdb, $folderRx)) {
            $fn = $m.Groups[1].Value
            $fg = $m.Groups[2].Value
            if ($vanillaFolderGuids.ContainsKey($fn) -and
                $fg -ne $vanillaFolderGuids[$fn]) {
                $failures.Add("$name``: folder '$fn' uses non-vanilla GUID $fg (expected $($vanillaFolderGuids[$fn])). Re-triggers the x25.01.17 'already nested' merge error.")
            }
        }
        foreach ($m in [regex]::Matches($xdb, $projectRx)) {
            if ($m.Groups[1].Value -ne $vanillaProjectGuid) {
                $failures.Add("$name``: ProjectTemplate uses non-vanilla GUID $($m.Groups[1].Value) (expected $vanillaProjectGuid).")
            }
        }
    }

    # (B) a folder NAME must not map to two different GUIDs across the package.
    $guidsByFolderName = @{}
    foreach ($name in $XdbTexts.Keys) {
        foreach ($m in [regex]::Matches([string]$XdbTexts[$name], $folderRx)) {
            $fn = $m.Groups[1].Value
            $fg = $m.Groups[2].Value
            if (-not $guidsByFolderName.ContainsKey($fn)) { $guidsByFolderName[$fn] = @{} }
            $guidsByFolderName[$fn][$fg.ToLowerInvariant()] = $true
        }
    }
    foreach ($fn in $guidsByFolderName.Keys) {
        if ($guidsByFolderName[$fn].Count -gt 1) {
            $failures.Add("Cross-file folder collision: name '$fn' declared with multiple GUIDs ($($guidsByFolderName[$fn].Keys -join ', ')). The merge appends a rogue duplicate folder and throws 'already nested'.")
        }
    }

    # (C)+(collision) GLOBAL Guid uniqueness + InheritanceParent references.
    # The x25.01.17 loader keys EVERY Guid= value (including <Parameter>) into a
    # dictionary; reusing any GUID throws 'An item with the same key has already
    # been added'. The ONLY legitimate repeats are the shared vanilla CONTAINER
    # GUIDs (Entities/Gameplay/BlockBehaviors folders + Project), which each file
    # re-declares once so their children fold into the SAME vanilla folder.
    $sharedVanillaGuids = @{}
    @($vanillaFolderGuids.Values) + @($vanillaProjectGuid) | ForEach-Object { $sharedVanillaGuids[$_.ToLowerInvariant()] = $true }
    $guidOwners = @{}
    $definedGuids = [System.Collections.Generic.HashSet[string]]::new()
    $parentRefs = @()
    foreach ($name in $XdbTexts.Keys) {
        $xdb = [string]$XdbTexts[$name]
        foreach ($m in [regex]::Matches($xdb, $anyGuidRx)) {
            $gl = $m.Groups[1].Value.ToLowerInvariant()
            [void]$definedGuids.Add($gl)
            if ($sharedVanillaGuids.ContainsKey($gl)) { continue }
            if ($guidOwners.ContainsKey($gl)) {
                $failures.Add("Duplicate GUID $($m.Groups[1].Value): first in $($guidOwners[$gl]), again in $name. The x25.01.17 database loader keys every Guid= (including <Parameter>) and throws 'An item with the same key has already been added'.")
            }
            else { $guidOwners[$gl] = $name }
        }
        foreach ($m in [regex]::Matches($xdb, $parentRx)) {
            $parentRefs += [pscustomobject]@{ File = $name; Node = $m.Groups[1].Value; Parent = $m.Groups[2].Value }
        }
    }
    foreach ($ref in $parentRefs) {
        $pl = $ref.Parent.ToLowerInvariant()
        if ($definedGuids.Contains($pl)) { continue }
        if ($knownVanillaParents.ContainsKey($pl)) { continue }
        $failures.Add("$($ref.File): node '$($ref.Node)' InheritanceParent=$($ref.Parent) is neither defined in the package nor a known vanilla base. Would throw 'database object not found' at load.")
    }

    return ,$failures.ToArray()
}
