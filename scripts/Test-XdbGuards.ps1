# Regression test for the shared Builder Assistant XDB merge-safety validator.
# Run:  powershell -ExecutionPolicy Bypass -File scripts/Test-XdbGuards.ps1
# Exits non-zero on any assertion failure.
#
# It feeds crafted fixtures through the SAME Test-XdbMergeSafety function the
# production Build-NetMod.ps1 uses, so a passing run proves the guard actually
# bites on the three failure modes (and does NOT false-positive on legitimate
# cross-file repeats). It also validates the real shipped .xdb files.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'XdbMergeSafety.ps1')

$script:pass = 0
$script:fail = 0
function Assert-Cond([bool]$cond, [string]$label) {
    if ($cond) { $script:pass++; Write-Host "  PASS  $label" -ForegroundColor Green }
    else       { $script:fail++; Write-Host "  FAIL  $label" -ForegroundColor Red }
}

$realEntities = '5fcdf35f-7001-442c-883e-e0502b15d291'
$realBlocks   = '00c97f0f-731e-481c-9909-eae9cc5ee940'
$realProject  = '85023bf8-1c90-4dd1-9442-e6c13691d078'
$boatBase     = '472b36f3-6ee6-442a-900f-b4caba94be39'
$bbBase       = 'fefb9590-4972-4893-b02a-76063611b745'

Write-Host "[1] real shipped files must be clean"
$dir = Join-Path $PSScriptRoot '..\src\BuilderAssistant\Assets\Database'
$real = [ordered]@{}
Get-ChildItem -LiteralPath $dir -Filter *.xdb | ForEach-Object {
    $real["Assets/Database/$($_.Name)"] = [System.IO.File]::ReadAllText($_.FullName)
}
$res = Test-XdbMergeSafety -XdbTexts $real
Assert-Cond ($res.Count -eq 0) ("both shipped .xdb files pass (got $($res.Count) failure(s))")
if ($res.Count -gt 0) { $res | ForEach-Object { Write-Host "        - $_" } }

Write-Host "[2] rogue vanilla-named folder (SCIE-Port 0.3.1 failure) must be caught"
$bad = [ordered]@{
    'Assets/Database/Rogue.xdb' = ('<DatabaseObjects><Folder Name="Entities" Guid="11111111-1111-1111-1111-111111111111">' +
        '<EntityTemplate Name="Thing" Guid="22222222-2222-2222-2222-222222222222" InheritanceParent="' + $boatBase + '"/></Folder></DatabaseObjects>')
}
$res = Test-XdbMergeSafety -XdbTexts $bad
Assert-Cond ($res.Count -ge 1 -and (($res -join ' ') -match 'non-vanilla GUID')) ("rogue Entities folder flagged (got $($res.Count))")

Write-Host "[3] same folder name mapped to two GUIDs across files must be caught"
$bad2 = [ordered]@{
    'A.xdb' = ('<DatabaseObjects><Folder Name="BlockBehaviors" Guid="' + $realBlocks + '"/></DatabaseObjects>')
    'B.xdb' = ('<DatabaseObjects><Folder Name="BlockBehaviors" Guid="33333333-3333-3333-3333-333333333333"/></DatabaseObjects>')
}
$res = Test-XdbMergeSafety -XdbTexts $bad2
Assert-Cond (($res -join ' ') -match 'folder collision') ("cross-file folder-name collision flagged (got $($res.Count))")

Write-Host "[4] unresolvable InheritanceParent must be caught"
$bad3 = [ordered]@{
    'C.xdb' = ('<DatabaseObjects><Folder Name="Entities" Guid="' + $realEntities + '">' +
        '<EntityTemplate Name="Thing" Guid="44444444-4444-4444-4444-444444444444" InheritanceParent="deadbeef-dead-dead-dead-deaddeadbeef"/></Folder></DatabaseObjects>')
}
$res = Test-XdbMergeSafety -XdbTexts $bad3
Assert-Cond (($res -join ' ') -match 'neither defined') ("unknown parent flagged (got $($res.Count))")

