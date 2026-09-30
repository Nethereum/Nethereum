<#
.SYNOPSIS
  Brings up Nethereum's OWN DevChain node and ERC-4337 Bundler as two real, standalone
  HTTP processes, deploys an EntryPoint onto the node, and prints the three values the
  demo's tab0 "External" mode needs (Node RPC url, Bundler url, Funder private key).

.DESCRIPTION
  This is a local dev/demo convenience script only - it is not part of any shipped
  package. Windows only: it uses taskkill and Get-NetTCPConnection for process-tree
  teardown and port checks (the servers themselves are cross-platform - run them with
  the plain `dotnet run` commands this script wraps on Linux/macOS). It runs three of
  Nethereum's own console apps as child processes, wired together the same way a real
  deployment would be:

    1. Nethereum.DevChain.Server   - a real JSON-RPC node, listening on -NodePort.
    2. DeployContracts             - a one-shot tool (from the AA compliance harness)
                                      that deploys the ERC-4337 EntryPoint onto that node
                                      and prints its address.
    3. Nethereum.AccountAbstraction.Bundler.RpcServer
                                    - a real ERC-4337 bundler, listening on -BundlerPort,
                                      pinned to the EntryPoint deployed in step 2.

  The node and the bundler are two separate accounts on purpose (matching the demo's own
  in-process bootstrap, HostBootstrap.OwnerPrivateKey/BundlerPrivateKey): account[0] is the
  funder that pays for every deployment the demo does against this stack, account[1] is the
  bundler's own relayer key that pays for submitting bundles on-chain. Both are the
  well-known, public Hardhat/anvil dev mnemonic accounts - never use them for anything but a
  local throwaway chain.

  By default the script blocks after printing the summary, keeping both servers up so you can
  paste the three values into tab0 -> External and drive the demo against them. Ctrl-C (or
  closing the window) runs the teardown in the `finally` block, which kills both process trees
  (`dotnet run` spawns a child .exe, so a plain Stop-Process on the `dotnet` PID would leak it).

  Pass -SmokeTest to instead run the same bring-up, verify the stack answers correctly, tear
  down immediately, and exit non-zero on any failure - useful for CI or a quick sanity check
  without needing to run the demo UI at all.

.PARAMETER NodePort
  Port the DevChain server listens on. Default 8545.

.PARAMETER BundlerPort
  Port the Bundler RpcServer listens on. Default 4337.

.PARAMETER ChainId
  Chain id both the node and the bundler run at. Default 31337 (0x7a69).

.PARAMETER SmokeTest
  Verify the stack instead of blocking: assert the node's chainId, the bundler's chainId, and
  that the bundler's eth_supportedEntryPoints reports the exact EntryPoint DeployContracts
  deployed. Prints PASS/FAIL, always tears down, and exits with a non-zero code on failure.

.EXAMPLE
  ./run-local-stack.ps1
  # ... paste the printed Node RPC / Bundler URL / Funder key into tab0 -> External ...
  # Ctrl-C when done.

.EXAMPLE
  ./run-local-stack.ps1 -SmokeTest
#>
[CmdletBinding()]
param(
    [int]    $NodePort = 8545,
    [int]    $BundlerPort = 4337,
    [long]   $ChainId = 31337,
    [switch] $SmokeTest
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")   # src\demos\<this>\ -> repo root

# The well-known, public Hardhat/anvil dev mnemonic accounts - NOT secrets, never use on a
# real chain. account[0] funds every deployment the demo does (tab0's FunderPrivateKey);
# account[1] is the bundler's own relayer signer, kept distinct exactly like
# HostBootstrap's OwnerPrivateKey/BundlerPrivateKey in the in-process bootstrap.
$funderKey        = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80"
$funderAddress    = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266"
$bundlerSignerKey = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d"

$nodeUrl = "http://127.0.0.1:$NodePort"
$bundlerUrl = "http://127.0.0.1:$BundlerPort/"
$chainIdHex = "0x" + [Convert]::ToString($ChainId, 16)

# Posts a JSON-RPC request and returns the parsed response, or $null if the server isn't
# answering yet (used by the readiness polls below - a plain TCP-connect check isn't enough
# because Kestrel can accept the connection before the app pipeline is ready to answer).
function Invoke-JsonRpc {
    param([string] $Url, [string] $Method, [array] $Params = @())
    $body = @{ jsonrpc = "2.0"; id = 1; method = $Method; params = $Params } | ConvertTo-Json -Compress
    try {
        return Invoke-RestMethod -Uri $Url -Method Post -Body $body -ContentType "application/json" -TimeoutSec 3
    } catch {
        return $null
    }
}

# Polls a JSON-RPC endpoint until it answers eth_chainId, or throws after $TimeoutSec.
function Wait-ForRpcReady {
    param([string] $Name, [string] $Url, [System.Diagnostics.Process] $Process, [int] $TimeoutSec = 60)
    Write-Host "==> Waiting for $Name to become ready on $Url ..." -ForegroundColor Cyan
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            throw "$Name process exited early (exit code $($Process.ExitCode)) before becoming ready - see console output above."
        }
        $resp = Invoke-JsonRpc -Url $Url -Method "eth_chainId"
        if ($resp -and $resp.result) {
            Write-Host "    $Name ready (chainId=$($resp.result))." -ForegroundColor Green
            return $resp.result
        }
        Start-Sleep -Milliseconds 500
    }
    throw "$Name did not become ready on $Url within ${TimeoutSec}s."
}

