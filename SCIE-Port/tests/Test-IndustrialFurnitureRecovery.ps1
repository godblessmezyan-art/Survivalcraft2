$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '..\src\SCIEPort\Assets\Blocks\IndustrialFurniture.csv'
$rows = @(Import-Csv -LiteralPath $path -Delimiter ';' -Encoding UTF8)
$expected = @('SteelGearFurnitureBlock', 'SteelWheelFurnitureBlock', 'GearMouldFurnitureBlock', 'WheelMouldFurnitureBlock', 'IndustrialPistonFurnitureBlock', 'IndustrialCylinderFurnitureBlock')
if ($rows.Count -ne $expected.Count) { throw 'Unexpected industrial furniture row count.' }
foreach ($name in $expected) {
    $row = @($rows | Where-Object { $_.'Class Name' -ceq $name })
    if ($row.Count -ne 1 -or $row[0].IsPlaceable -ne 'TRUE' -or $row[0].IsDiggingTransparent -ne 'FALSE') {
        throw "Furniture must remain placeable and targetable by the digging ray: $name"
    }
    $resilience = 0.0
    if (-not [double]::TryParse($row[0].DigResilience, [ref]$resilience) -or $resilience -le 0 -or [double]::IsInfinity($resilience)) {
        throw "Furniture must have finite positive digging resilience: $name"
    }
}
Write-Host 'Industrial furniture recovery configuration: 6 rows passed.'
