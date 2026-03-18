# Generate C# contract services from Forge compiled output
# Usage: .\scripts\generate-csharp.ps1 [-Build]
#   -Build  Build contracts with Forge before generating

param(
    [Alias("b")]
    [switch]$Build
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$ConfigFile = Join-Path $ProjectRoot "contracts\.nethereum-gen.multisettings"

if ($Build) {
    Write-Host "Building contracts with Forge..." -ForegroundColor Yellow
    Push-Location (Join-Path $ProjectRoot "contracts")
    & forge build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Forge build failed" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Pop-Location
    Write-Host ""
}

if (-not (Test-Path $ConfigFile)) {
    Write-Host "ERROR: Config file not found: $ConfigFile" -ForegroundColor Red
    Write-Host "Make sure you are running this from the project root."
    exit 1
}

Write-Host "=== Generating C# Contract Services ===" -ForegroundColor Cyan
Write-Host "Config: $ConfigFile"
Write-Host ""

$GeneratorProject = Join-Path $ProjectRoot "generators\Nethereum.Generator.Console\Nethereum.Generator.Console.csproj"
$ContractsRoot = Join-Path $ProjectRoot "contracts"

& dotnet run --project $GeneratorProject -- generate from-config -cfg $ConfigFile -r $ContractsRoot

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Code generation failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Generation Complete ===" -ForegroundColor Green
Write-Host "Generated files are in ContractServices/"
