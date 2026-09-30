<#
.SYNOPSIS
  Brings up Nethereum's OWN DevChain node and ERC-4337 Bundler as two real, standalone HTTP
  processes, pinned to a freshly-deployed EntryPoint, and prints the three values the enterprise
  demo's Setup tab -> External mode needs (Node RPC url, Bundler url, Funder private key).

.DESCRIPTION
  This is a local dev/demo convenience script only - it is not part of any shipped package.
  Windows only: it uses taskkill and Get-NetTCPConnection for process-tree teardown and port
  checks (the servers themselves are cross-platform - run them with the plain `dotnet run`
  commands this script wraps on Linux/macOS). It runs three of Nethereum's own console apps as
  child processes, wired together the same way a real deployment would be:

    1. Nethereum.DevChain.Server   - a real JSON-RPC node, listening on -NodePort.
    2. DeployEntryPoint            - a one-shot tool (this demo's own, under
                                      src\demos\Nethereum.AccountAbstraction.AppChain.Enterprise.Example\DeployEntryPoint)
                                      that deploys JUST a bare ERC-4337 EntryPoint onto that node and
                                      prints its address. Nothing else in the AppChain stack is
                                      deployed here - the demo's own Setup tab (External mode)
                                      deploys the rest (AccountRegistry, factory, SponsoredPaymaster,
                                      modules, the P1-D rule stack) on demand, reusing THIS
                                      EntryPoint via AADeployer's production-faithful on-chain-code
                                      reuse check rather than deploying a second one.
    3. Nethereum.AccountAbstraction.Bundler.RpcServer
                                    - a real ERC-4337 bundler, listening on -BundlerPort, pinned to
                                      the EntryPoint deployed in step 2, launched with AppChain-tuned
                                      knobs (`--unsafe`, near-zero stake/unstake-delay thresholds, a
                                      fast auto-bundle interval) - see the AppChain-tuned args block
                                      below for exactly which knobs are exposed as dedicated `--`
                                      flags versus the generic `--Bundler:<Property>=<value>` binder.

  The node and the bundler are two separate accounts on purpose, matching
  EnterpriseDemoHostBootstrap's own OwnerPrivateKey/BundlerPrivateKey: account[0] is the funder
  that pays for every AppChain deployment the demo does against this stack, account[1] is the
  bundler's own relayer key that pays for submitting bundles on-chain. Both are the well-known,
  public Hardhat/anvil dev mnemonic accounts - never use them for anything but a local throwaway
  chain.

  By default the script blocks after printing the summary, keeping both servers up so you can
  paste the three values into the demo's Setup tab -> External and drive it against them. Ctrl-C
  (or closing the window) runs the teardown in the `finally` block, which kills both process trees
  (`dotnet run` spawns a child .exe, so a plain Stop-Process on the `dotnet` PID would leak it).

  Pass -SmokeTest to instead run the same bring-up, then drive the FULL external flow over real
  HTTP: ExternalEnterpriseInfrastructureProvisioner connects to the two servers just started,
  deploys the AppChain AA stack + P1-D rule stack through the funder account, then an admin enrolls
  a user, the owner installs a tier-1 capped role, and a within-cap payment executes (via
  `dotnet test` filtered to ExternalInfrastructureProvisionerSmokeTests, in Core.Tests) - proving
  the external path end to end, not just that the servers answer eth_chainId. Always tears down,
  and exits non-zero on any failure.

.PARAMETER NodePort
  Port the DevChain server listens on. Default 8545.

.PARAMETER BundlerPort
  Port the Bundler RpcServer listens on. Default 4337.

.PARAMETER ChainId
  Chain id both the node and the bundler run at. Default 31337 (0x7a69) - the same chain id
  EnterpriseDemoHostBootstrap's embedded DevChain uses.

.PARAMETER SmokeTest
  Run the full external flow (enroll -> install a capped role -> pay within cap -> over-cap
  rejected) against the servers this script starts, instead of blocking for manual use. Prints
  PASS/FAIL, always tears down, and exits with a non-zero code on failure.

.EXAMPLE
  ./run-enterprise-local-stack.ps1
  # ... paste the printed Node RPC / Bundler URL / Funder key into the Setup tab -> External ...
  # Ctrl-C when done.

.EXAMPLE
  ./run-enterprise-local-stack.ps1 -SmokeTest
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

