param(
    [string]$ReleaseSuffix = "",
    [string]$Configuration = "Release",
    [int]$MaxParallel = 0  # 0 = auto (processor count)
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$outputDir = Join-Path $scriptDir "packages"
$nativeArtifacts = Join-Path $repoRoot "nativeartifacts"

if ($MaxParallel -le 0) { $MaxParallel = [Environment]::ProcessorCount }
Write-Host "Using $MaxParallel parallel threads" -ForegroundColor Cyan

$propsArr = @("/property:TargetNet35=false")
if ($ReleaseSuffix) { $propsArr += "/property:ReleaseSuffix=$ReleaseSuffix" }
$props = $propsArr

# All projects to pack (same list as nuget.bat)
$projects = @(
    "Nethereum.Web3\Nethereum.Web3.csproj"
    "Nethereum.Web3\Nethereum.Web3Lite.csproj"
    "Nethereum.Hex"
    "Nethereum.ABI"
    "Nethereum.JsonRpc.Client"
    "Nethereum.RPC"
    "Nethereum.StandardTokenEIP20"
    "Nethereum.JsonRpc.IpcClient"
    "Nethereum.JsonRpc.RpcClient"
    "Nethereum.JsonRpc.WebSocketClient"
    "Nethereum.KeyStore"
    "Nethereum.ENS"
    "Nethereum.Quorum"
    "Nethereum.Geth"
    "Nethereum.Contracts"
    "Nethereum.RLP"
    "Nethereum.Signer"
    "Nethereum.Signer.AzureKeyVault"
    "Nethereum.Signer.AWSKeyManagement"
    "Nethereum.Signer.Ledger"
    "Nethereum.Signer.Trezor"
    "Nethereum.Util"
    "Nethereum.HDWallet"
    "Nethereum.Parity"
    "Nethereum.Accounts"
    "Nethereum.Parity.Reactive"
    "Nethereum.RPC.Reactive"
    "Nethereum.Model"
    "Nethereum.StandardNonFungibleTokenERC721"
    "Nethereum.Besu"
    "Nethereum.RSK"
    "Nethereum.BlockchainProcessing"
    "Nethereum.Signer.EIP712"
    "Nethereum.GnosisSafe"
    "Nethereum.Siwe.Core"
    "Nethereum.Siwe"
    "Nethereum.Optimism"
    "Nethereum.Metamask"
    "Nethereum.Metamask.Blazor"
    "Nethereum.UI"
    "Nethereum.EVM"
    "Nethereum.Merkle"
    "Nethereum.Merkle.Patricia"
    "Nethereum.RPC.Extensions"
    "Nethereum.EVM.Contracts"
    "Nethereum.DataServices"
    "Nethereum.WalletConnect"
    "Nethereum.Mud"
    "Nethereum.Mud.Contracts"
    "Nethereum.Mud.Repositories.Postgres"
    "Nethereum.Mud.Repositories.EntityFramework"
    "Nethereum.Util.Rest"
    "Nethereum.Blazor"
    "Nethereum.EIP6963WalletInterop"
    "Nethereum.Reown.AppKit.Blazor"
    "Nethereum.MudBlazorComponents"
    "Nethereum.AccountAbstraction"
    "Nethereum.JsonRpc.SystemTextJsonRpcClient"
    "Nethereum.Beaconchain"
    "Nethereum.Consensus.LightClient"
    "Nethereum.Consensus.Ssz"
    "Nethereum.Ssz"
    "Nethereum.Signer.Bls"
    "Nethereum.Signer.Bls.Herumi"
    "Nethereum.ChainStateVerification"
    "Nethereum.Circles"
    "Nethereum.Maui.AndroidUsb"
    "Nethereum.TokenServices"
    "Nethereum.Uniswap"
    "Nethereum.Wallet"
    "Nethereum.Wallet.RpcRequests"
    "Nethereum.Wallet.Trezor"
    "Nethereum.Wallet.UI.Components"
    "Nethereum.Wallet.UI.Components.Blazor"
    "Nethereum.Wallet.UI.Components.Blazor.Trezor"
    "Nethereum.Wallet.UI.Components.Maui"
    "Nethereum.Wallet.UI.Components.Trezor"
    "Nethereum.X402"
    "Nethereum.AccountAbstraction.Bundler"
    "Nethereum.AccountAbstraction.Bundler.RocksDB"
    "Nethereum.AccountAbstraction.Bundler.RpcServer"
    "Nethereum.AppChain"
    "Nethereum.AppChain.Sequencer"
    "Nethereum.AppChain.Sync"
    "Nethereum.BlockchainStorage.Processors"
    "Nethereum.BlockchainStorage.Processors.Postgres"
    "Nethereum.BlockchainStorage.Processors.Sqlite"
    "Nethereum.BlockchainStorage.Processors.SqlServer"
    "Nethereum.BlockchainStorage.Token.Postgres"
    "Nethereum.BlockchainStore.EFCore"
    "Nethereum.BlockchainStore.Postgres"
    "Nethereum.BlockchainStore.Sqlite"
    "Nethereum.BlockchainStore.SqlServer"
    "Nethereum.CoreChain"
    "Nethereum.CoreChain.RocksDB"
    "Nethereum.DevChain"
    "Nethereum.DevChain.Server"
    "Nethereum.EVM.Precompiles.Bls"
    "Nethereum.EVM.Precompiles.Kzg"
    "Nethereum.Blazor.Solidity"
    "Nethereum.Explorer"
    "Nethereum.Sourcify.Database"
)

# Resolve csproj paths
function Get-CsprojPath($entry) {
    if ($entry -like "*.csproj") {
        return Join-Path $scriptDir $entry
    }
    $dir = Join-Path $scriptDir $entry
    $csproj = Get-ChildItem -Path $dir -Filter "*.csproj" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($csproj) { return $csproj.FullName }
    return $null
}

# Step 1: Pack Herumi to nativeartifacts first (needed as a dependency)
Write-Host "`n=== Step 1: Pack Herumi to nativeartifacts ===" -ForegroundColor Cyan
$herumiCsproj = Join-Path $scriptDir "Nethereum.Signer.Bls.Herumi\Nethereum.Signer.Bls.Herumi.csproj"
dotnet pack $herumiCsproj -c $Configuration --include-symbols -p:SymbolPackageFormat=snupkg @props -o $nativeArtifacts
if ($LASTEXITCODE -ne 0) { Write-Host "Herumi pack failed" -ForegroundColor Red; exit 1 }

# Resolve all csproj paths
$csprojPaths = @()
foreach ($p in $projects) {
    $path = Get-CsprojPath $p
    if ($path -and (Test-Path $path)) {
        $csprojPaths += $path
    } else {
        Write-Host "  SKIP: $p (not found)" -ForegroundColor Yellow
    }
}

# Step 2: Parallel restore (shared lock can conflict, so restore first)
Write-Host "`n=== Step 2: Restoring all projects ===" -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet restore (Join-Path $scriptDir ".." | Join-Path -ChildPath "Nethereum.AllPackages.sln") @props --nologo -v q 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Solution restore failed, falling back to per-project restore" -ForegroundColor Yellow
    $csprojPaths | ForEach-Object -ThrottleLimit $MaxParallel -Parallel {
        dotnet restore $_ $using:propsArr --nologo -v q 2>&1 | Out-Null
    }
}
Write-Host "Restore phase: $($sw.Elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor Cyan

