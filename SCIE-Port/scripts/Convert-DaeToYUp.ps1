[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Source,
    [Parameter(Mandatory = $true)]
    [string]$Destination,
    [switch]$LiftToGround
)

$ErrorActionPreference = 'Stop'
$culture = [Globalization.CultureInfo]::InvariantCulture
$resolvedSource = (Resolve-Path -LiteralPath $Source).Path

$document = New-Object Xml.XmlDocument
$document.PreserveWhitespace = $true
$document.Load($resolvedSource)

$upAxis = $document.SelectSingleNode("//*[local-name()='asset']/*[local-name()='up_axis']")
if ($null -eq $upAxis -or $upAxis.InnerText -ne 'Z_UP') {
    throw "Expected a Z_UP source model: $resolvedSource"
}

$positionArrays = @($document.SelectNodes("//*[local-name()='float_array'][contains(@id,'-positions-')]"))
$normalArrays = @($document.SelectNodes("//*[local-name()='float_array'][contains(@id,'-normals-')]"))
if ($positionArrays.Count -eq 0 -or $normalArrays.Count -eq 0) {
    throw 'The model must contain position and normal arrays.'
}

$minimumRotatedY = [double]::PositiveInfinity
foreach ($array in $positionArrays) {
    $values = @($array.InnerText -split '\s+' | Where-Object { $_ } | ForEach-Object { [double]::Parse($_, $culture) })
    if (($values.Count % 3) -ne 0) { throw "Invalid position tuple count in $($array.id)." }
    for ($i = 2; $i -lt $values.Count; $i += 3) {
        $minimumRotatedY = [Math]::Min($minimumRotatedY, $values[$i])
    }
}
$lift = if ($LiftToGround -and $minimumRotatedY -lt 0) { -$minimumRotatedY } else { 0.0 }

function Convert-Array([Xml.XmlElement]$array, [bool]$isPosition) {
    $values = @($array.InnerText -split '\s+' | Where-Object { $_ } | ForEach-Object { [double]::Parse($_, $culture) })
    if (($values.Count % 3) -ne 0) { throw "Invalid vector tuple count in $($array.id)." }
    $converted = New-Object 'Collections.Generic.List[string]'
    for ($i = 0; $i -lt $values.Count; $i += 3) {
        $x = $values[$i]
        $y = $values[$i + 1]
        $z = $values[$i + 2]
        $converted.Add($x.ToString('R', $culture))
        $converted.Add(($z + $(if ($isPosition) { $lift } else { 0.0 })).ToString('R', $culture))
        $converted.Add((-$y).ToString('R', $culture))
    }
    $array.InnerText = [string]::Join(' ', $converted)
}

foreach ($array in $positionArrays) { Convert-Array $array $true }
foreach ($array in $normalArrays) { Convert-Array $array $false }
$upAxis.InnerText = 'Y_UP'

$destinationDirectory = Split-Path -Parent $Destination
New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
$settings = New-Object Xml.XmlWriterSettings
$settings.Encoding = New-Object Text.UTF8Encoding($false)
$settings.Indent = $false
$settings.NewLineChars = "`n"
$settings.NewLineHandling = [Xml.NewLineHandling]::Replace
$writer = [Xml.XmlWriter]::Create($Destination, $settings)
try { $document.Save($writer) } finally { $writer.Dispose() }

Write-Host "Converted Z_UP -> Y_UP: $resolvedSource"
Write-Host "Destination: $Destination"
Write-Host "Lift: $lift"