# The well-known, public Hardhat/anvil dev mnemonic accounts - NOT secrets, never use on a real
# chain. account[0] funds every AppChain deployment the demo does (Setup tab's FunderPrivateKey);
# account[1] is the bundler's own relayer signer, kept distinct exactly like
# EnterpriseDemoHostBootstrap's OwnerPrivateKey/BundlerPrivateKey in the embedded bootstrap.
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
    $nodeProcess = Start-Process dotnet -ArgumentList $nodeArgs -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $env:TEMP "nethereum-enterprise-devchain-$NodePort.log") -RedirectStandardError (Join-Path $env:TEMP "nethereum-enterprise-devchain-$NodePort.err.log")

    $observedChainId = Wait-ForRpcReady -Name "DevChain node" -Url "$nodeUrl/" -Process $nodeProcess
    if ($observedChainId -ne $chainIdHex) {
        throw "DevChain node reported chainId $observedChainId, expected $chainIdHex ($ChainId)."
    }

    # 2. Deploy a bare ERC-4337 EntryPoint onto the node. The bundler pins its supported
    # EntryPoint at construction, so this has to happen before the bundler starts. Nothing else
    # in the AppChain stack is deployed here - the demo's own Setup tab (External mode) deploys
    # the rest through the funder account, reusing this EntryPoint via AADeployer's on-chain-code
    # reuse check.
    Write-Host "==> Deploying the EntryPoint to $nodeUrl ..." -ForegroundColor Cyan
    $deployProject = Join-Path $repoRoot "src\demos\Nethereum.AccountAbstraction.AppChain.Enterprise.Example\DeployEntryPoint"
    $deployOut = dotnet run --project $deployProject -- $nodeUrl $funderKey
    $deployOut | Write-Host
    $entryPoint = ($deployOut | Select-String '^ENTRYPOINT=(.+)$').Matches.Groups[1].Value
    if (-not $entryPoint) {
        throw "DeployEntryPoint did not print ENTRYPOINT=... - see output above."
    }
    Write-Host "    EntryPoint=$entryPoint" -ForegroundColor Green

    # 3. Build the bundler --no-incremental first: a stale cross-project incremental build can
    # leave `dotnet run` serving an old binary that silently ignores a source fix, then start it
    # as a real HTTP server pinned to the EntryPoint just deployed, with AppChain-tuned knobs.
    #
    # `--unsafe`, `--minStake` and `--minUnstakeDelay` are dedicated CLI flags
    # (BundlerRpcServerConfig binds them explicitly in Program.cs) - `--unsafe` needs the explicit
    # `=true` form: the default CommandLineConfigurationProvider treats a bare `--unsafe` (no `=`)
    # as expecting the NEXT array element as its value, which would silently swallow the following
    # `--minStake=0` token instead of parsing as a boolean. AutoBundleIntervalMs has no dedicated
    # flag - it is set here via the generic `--Bundler:<Property>=<value>` binder
    # (`builder.Configuration.Bind("Bundler", config)` in Program.cs reads that section, and
    # AddCommandLine registers `--Bundler:X=Y` as `Bundler:X` in configuration), the same
    # mechanism that reaches every other BundlerRpcServerConfig property without its own flag.
    Write-Host "==> Building Bundler.RpcServer (--no-incremental) ..." -ForegroundColor Cyan
    $bundlerProject = Join-Path $repoRoot "src\Nethereum.AccountAbstraction.Bundler.RpcServer\Nethereum.AccountAbstraction.Bundler.RpcServer.csproj"
    dotnet build $bundlerProject -c Debug --no-incremental | Write-Host
    if ($LASTEXITCODE -ne 0) { throw "Bundler.RpcServer build failed." }

    Write-Host "==> Starting Bundler.RpcServer on port $BundlerPort (AppChain-tuned: unsafe, near-zero stake/unstake-delay, 1s auto-bundle) ..." -ForegroundColor Cyan
    $bundlerArgs = @(
        "run", "--no-build", "--project", (Join-Path $repoRoot "src\Nethereum.AccountAbstraction.Bundler.RpcServer"),
        "--", "--rpc=$nodeUrl", "--entryPoint=$entryPoint", "--beneficiary=$funderAddress",
        "--privateKey=$bundlerSignerKey", "--port=$BundlerPort", "--chainId=$ChainId",
        "--unsafe=true", "--minStake=0", "--minUnstakeDelay=0", "--Bundler:AutoBundleIntervalMs=1000"
    )
    $bundlerProcess = Start-Process dotnet -ArgumentList $bundlerArgs -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $env:TEMP "nethereum-enterprise-bundler-$BundlerPort.log") -RedirectStandardError (Join-Path $env:TEMP "nethereum-enterprise-bundler-$BundlerPort.err.log")

    $observedBundlerChainId = Wait-ForRpcReady -Name "Bundler" -Url $bundlerUrl -Process $bundlerProcess
    if ($observedBundlerChainId -ne $chainIdHex) {
        throw "Bundler reported chainId $observedBundlerChainId, expected $chainIdHex ($ChainId)."
    }

    Write-Host "==> Confirming eth_supportedEntryPoints ..." -ForegroundColor Cyan
    $supportedResp = Invoke-JsonRpc -Url $bundlerUrl -Method "eth_supportedEntryPoints"
    $supported = @($supportedResp.result)
    Write-Host "    eth_supportedEntryPoints -> $($supported -join ', ')"
    if ($supported -notcontains $entryPoint) {
        throw "Bundler's eth_supportedEntryPoints ($($supported -join ', ')) does not include the deployed EntryPoint ($entryPoint)."
    }

    if ($SmokeTest) {
        # Drive the FULL external flow over real HTTP: ExternalEnterpriseInfrastructureProvisioner
        # connects to the two servers above, deploys the AppChain AA stack + P1-D rule stack
        # through the funder account (reusing the EntryPoint deployed in step 2), then an admin
        # enrolls a user, the owner installs a tier-1 capped role, and a within-cap payment
        # executes - the same assertions the embedded EnterpriseOperatorViewModelTests make,
        # just over HTTP against these standalone processes instead of an in-process bundler.
        Write-Host "==> Running the external smoke test (ExternalInfrastructureProvisionerSmokeTests) ..." -ForegroundColor Cyan
        $testProject = Join-Path $repoRoot "src\demos\Nethereum.AccountAbstraction.AppChain.Enterprise.Example\Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core.Tests\Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core.Tests.csproj"

        $env:AA_ENTERPRISE_EXTERNAL_NODE_URL = $nodeUrl
        $env:AA_ENTERPRISE_EXTERNAL_BUNDLER_URL = $bundlerUrl
        $env:AA_ENTERPRISE_EXTERNAL_FUNDER_KEY = $funderKey
        try {
            dotnet test $testProject --filter "FullyQualifiedName~ExternalInfrastructureProvisionerSmokeTests" -v normal | Write-Host
            $testExitCode = $LASTEXITCODE
        }
        finally {
            Remove-Item Env:\AA_ENTERPRISE_EXTERNAL_NODE_URL -ErrorAction SilentlyContinue
            Remove-Item Env:\AA_ENTERPRISE_EXTERNAL_BUNDLER_URL -ErrorAction SilentlyContinue
            Remove-Item Env:\AA_ENTERPRISE_EXTERNAL_FUNDER_KEY -ErrorAction SilentlyContinue
        }
        if ($testExitCode -ne 0) {
            throw "ExternalInfrastructureProvisionerSmokeTests failed (dotnet test exit code $testExitCode) - see output above."
        }

        Write-Host ""
        Write-Host "PASS - node chainId=$observedChainId, bundler chainId=$observedBundlerChainId, EntryPoint=$entryPoint confirmed in eth_supportedEntryPoints, and the full external enroll -> install-capped-role -> pay-within-cap -> over-cap-rejected flow succeeded over real HTTP." -ForegroundColor Green
        $exitCode = 0
    }
    else {
        Write-Host ""
        Write-Host "========================================================================" -ForegroundColor Yellow
        Write-Host " Nethereum's OWN DevChain + AA Bundler are running as real HTTP servers." -ForegroundColor Yellow
        Write-Host " Paste into the demo's Setup tab -> External:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "   Node RPC url        : $nodeUrl"
        Write-Host "   Bundler url         : $bundlerUrl"
        Write-Host "   Funder private key  : $funderKey"
        Write-Host ""
        Write-Host "   (that funder key is account[0] of the standard Hardhat dev mnemonic," -ForegroundColor DarkGray
        Write-Host "    funded with 10000 ETH on this chain - the demo pays every AppChain" -ForegroundColor DarkGray
        Write-Host "    deployment and every operator payment from it. EntryPoint deployed at" -ForegroundColor DarkGray
        Write-Host "    $entryPoint - the rest of the AppChain stack is NOT pre-deployed; the" -ForegroundColor DarkGray
        Write-Host "    demo's own Setup tab deploys it on demand.)" -ForegroundColor DarkGray
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