Write-Host "[5] duplicate custom structural GUID across files must be caught"
$bad4 = [ordered]@{
    'D.xdb' = ('<DatabaseObjects><Folder Name="Entities" Guid="' + $realEntities + '">' +
        '<EntityTemplate Name="One" Guid="55555555-5555-5555-5555-555555555555" InheritanceParent="' + $boatBase + '"/></Folder></DatabaseObjects>')
    'E.xdb' = ('<DatabaseObjects><Folder Name="Entities" Guid="' + $realEntities + '">' +
        '<EntityTemplate Name="Two" Guid="55555555-5555-5555-5555-555555555555" InheritanceParent="' + $boatBase + '"/></Folder></DatabaseObjects>')
}
$res = Test-XdbMergeSafety -XdbTexts $bad4
Assert-Cond (($res -join ' ') -match 'Duplicate GUID') ("duplicate custom GUID flagged (got $($res.Count))")

Write-Host "[5b] duplicate <Parameter> GUID across files (the ea25b78e shipped bug) must be caught"
$bad5b = [ordered]@{
    'F.xdb' = ('<DatabaseObjects><Folder Name="BlockBehaviors" Guid="' + $realBlocks + '">' +
        '<SubsystemTemplate Name="BA" Guid="aaaaaaaa-1111-1111-1111-111111111111" InheritanceParent="' + $bbBase + '">' +
        '<Parameter Name="Class" Guid="ea25b78e-7d8c-4998-85d3-2b1baad3ba34" Value="X.SubA" Type="string"/></SubsystemTemplate></Folder></DatabaseObjects>')
    'G.xdb' = ('<DatabaseObjects><Folder Name="BlockBehaviors" Guid="' + $realBlocks + '">' +
        '<SubsystemTemplate Name="BB" Guid="bbbbbbbb-2222-2222-2222-222222222222" InheritanceParent="' + $bbBase + '">' +
        '<Parameter Name="Class" Guid="ea25b78e-7d8c-4998-85d3-2b1baad3ba34" Value="X.SubB" Type="string"/></SubsystemTemplate></Folder></DatabaseObjects>')
}
$res = Test-XdbMergeSafety -XdbTexts $bad5b
Assert-Cond (($res -join ' ') -match 'Duplicate GUID') ("duplicate <Parameter> GUID flagged (got $($res.Count))")

Write-Host "[6] legitimate shared-vanilla folder + Project repeat across files must NOT be flagged"
$good = [ordered]@{
    'P.xdb' = ('<DatabaseObjects><Folder Name="BlockBehaviors" Guid="' + $realBlocks + '">' +
        '<SubsystemTemplate Name="One" Guid="66666666-6666-6666-6666-666666666666" InheritanceParent="' + $bbBase + '"/></Folder>' +
        '<ProjectTemplate Name="Project" Guid="' + $realProject + '"><MemberSubsystemTemplate Name="M1" Guid="77777777-7777-7777-7777-777777777777" InheritanceParent="66666666-6666-6666-6666-666666666666"/></ProjectTemplate></DatabaseObjects>')
    'Q.xdb' = ('<DatabaseObjects><Folder Name="BlockBehaviors" Guid="' + $realBlocks + '">' +
        '<SubsystemTemplate Name="Two" Guid="88888888-8888-8888-8888-888888888888" InheritanceParent="' + $bbBase + '"/></Folder>' +
        '<ProjectTemplate Name="Project" Guid="' + $realProject + '"><MemberSubsystemTemplate Name="M2" Guid="99999999-9999-9999-9999-999999999999" InheritanceParent="88888888-8888-8888-8888-888888888888"/></ProjectTemplate></DatabaseObjects>')
}
$res = Test-XdbMergeSafety -XdbTexts $good
Assert-Cond ($res.Count -eq 0) ("legitimate shared-vanilla repeat accepted (got $($res.Count) failure(s))")
if ($res.Count -gt 0) { $res | ForEach-Object { Write-Host "        - $_" } }

Write-Host ""
Write-Host ("CHECKS: {0} passed, {1} failed" -f $script:pass, $script:fail)
if ($script:fail -gt 0) { exit 1 }
Write-Host "XDB GUARD TESTS PASSED" -ForegroundColor Green
