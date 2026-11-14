#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$AndroidBclDir,
    [Parameter(Mandatory)][string]$CecilPath,
    [Parameter(Mandatory)][string]$ModDll,
    [string]$NegativeDll
)
$ErrorActionPreference='Stop'
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
$bcl=(Resolve-Path -LiteralPath $AndroidBclDir).Path
$resolver=[Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory($bcl)
$reader=[Mono.Cecil.ReaderParameters]::new()
$reader.AssemblyResolver=$resolver
$snapshots=@{}
try {
    foreach($name in @('mscorlib','System','System.Net.Http')){
        $path=Join-Path $bcl ($name+'.dll')
        $snapshots[$name]=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($path,$reader)
        Write-Output "$name SHA256=$((Get-FileHash -LiteralPath $path).Hash) MVID=$($snapshots[$name].MainModule.Mvid)"
    }
    function Read-Members([string]$path){
        $mod=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $path).Path,$reader)
        try {
            $types=[Collections.Generic.List[Mono.Cecil.TypeDefinition]]::new()
            function Visit($items){foreach($type in $items){
                if($type.FullName.StartsWith('SCIEPort.Features.Fishing.')){$types.Add($type)}
                Visit $type.NestedTypes
            }}
            Visit $mod.MainModule.Types
            if($types.Count -eq 0){throw 'Fishing implementation is absent'}
            $members=@{}
            foreach($type in $types){foreach($method in $type.Methods){
                if(-not $method.HasBody){continue}
                foreach($instruction in $method.Body.Instructions){
                    $m=$instruction.Operand
                    if(($m -is [Mono.Cecil.MethodReference] -or $m -is [Mono.Cecil.FieldReference]) -and $snapshots.ContainsKey($m.DeclaringType.Scope.Name)){$members[$m.FullName]=$m}
                }
            }}
            $missing=[Collections.Generic.List[string]]::new()
            foreach($m in $members.Values){
                $resolved=$m.Resolve()
                if(-not $resolved){$missing.Add($m.FullName);continue}
                $expected=Join-Path $bcl ($resolved.Module.Assembly.Name.Name+'.dll')
                if(-not [string]::Equals($expected,$resolved.Module.FileName,[StringComparison]::OrdinalIgnoreCase)){throw "Resolved outside Android snapshot: $m"}
            }
            return [pscustomobject]@{Missing=$missing.ToArray();Count=$members.Count;Mvid=$mod.MainModule.Mvid}
        } finally {$mod.Dispose()}
    }
    $result=Read-Members $ModDll
    if($result.Missing.Count){throw ("Missing Android members:`n"+($result.Missing -join "`n"))}
    Write-Output "Fishing Android snapshot: $($result.Count) members resolved, 0 missing; MVID=$($result.Mvid). Metadata only, no device execution."
    if($NegativeDll){
        $negative=Read-Members $NegativeDll
        if($negative.Missing -notcontains 'System.Double System.Random::NextDouble()'){throw 'Expected the old Random.NextDouble failure in negative DLL'}
        Write-Output 'Negative DLL correctly rejected: System.Random.NextDouble() is absent in this snapshot.'
    }
} finally {
    foreach($assembly in $snapshots.Values){$assembly.Dispose()}
    $resolver.Dispose()
}
