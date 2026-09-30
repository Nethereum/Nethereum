# Generate C# contract services from Forge compiled output using multisettings
# Usage: .\scripts\generate-csharp-from-forge.ps1 [-Config <path>] [-Root <path>] [-Build]

param(
    [Alias("c")]
    [string]$Config,

    [Alias("r")]
    [string]$Root,

    [Alias("b")]
    [switch]$Build,

    [Alias("h")]
    [switch]$Help
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir

if (-not $Config) {
    $Config = Join-Path $RepoRoot "contracts\.nethereum-gen.multisettings"
}

if (-not $Root) {
    # The config's paths ("out/*.json") and basePath ("../src/*") are relative to the contracts
    # directory, so the generator root defaults there - not the repo root.
    $Root = Join-Path $RepoRoot "contracts"
}

$GeneratorProject = Join-Path $RepoRoot "generators\Nethereum.Generator.Console\Nethereum.Generator.Console.csproj"

function Show-Usage {
    Write-Host "Usage: .\generate-csharp-from-forge.ps1 [-Config <path>] [-Root <path>] [-Build]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Config, -c   Path to .nethereum-gen.multisettings file"
    Write-Host "                Default: contracts\.nethereum-gen.multisettings"
    Write-Host "  -Root, -r     Root path for relative paths in config"
    Write-Host "                Default: contracts directory (config paths are contracts-relative)"
    Write-Host "  -Build, -b    Build contracts with Forge before generating"
    Write-Host "  -Help, -h     Show this help"
    Write-Host ""
    Write-Host "Example:"
    Write-Host "  .\generate-csharp-from-forge.ps1              # Generate using default config"
    Write-Host "  .\generate-csharp-from-forge.ps1 -Build       # Build then generate"
    Write-Host "  .\generate-csharp-from-forge.ps1 -c my.json   # Use custom config"
    exit 0
}

if ($Help) {
    Show-Usage
}

Write-Host "=== Nethereum C# Generator from Forge Output ===" -ForegroundColor Cyan
Write-Host "Config file: $Config"
Write-Host "Root path: $Root"
Write-Host ""

if (-not (Test-Path $Config)) {
    Write-Host "ERROR: Config file not found: $Config" -ForegroundColor Red
    exit 1
}

if ($Build) {
    Write-Host "Building contracts with Forge..." -ForegroundColor Yellow
    $ContractsDir = Join-Path $RepoRoot "contracts"
    & forge build --root $ContractsDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Forge build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

Write-Host "Generating C# services..." -ForegroundColor Yellow
& dotnet run --project $GeneratorProject --framework net8.0 -- generate from-config -cfg $Config -r $Root

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Code generation failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Generation Complete ===" -ForegroundColor Green
