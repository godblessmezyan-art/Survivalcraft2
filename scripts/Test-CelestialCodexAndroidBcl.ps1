#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AndroidBclDir,
    [Parameter(Mandatory)][string]$CecilPath,
    [string]$ModDll = (Join-Path $PSScriptRoot '../src/CelestialCodex/bin/Release/net472/CelestialCodex.dll'),
    [string]$NegativeDll
)
$ErrorActionPreference = 'Stop'
# Read-only metadata audit. Supply extracted Android assemblies locally; never commit them.
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
$bclRoot = (Resolve-Path -LiteralPath $AndroidBclDir).Path
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory($bclRoot)
$parameters = [Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver = $resolver
$snapshots = @{}
try {
    foreach ($name in @('mscorlib', 'System', 'System.Net.Http')) {
        $path = Join-Path $bclRoot ($name + '.dll')
        $snapshots[$name] = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path, $parameters)
        Write-Output "$name SHA256=$((Get-FileHash -LiteralPath $path).Hash) MVID=$($snapshots[$name].MainModule.Mvid)"
    }
    $http = $snapshots['System.Net.Http']
    $handler = $http.MainModule.GetType('System.Net.Http.HttpClientHandler')
    $sockets = $http.MainModule.GetType('System.Net.Http.SocketsHttpHandler')
    $settings = $http.MainModule.GetType('System.Net.Http.HttpConnectionSettings')
    if (-not $handler -or -not $sockets -or -not $settings) { throw 'Unknown Android HTTP backend; re-audit before use.' }
    foreach ($slot in @(
        @($handler, '_delegatingHandler', 'System.Net.Http.IMonoHttpClientHandler'),
        @($sockets, '_settings', 'System.Net.Http.HttpConnectionSettings'),
        @($settings, '_allowAutoRedirect', 'System.Boolean'),
        @($settings, '_useCookies', 'System.Boolean'),
        @($settings, '_automaticDecompression', 'System.Net.DecompressionMethods')
    )) {
        $field = @($slot[0].Fields | Where-Object { $_.Name -ceq $slot[1] -and $_.FieldType.FullName -ceq $slot[2] -and -not $_.IsStatic })
        if ($field.Count -ne 1) { throw "Android policy field mismatch: $($slot[1])" }
        if ($slot[0] -eq $settings -and $field[0].IsInitOnly) { throw 'Android policy slot is readonly.' }
    }
    foreach ($name in @('AllowAutoRedirect', 'UseCookies', 'AutomaticDecompression')) {
        $property = @($handler.Properties | Where-Object Name -CEQ $name)
        if ($property.Count -ne 1 -or -not $property[0].GetMethod) { throw "Missing policy readback: $name" }
    }
    $mediaType = $http.MainModule.GetType('System.Net.Http.Headers.MediaTypeHeaderValue')
    if (-not $mediaType -or -not ($mediaType.Methods | Where-Object { $_.Name -ceq 'ToString' -and $_.IsVirtual -and $_.Parameters.Count -eq 0 })) { throw 'Missing media type ToString override.' }
    function Find-MissingMembers([string]$Path) {
        $mod = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $Path).Path, $parameters)
        try {
            $count = 0
            $missing = [Collections.Generic.List[string]]::new()
            foreach ($member in $mod.MainModule.GetMemberReferences()) {
                if (-not $snapshots.ContainsKey($member.DeclaringType.Scope.Name)) { continue }
                $count++
                $resolved = $member.Resolve()
                if (-not $resolved) { $missing.Add($member.FullName); continue }
                # A desktop/GAC fallback must never make this Android audit pass.
                $expected = Join-Path $bclRoot ($resolved.Module.Assembly.Name.Name + '.dll')
                if (-not [string]::Equals($resolved.Module.FileName, $expected, [StringComparison]::OrdinalIgnoreCase)) { throw "Resolved outside supplied Android BCL: $member" }
            }
            return [pscustomobject]@{ Count = $count; Missing = $missing.ToArray(); Mvid = $mod.MainModule.Mvid }
        } finally { $mod.Dispose() }
    }
    $audit = Find-MissingMembers $ModDll
    if ($audit.Missing.Count -gt 0) { throw ("Android missing members:`n" + ($audit.Missing -join "`n")) }
    Write-Output "Android snapshot member audit: $($audit.Count) resolved, 0 missing; Mod MVID=$($audit.Mvid). Metadata only, no device execution."
    if ($NegativeDll) {
        $negative = Find-MissingMembers $NegativeDll
        if ($negative.Missing.Count -eq 0) { throw 'Negative DLL unexpectedly passed.' }
        Write-Output "Negative DLL rejected: $($negative.Missing.Count) missing members; MVID=$($negative.Mvid)"
        $negative.Missing | ForEach-Object { Write-Output "  $_" }
    }
} finally {
    foreach ($snapshot in $snapshots.Values) { $snapshot.Dispose() }
    $resolver.Dispose()
}