# Fails fast and clearly if a port we need is already bound by something else, rather than
# letting `dotnet run` fail deep inside Kestrel with a confusing bind exception.
function Assert-PortFree {
    param([int] $Port, [string] $Name)
    $inUse = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($inUse) {
        throw "Port $Port ($Name) is already in use - stop whatever is listening on it (or pass a different -NodePort/-BundlerPort) before running this script."
    }
}

# Kills a process tree (dotnet run spawns a child .exe, so stopping just the `dotnet` PID
# leaves the actual server running and the port held) - same approach as
# tests/Nethereum.AccountAbstraction.ComplianceHarness/run-spec-tests.ps1.
function Stop-ProcessTree {
    param([System.Diagnostics.Process] $Process, [string] $Name)
    if ($Process -and -not $Process.HasExited) {
        Write-Host "    stopping $Name (pid $($Process.Id)) ..." -ForegroundColor DarkGray
        taskkill /PID $Process.Id /T /F 2>$null | Out-Null
    }
}

$nodeProcess = $null
$bundlerProcess = $null
$entryPoint = $null
$exitCode = 0

try {
    # Fail fast and clearly if a port we need is already taken, before spending time on
    # `dotnet run`/build only to hit a confusing bind exception deep inside Kestrel.
    Assert-PortFree -Port $NodePort -Name "DevChain node"
    Assert-PortFree -Port $BundlerPort -Name "Bundler"

    # 1. Start Nethereum's own DevChain node as a real HTTP JSON-RPC server.
    Write-Host "==> Starting Nethereum.DevChain.Server on port $NodePort (chainId $ChainId) ..." -ForegroundColor Cyan
    $nodeArgs = @(
        "run", "--project", (Join-Path $repoRoot "src\Nethereum.DevChain.Server"),
        "--", "--port", "$NodePort", "--chain-id", "$ChainId"
    )
    $nodeProcess = Start-Process dotnet -ArgumentList $nodeArgs -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $env:TEMP "nethereum-devchain-$NodePort.log") -RedirectStandardError (Join-Path $env:TEMP "nethereum-devchain-$NodePort.err.log")

    $observedChainId = Wait-ForRpcReady -Name "DevChain node" -Url "$nodeUrl/" -Process $nodeProcess
    if ($observedChainId -ne $chainIdHex) {
        throw "DevChain node reported chainId $observedChainId, expected $chainIdHex ($ChainId)."
    }

    # 2. Deploy the ERC-4337 EntryPoint onto the node. The bundler pins its supported
    # EntryPoint at construction, so this has to happen before the bundler starts.
    # DeployContracts is a tool from the AA compliance harness (tests/), reused here purely
    # as a local orchestration convenience - it deploys the same Nethereum-generated
    # EntryPoint the demo itself deploys, it just needs to exist before the bundler starts.
    Write-Host "==> Deploying EntryPoint to $nodeUrl ..." -ForegroundColor Cyan
    $deployProject = Join-Path $repoRoot "tests\Nethereum.AccountAbstraction.ComplianceHarness\DeployContracts"
    $deployOut = dotnet run --project $deployProject -- $nodeUrl $funderKey
    $deployOut | Write-Host
    $entryPoint = ($deployOut | Select-String '^ENTRYPOINT=(.+)$').Matches.Groups[1].Value
    if (-not $entryPoint) {
        throw "DeployContracts did not print ENTRYPOINT=... - see output above."
    }
    Write-Host "    EntryPoint=$entryPoint" -ForegroundColor Green

    # 3. Build the bundler --no-incremental first: a stale cross-project incremental build can
    # leave `dotnet run` serving an old binary that silently ignores a source fix (the same
    # caution run-spec-tests.ps1 takes), then start it as a real HTTP server pinned to the
    # EntryPoint just deployed.
    Write-Host "==> Building Bundler.RpcServer (--no-incremental) ..." -ForegroundColor Cyan
    $bundlerProject = Join-Path $repoRoot "src\Nethereum.AccountAbstraction.Bundler.RpcServer\Nethereum.AccountAbstraction.Bundler.RpcServer.csproj"
    dotnet build $bundlerProject -c Debug --no-incremental | Write-Host
    if ($LASTEXITCODE -ne 0) { throw "Bundler.RpcServer build failed." }

    Write-Host "==> Starting Bundler.RpcServer on port $BundlerPort ..." -ForegroundColor Cyan
    $bundlerArgs = @(
        "run", "--no-build", "--project", (Join-Path $repoRoot "src\Nethereum.AccountAbstraction.Bundler.RpcServer"),
        "--", "--rpc=$nodeUrl", "--entryPoint=$entryPoint", "--beneficiary=$funderAddress",
        "--privateKey=$bundlerSignerKey", "--port=$BundlerPort", "--chainId=$ChainId"
    )
    $bundlerProcess = Start-Process dotnet -ArgumentList $bundlerArgs -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $env:TEMP "nethereum-bundler-$BundlerPort.log") -RedirectStandardError (Join-Path $env:TEMP "nethereum-bundler-$BundlerPort.err.log")

    $observedBundlerChainId = Wait-ForRpcReady -Name "Bundler" -Url $bundlerUrl -Process $bundlerProcess
    if ($observedBundlerChainId -ne $chainIdHex) {
        throw "Bundler reported chainId $observedBundlerChainId, expected $chainIdHex ($ChainId)."
    }

    if ($SmokeTest) {
        # Verify the stack answers correctly instead of handing it to a human: the node and
        # bundler chainIds are already asserted above by Wait-ForRpcReady; the one thing left
        # to prove is that the bundler is actually pinned to the EntryPoint we just deployed
        # (not some other address it happened to be configured with).
        Write-Host "==> Smoke-testing eth_supportedEntryPoints ..." -ForegroundColor Cyan
        $supportedResp = Invoke-JsonRpc -Url $bundlerUrl -Method "eth_supportedEntryPoints"
        $supported = @($supportedResp.result)
        Write-Host "    eth_supportedEntryPoints -> $($supported -join ', ')"
        if ($supported -notcontains $entryPoint) {
            throw "Bundler's eth_supportedEntryPoints ($($supported -join ', ')) does not include the deployed EntryPoint ($entryPoint)."
        }

        Write-Host ""
        Write-Host "PASS - node chainId=$observedChainId, bundler chainId=$observedBundlerChainId, EntryPoint=$entryPoint confirmed in eth_supportedEntryPoints." -ForegroundColor Green
        $exitCode = 0
    }
    else {
        Write-Host ""
        Write-Host "========================================================================" -ForegroundColor Yellow
        Write-Host " Nethereum's OWN DevChain + AA Bundler are running as real HTTP servers." -ForegroundColor Yellow
        Write-Host " Paste into tab0 -> External:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "   Node RPC url        : $nodeUrl"
        Write-Host "   Bundler url         : $bundlerUrl"
        Write-Host "   Funder private key  : $funderKey"
        Write-Host ""
        Write-Host "   (that funder key is account[0] of the standard Hardhat dev mnemonic," -ForegroundColor DarkGray
        Write-Host "    funded with 10000 ETH on this chain - the demo pays every deployment" -ForegroundColor DarkGray
        Write-Host "    and faucet transfer from it. EntryPoint deployed at $entryPoint.)" -ForegroundColor DarkGray
        Write-Host "========================================================================" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Press Ctrl-C to stop both servers." -ForegroundColor Cyan

        while ($true) {
            Start-Sleep -Seconds 1
            if ($nodeProcess.HasExited) { throw "DevChain node process exited unexpectedly (exit code $($nodeProcess.ExitCode))." }
            if ($bundlerProcess.HasExited) { throw "Bundler process exited unexpectedly (exit code $($bundlerProcess.ExitCode))." }
        }
    }
}
catch {
    Write-Host ""
    Write-Host "FAIL - $($_.Exception.Message)" -ForegroundColor Red
    $exitCode = 1
}
finally {
    Write-Host "==> Tearing down ..." -ForegroundColor Cyan
    Stop-ProcessTree -Process $bundlerProcess -Name "Bundler.RpcServer"
    Stop-ProcessTree -Process $nodeProcess -Name "DevChain.Server"
}

exit $exitCode
