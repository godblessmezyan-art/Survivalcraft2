#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AndroidBclDir,
    [Parameter(Mandatory)][string]$CecilPath,
    [string]$ModDll = (Join-Path $PSScriptRoot '../artifacts/build/SCIEPort.dll'),
    [string]$NegativeDll
)
$ErrorActionPreference = 'Stop'
# Metadata only. Android assemblies remain local and are never packaged.
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
$bcl = (Resolve-Path -LiteralPath $AndroidBclDir).Path
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory($bcl)
$reader = [Mono.Cecil.ReaderParameters]::new()
$reader.AssemblyResolver = $resolver
$snapshots = @{}
try {
    foreach ($name in @('mscorlib', 'System', 'System.Net.Http')) {
        $path = Join-Path $bcl ($name + '.dll')
        $snapshots[$name] = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path, $reader)
        Write-Output "$name SHA256=$((Get-FileHash -LiteralPath $path).Hash) MVID=$($snapshots[$name].MainModule.Mvid)"
    }
    $module = $snapshots['System.Net.Http'].MainModule
    foreach ($slot in @(
        @('HttpClientHandler', '_delegatingHandler', 'System.Net.Http.IMonoHttpClientHandler'),
        @('SocketsHttpHandler', '_settings', 'System.Net.Http.HttpConnectionSettings'),
        @('HttpConnectionSettings', '_allowAutoRedirect', 'System.Boolean'),
        @('HttpConnectionSettings', '_useCookies', 'System.Boolean'),
        @('HttpConnectionSettings', '_automaticDecompression', 'System.Net.DecompressionMethods')
    )) {
        $type = $module.GetType('System.Net.Http.' + $slot[0])
        if (-not $type) { throw "Unknown HTTP layout: $($slot[0])" }
        $fields = @($type.Fields | Where-Object { $_.Name -ceq $slot[1] -and $_.FieldType.FullName -ceq $slot[2] -and -not $_.IsStatic })
        if ($fields.Count -ne 1 -or ($slot[0] -eq 'HttpConnectionSettings' -and $fields[0].IsInitOnly)) { throw "Unknown HTTP policy slot: $($slot[1])" }
    }
    $handler = $module.GetType('System.Net.Http.HttpClientHandler')
    foreach ($name in @('AllowAutoRedirect', 'UseCookies', 'AutomaticDecompression')) {
        $property = @($handler.Properties | Where-Object Name -CEQ $name)
        if ($property.Count -ne 1 -or -not $property[0].GetMethod) { throw "Missing HTTP policy getter: $name" }
    }
    $media = $module.GetType('System.Net.Http.Headers.MediaTypeHeaderValue')
    if (-not $media -or -not ($media.Methods | Where-Object { $_.Name -ceq 'ToString' -and $_.IsVirtual -and $_.Parameters.Count -eq 0 })) { throw 'Missing JSON media-type ToString override.' }
    function Test-Members([string]$Path) {
        $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $Path).Path, $reader)
        try {
            $types = [Collections.Generic.List[Mono.Cecil.TypeDefinition]]::new()
            function Visit($items) {
                foreach ($type in $items) {
                    if ($type.FullName -match '^SCIEPort\.Features\.Blueprints\.') { $types.Add($type) }
                    Visit $type.NestedTypes
                }
            }
            Visit $assembly.MainModule.Types
            if ($types.Count -eq 0) { throw 'Missing blueprint implementation types.' }
            $members = @{}
            foreach ($type in $types) { foreach ($method in $type.Methods) {
                if (-not $method.HasBody) { continue }
                foreach ($instruction in $method.Body.Instructions) {
                    $member = $instruction.Operand
                    if (($member -is [Mono.Cecil.MethodReference] -or $member -is [Mono.Cecil.FieldReference]) -and $snapshots.ContainsKey($member.DeclaringType.Scope.Name)) { $members[$member.FullName] = $member }
                }
            } }
            $missing = [Collections.Generic.List[string]]::new()
            foreach ($member in $members.Values) {
                $resolved = $member.Resolve()
                if (-not $resolved) { $missing.Add($member.FullName); continue }
                $expected = Join-Path $bcl ($resolved.Module.Assembly.Name.Name + '.dll')
                if (-not [string]::Equals($expected, $resolved.Module.FileName, [StringComparison]::OrdinalIgnoreCase)) { throw "Resolved outside Android snapshot: $member" }
            }
            return [pscustomobject]@{ Missing = $missing.ToArray(); Count = $members.Count; Mvid = $assembly.MainModule.Mvid }
        } finally { $assembly.Dispose() }
    }
    $result = Test-Members $ModDll
    if ($result.Missing.Count) { throw ("Missing Android members:`n" + ($result.Missing -join "`n")) }
    Write-Output "Tiangong Android snapshot: $($result.Count) members resolved; 0 missing; MVID=$($result.Mvid). Metadata only, no Android execution."
    if ($NegativeDll) {
        $negative = Test-Members $NegativeDll
        if ($negative.Missing.Count -eq 0) { throw "Expected missing members in negative DLL, got $($negative.Missing.Count)." }
        Write-Output "Negative DLL rejected; MVID=$($negative.Mvid)"
        $negative.Missing | ForEach-Object { Write-Output "  $_" }
    }
} finally {
    foreach ($assembly in $snapshots.Values) { $assembly.Dispose() }
    $resolver.Dispose()
}
