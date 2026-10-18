[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repoRoot 'tests\BuilderAssistant.Tests\BuilderAssistant.Tests.csproj'

if (-not (Test-Path -LiteralPath $testProject -PathType Leaf)) {
    throw "Logic test project not found: $testProject"
}

Write-Host 'Running offline build-engine logic tests (Core + Generators, no game assemblies)...'
$dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
$dotnetSdkRoot = Join-Path (Split-Path -Parent $dotnetPath) 'sdk'
$sdkMsBuild = Get-ChildItem -LiteralPath $dotnetSdkRoot -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' -and (Test-Path -LiteralPath (Join-Path $_.FullName 'MSBuild.dll')) } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Get-Item -LiteralPath (Join-Path $_.FullName 'MSBuild.dll') } |
    Select-Object -First 1
if ($null -eq $sdkMsBuild) {
    throw "No installed .NET SDK MSBuild.dll found under $dotnetSdkRoot"
}

& $dotnetPath $sdkMsBuild.FullName $testProject /restore /t:Build /p:Configuration=Release /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Logic test build failed with exit code $LASTEXITCODE"
}
$testDll = Join-Path (Split-Path -Parent $testProject) 'bin\Release\net8.0\BuilderAssistant.Tests.dll'
& $dotnetPath $testDll
if ($LASTEXITCODE -ne 0) {
    throw "Logic tests failed with exit code $LASTEXITCODE"
}
Write-Host 'Logic tests passed.'