# Step 3: Parallel build
Write-Host "`n=== Step 3: Building all projects (parallel x$MaxParallel) ===" -ForegroundColor Cyan
$sw.Restart()
$buildResults = [System.Collections.Concurrent.ConcurrentDictionary[string, bool]]::new()

$csprojPaths | ForEach-Object -ThrottleLimit $MaxParallel -Parallel {
    $csproj = $_
    $name = [System.IO.Path]::GetFileNameWithoutExtension($csproj)
    $result = $using:buildResults
    $output = dotnet build $csproj -c $using:Configuration $using:propsArr --no-restore --nologo -v q 2>&1
    if ($LASTEXITCODE -eq 0) {
        $result[$name] = $true
    } else {
        $result[$name] = $false
    }
}

$built = ($buildResults.Values | Where-Object { $_ }).Count
$failedCount = ($buildResults.Values | Where-Object { -not $_ }).Count
$failedNames = ($buildResults.GetEnumerator() | Where-Object { -not $_.Value } | ForEach-Object { $_.Key })
Write-Host "Build phase: $($sw.Elapsed.TotalSeconds.ToString('F1'))s ($built OK, $failedCount failed)" -ForegroundColor Cyan
if ($failedNames) { Write-Host "  Failed: $($failedNames -join ', ')" -ForegroundColor Red }

# Step 4: Parallel pack (--no-build = fast)
Write-Host "`n=== Step 4: Packing (no-build, parallel x$MaxParallel) ===" -ForegroundColor Cyan
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }

$sw.Restart()
$packResults = [System.Collections.Concurrent.ConcurrentDictionary[string, bool]]::new()

$csprojPaths | ForEach-Object -ThrottleLimit $MaxParallel -Parallel {
    $csproj = $_
    $name = [System.IO.Path]::GetFileNameWithoutExtension($csproj)
    $br = $using:buildResults
    $pr = $using:packResults
    if ($br.ContainsKey($name) -and -not $br[$name]) {
        $pr[$name] = $false
        return
    }
    $output = dotnet pack $csproj -c $using:Configuration --no-build --no-restore --include-symbols -p:SymbolPackageFormat=snupkg $using:propsArr --nologo -v q -o $using:outputDir 2>&1
    $pr[$name] = ($LASTEXITCODE -eq 0)
}

$packed = ($packResults.Values | Where-Object { $_ }).Count
Write-Host "Pack phase: $($sw.Elapsed.TotalSeconds.ToString('F1'))s ($packed packages)" -ForegroundColor Cyan

# Summary
$nupkgCount = (Get-ChildItem -Path $outputDir -Filter "*.nupkg" -ErrorAction SilentlyContinue).Count
$snupkgCount = (Get-ChildItem -Path $outputDir -Filter "*.snupkg" -ErrorAction SilentlyContinue).Count
Write-Host "`n=== Done ===" -ForegroundColor Green
Write-Host "  Packages: $outputDir ($nupkgCount nupkg, $snupkgCount snupkg)"
Write-Host "  Herumi:   $nativeArtifacts"
if ($failedNames) {
    Write-Host "  Build failures: $($failedNames -join ', ')" -ForegroundColor Red
}
