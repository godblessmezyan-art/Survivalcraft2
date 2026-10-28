$ErrorActionPreference = 'Stop'
# Recover the gitignored, licensed upstream SCIE assets from the user's own
# previously packaged SCIEPort.netmod so the packaging pipeline can run in a
# workspace where upstream/SCIE was emptied. Staged copies of SteamBoat.png,
# Train.png and Cylinder.dae are byte-identical to upstream; Minecart.dae is
# the normalized SCNET form which the build passes through unchanged.
$projectRoot = Split-Path -Parent $PSScriptRoot
$package = Join-Path $projectRoot 'artifacts\荒野探险家.netmod'
if (-not (Test-Path -LiteralPath $package)) { $package = Join-Path $projectRoot 'artifacts\SCIEPort.netmod' } # Read-only historical recovery.
$upstream = Join-Path $projectRoot 'upstream\SCIE'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $map = @{
        'Assets/Textures/SteamBoat.png' = 'IndustrialMod\Textures\SteamBoat.png'
        'Assets/Textures/Train.png'     = 'IndustrialMod\Textures\Train.png'
        'Assets/Models/Minecart.dae'    = 'IndustrialMod\Models\Minecart.dae'
        'Assets/Models/Cylinder.dae'    = 'IndustrialMod\Models\Cylinder.dae'
    }
    foreach ($entryName in $map.Keys) {
        $entry = $archive.GetEntry($entryName)
        if ($null -eq $entry) { throw "Package is missing staged entry $entryName." }
        $destination = Join-Path $upstream $map[$entryName]
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
        $stream = $entry.Open()
        try {
            $file = [IO.File]::Create($destination)
            try { $stream.CopyTo($file) }
            finally { $file.Dispose() }
        }
        finally { $stream.Dispose() }
        Write-Output ("restored " + $map[$entryName] + " (" + $entry.Length + " bytes)")
    }
}
finally { $archive.Dispose() }
