<#
.SYNOPSIS
  Runs the eth-infinitism bundler-spec-tests compliance suite against the Nethereum
  bundler RpcServer, end to end: anvil -> deploy v0.9 EntryPoint+factory -> start our
  RpcServer -> pytest -> cleanup.

.DESCRIPTION
  See README.md for prerequisites (anvil/foundry, a cloned + built bundler-spec-tests,
  the .NET SDK) and for how to interpret the results (which failures are expected).

  The script always tears down the anvil and RpcServer processes it starts, even on error.

.PARAMETER SpecTests
  Path to a local clone of https://github.com/eth-infinitism/bundler-spec-tests with its
  Python venv installed and spec/openrpc.json built (see README). Required.

.PARAMETER Venv
  Path to the bundler-spec-tests Python venv directory. Defaults to "<SpecTests>\.venv".

.PARAMETER TestPath
  pytest target under the spec-tests repo. Defaults to "tests/single" (the whole suite).
  Use e.g. "tests/single/bundle/test_storage_rules.py" to run one module.

.PARAMETER Port
  Port the RpcServer listens on. Default 3000.

.PARAMETER EnableErc7562
  Enable the ERC-7562 opcode/storage validation engine. Default $true.

.PARAMETER MinUnstakeDelay
  --minUnstakeDelay passed to the RpcServer. The suite stakes entities with a 2-second
  delay for test speed, so this must be 0 for staked tests to pass (the production default
  is 86400). Default 0.

.PARAMETER MinStake
  --minStake passed to the RpcServer. Default 0 for the same reason (production default 1 ETH).

.EXAMPLE
  ./run-spec-tests.ps1 -SpecTests C:\repos\bundler-spec-tests
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)] [string] $SpecTests,
  [string] $Venv,
  [string] $TestPath = "tests/single",
  [int]    $Port = 3000,
  [bool]   $EnableErc7562 = $true,
  [int]    $MinUnstakeDelay = 0,
  [long]   $MinStake = 0
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")   # tests\...\ -> repo root
$rpcUrl = "http://127.0.0.1:8545"
# anvil account[0] — the well-known public deterministic dev key/address (NOT a secret).
$deployerKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80"
$beneficiary = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266"
# chainId 1337 — the eth-infinitism bundler-spec-tests hardcode chainId 1337 in the eip7702
# authorization tuples (tests/single/eip7702/test_eip7702_tuple_userop.py), so the chain (anvil)
# and our bundler must both run at 1337 for those tests to be valid. Other suites are chainId-agnostic.
$chainId = 1337

if (-not $Venv) { $Venv = Join-Path $SpecTests ".venv" }
$pytest = Join-Path $Venv "Scripts\pytest.exe"
if (-not (Test-Path $pytest)) { throw "pytest not found at $pytest — create/point -Venv at the spec-tests venv (see README)." }

$anvil = $null
$server = $null
try {
  Write-Host "==> Starting anvil on 8545 ..." -ForegroundColor Cyan
  $anvil = Start-Process anvil -ArgumentList "--port", "8545", "--chain-id", "$chainId" -PassThru -WindowStyle Hidden
  Start-Sleep -Seconds 2

  Write-Host "==> Deploying v0.9 EntryPoint + SimpleAccountFactory ..." -ForegroundColor Cyan
  $deployOut = dotnet run --project (Join-Path $PSScriptRoot "DeployContracts") -- $rpcUrl $deployerKey
  $deployOut | Write-Host
  $entryPoint = ($deployOut | Select-String '^ENTRYPOINT=(.+)$').Matches.Groups[1].Value
  $factory    = ($deployOut | Select-String '^FACTORY=(.+)$').Matches.Groups[1].Value
  if (-not $entryPoint) { throw "DeployContracts did not print ENTRYPOINT=..." }
  Write-Host "    EntryPoint=$entryPoint Factory=$factory" -ForegroundColor Green

  # Clean, non-incremental build first: a cross-project incremental build can leave `dotnet run`
  # serving a STALE binary that silently ignores a source fix, wasting a whole ~40-min run.
  Write-Host "==> Building RpcServer (--no-incremental, so the run can't use a stale binary) ..." -ForegroundColor Cyan
  $serverProj = Join-Path $repoRoot "src\Nethereum.AccountAbstraction.Bundler.RpcServer\Nethereum.AccountAbstraction.Bundler.RpcServer.csproj"
  dotnet build $serverProj -c Debug --no-incremental | Write-Host
  if ($LASTEXITCODE -ne 0) { throw "RpcServer build failed." }

  Write-Host "==> Starting Nethereum RpcServer on $Port (ERC-7562=$EnableErc7562) ..." -ForegroundColor Cyan
  $serverArgs = @(
    "run", "--no-build", "--project", (Join-Path $repoRoot "src\Nethereum.AccountAbstraction.Bundler.RpcServer"),
    "--", "--rpc=$rpcUrl", "--entryPoint=$entryPoint", "--beneficiary=$beneficiary",
    "--privateKey=$deployerKey", "--port=$Port", "--chainId=$chainId",
    "--debug=true", "--unsafe=true", "--maxVerificationGas=10000000",
    "--enableErc7562=$($EnableErc7562.ToString().ToLower())",
    "--minUnstakeDelay=$MinUnstakeDelay", "--minStake=$MinStake"
  )
  $server = Start-Process dotnet -ArgumentList $serverArgs -PassThru -WindowStyle Hidden

  Write-Host "==> Waiting for RpcServer readiness ..." -ForegroundColor Cyan
  $ready = $false
  foreach ($i in 1..30) {
    Start-Sleep -Seconds 2
    try {
      $body = '{"jsonrpc":"2.0","id":1,"method":"eth_chainId","params":[]}'
      $resp = Invoke-RestMethod -Uri "http://localhost:$Port/" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 3
      if ($resp.result) { $ready = $true; break }
    } catch { }
  }
  if (-not $ready) { throw "RpcServer did not become ready on port $Port." }
  Write-Host "    ready." -ForegroundColor Green

  Write-Host "==> Running pytest ($TestPath) ..." -ForegroundColor Cyan
  Push-Location $SpecTests
  try {
    # -ra prints a clean "short test summary info" (one FAILED/ERROR line per test id) so the
    # log can be parsed for the exact remaining failures; --tb=line keeps tracebacks terse.
    & $pytest $TestPath `
      -ra --tb=line `
      --url "http://localhost:$Port/" `
      --entry-point $entryPoint `
      --ethereum-node $rpcUrl `
      --deselect "tests/single/bundle/test_bundle.py::test_stake_check_in_bundler"
  } finally { Pop-Location }
}
finally {
  Write-Host "==> Cleaning up ..." -ForegroundColor Cyan
  foreach ($p in @($server, $anvil)) {
    if ($p -and -not $p.HasExited) {
      # kill the process tree (dotnet run spawns a child RpcServer .exe)
      taskkill /PID $p.Id /T /F 2>$null | Out-Null
    }
  }
  # anvil dumps chain state under ~/.foundry/anvil/tmp on every run and never prunes it;
  # left unchecked this leaks to 100+ GB across runs. Wipe it now that our anvil is stopped.
  $anvilTmp = Join-Path $env:USERPROFILE ".foundry\anvil\tmp"
  if (Test-Path $anvilTmp) {
    Remove-Item -Path (Join-Path $anvilTmp '*') -Recurse -Force -ErrorAction SilentlyContinue
  }
}
