[CmdletBinding()]
param(
    [string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' })
)

$ErrorActionPreference = 'Stop'

$requiredFiles = @('Survivalcraft.exe', 'Engine.dll', 'EntitySystem.dll')
foreach ($file in $requiredFiles) {
    $path = Join-Path $GameDir $file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing required SCNET file: $path"
    }
}

$frameworkReference = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
if (-not (Test-Path -LiteralPath $frameworkReference -PathType Container)) {
    throw 'Missing .NET Framework 4.7.2 Targeting Pack (MSB3644 will occur).'
}

$survivalcraftPath = Join-Path $GameDir 'Survivalcraft.exe'
$assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($survivalcraftPath)
if ($assemblyName.Version.ToString() -ne '2.4.0.0') {
    throw "Unexpected Survivalcraft assembly version: $($assemblyName.Version). Expected 2.4.0.0."
}

$assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($survivalcraftPath)
$targetFramework = $assembly.GetCustomAttributesData() |
    Where-Object { $_.AttributeType.FullName -eq 'System.Runtime.Versioning.TargetFrameworkAttribute' } |
    ForEach-Object { $_.ConstructorArguments[0].Value }
if ($targetFramework -ne '.NETFramework,Version=v4.7.2') {
    throw "Unexpected TargetFramework: $targetFramework"
}

# Extract stable version markers without executing the game's static constructors.
$bytes = [System.IO.File]::ReadAllBytes($survivalcraftPath)
$unicodeText = [System.Text.Encoding]::Unicode.GetString($bytes) +
    [System.Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1)
foreach ($marker in @('Initilizing Mods Manager. Api Version: 1.42', '2.4.10.6', '.netmod')) {
    if (-not $unicodeText.Contains($marker)) {
        throw "Expected x25.01.17 marker was not found in Survivalcraft.exe: $marker"
    }
}

$knownHashes = @{
    'Survivalcraft.exe' = '3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD'
    'Engine.dll' = '98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694'
    'EntitySystem.dll' = '7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C'
}

$hashesMatch = $true
foreach ($file in $requiredFiles) {
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $GameDir $file)).Hash
    if ($actual -ne $knownHashes[$file]) {
        $hashesMatch = $false
        Write-Warning "$file differs from the checked public Windows x25.01.17 build. Actual SHA-256: $actual"
    }
}

Write-Host "SCNET directory: $GameDir"
Write-Host "Assembly version: $($assemblyName.Version)"
Write-Host "Target framework: $targetFramework"
Write-Host 'Mod API marker: 1.42'
Write-Host 'SC version marker: 2.4.10.6'
Write-Host "Known Windows hashes match: $hashesMatch"
Write-Host 'Environment validation passed.'
