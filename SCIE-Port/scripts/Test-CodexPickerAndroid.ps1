#requires -Version 7.0
param([Parameter(Mandatory=$true)][string]$AndroidDir,[Parameter(Mandatory=$true)][string]$CecilPath)
$ErrorActionPreference='Stop'
Add-Type -Path $CecilPath
$engine=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $AndroidDir 'Engine.dll'))
$mono=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $AndroidDir 'Mono.Android.dll'))
try {
    $window=$engine.MainModule.GetType('Engine.Window')
    $activity=@($window.Properties|Where-Object Name -CEQ 'Activity')
    if($activity.Count -ne 1 -or -not $activity[0].GetMethod.IsStatic){throw 'Missing target Window.Activity'}
    $api=$mono.MainModule.GetType('Android.Runtime.JNIEnv');$checks=1
    $spec=@{
      FindClass=@('System.String');NewString=@('System.String');NewGlobalRef=@('System.IntPtr');DeleteGlobalRef=@('System.IntPtr');DeleteLocalRef=@('System.IntPtr');GetObjectClass=@('System.IntPtr');
      GetMethodID=@('System.IntPtr','System.String','System.String');GetStaticMethodID=@('System.IntPtr','System.String','System.String');
      GetString=@('System.IntPtr','Android.Runtime.JniHandleOwnership')
    }
    foreach($name in @('NewObject','CallObjectMethod','CallBooleanMethod','CallStaticVoidMethod','CallStaticIntMethod','CallStaticObjectMethod')){$spec[$name]=@('System.IntPtr','System.IntPtr','Android.Runtime.JValue[]')}
    foreach($name in $spec.Keys){
        $signature=$spec[$name]-join ','
        $method=@($api.Methods|Where-Object {$_.IsPublic -and $_.IsStatic -and $_.Name -ceq $name -and (($_.Parameters|ForEach-Object {$_.ParameterType.FullName})-join ',') -ceq $signature})
        if($method.Count -ne 1){throw "Trimmed Android member missing: JNIEnv.$name($signature)"};$checks++
    }
    $jvalue=$mono.MainModule.GetType('Android.Runtime.JValue')
    if(-not @($jvalue.Methods|Where-Object {$_.IsConstructor -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -ceq 'System.IntPtr'}).Count){throw 'JValue(IntPtr) missing'}
    $find=@($api.Methods|Where-Object {$_.Name -ceq 'FindClass' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -ceq 'System.String'})[0]
    $global=@($find.Body.Instructions|Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -ceq 'Android.Runtime.JNIEnv' -and $_.Operand.Name -ceq 'NewGlobalRef'})
    if($global.Count -ne 1){throw 'Unknown FindClass ownership: re-audit target IL before releasing class handles.'}
    Write-Host "Android picker exact retained JNI/Activity metadata: $($checks+1) signatures passed; device execution pending."
    Write-Host 'Target FindClass(string) IL creates a GLOBAL reference: DeleteGlobalRef required; native JNI FindClass rules cannot be assumed.'
    foreach($name in @('Engine.dll','Survivalcraft.dll','Mono.Android.dll')){Write-Host "$name SHA256=$((Get-FileHash -LiteralPath (Join-Path $AndroidDir $name)).Hash)"}
} finally {$engine.Dispose();$mono.Dispose()}
