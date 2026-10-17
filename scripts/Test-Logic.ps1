[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repoRoot 'tests\BuilderAssistant.Tests\BuilderAssistant.Tests.csproj'

if (-not (Test-Path -LiteralPath $testProject -PathType Leaf)) {
    throw "Logic test project not found: $testProject"
}

Write-Host 'Running offline build-engine logic tests (Core + Generators, no game assemblies)...'
& dotnet run --project $testProject -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Logic tests failed with exit code $LASTEXITCODE"
}
Write-Host 'Logic tests passed.'
