function Resolve-SurvivalcraftGameDirectory {
    [CmdletBinding()]
    param([string]$RequestedPath)

    $candidates = @()
    if ($RequestedPath) { $candidates += $RequestedPath }
    if ($env:SCNET_GAME_DIR) { $candidates += $env:SCNET_GAME_DIR }
    if ($env:SCNetGameDir) { $candidates += $env:SCNetGameDir }
    $candidates += @(
        'D:\SCNET',
        'D:\SC',
        'E:\SCNET\SCNETx25.01.17'
    )

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (-not $candidate) { continue }
        $resolved = [IO.Path]::GetFullPath($candidate)
        $required = @('Survivalcraft.exe', 'Engine.dll', 'EntitySystem.dll')
        if (($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $resolved $_) -PathType Leaf) }).Count -eq 0) {
            return $resolved.TrimEnd('\')
        }
    }
    throw 'SCNET x25.01.17 game directory not found. Pass -GameDir, set SCNET_GAME_DIR/SCNetGameDir, or provide D:\SCNET, D:\SC, or E:\SCNET\SCNETx25.01.17.'
}
