$root = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) ".."
$blsDir = Join-Path $root "external\bls"
$outDir = Join-Path $root "src\Nethereum.Signer.Bls.Herumi\runtimes"

if (-not (Test-Path $blsDir)) {
    Write-Error "Herumi BLS source not found at $blsDir. Run 'git submodule update --init --recursive' first."
    exit 1
}

Push-Location $blsDir
git submodule update --init --recursive | Out-Null

$mklib = Join-Path $blsDir "mklib.bat"
& $mklib dll eth
Pop-Location

$winOut = Join-Path $outDir "win-x64\native"
$dllSource = Join-Path $blsDir "bin\bls384_256.dll"
$dllTarget = Join-Path $winOut "bls_eth.dll"
New-Item -ItemType Directory -Force -Path $winOut | Out-Null
Copy-Item $dllSource $dllTarget -Force

Write-Host "Herumi BLS dll copied to $dllTarget"
