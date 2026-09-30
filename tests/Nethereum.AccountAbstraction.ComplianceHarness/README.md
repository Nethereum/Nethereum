# AA Bundler Compliance Harness (eth-infinitism bundler-spec-tests)

Runs the **eth-infinitism `bundler-spec-tests`** conformance suite — the reference
ERC-4337 / ERC-7562 test vectors — against the Nethereum bundler `RpcServer`.

## Un-missable gate

This suite is also wired as an xUnit test, `BundlerSpecComplianceGateTests`
(`[SkippableFact]`, `Category=Compliance`) in `Nethereum.AccountAbstraction.IntegrationTests`,
so `dotnet test` surfaces it instead of only running manually. Set `BUNDLER_SPEC_TESTS` to a
prepared clone (see Prerequisites below) and run:

```
dotnet test --filter Category=Compliance
```

Without `BUNDLER_SPEC_TESTS` set, or with `anvil`/`pwsh`/the venv missing, the test SKIPS
loudly with the exact missing prerequisite rather than passing silently. CI should run this
filter as a dedicated step. This gate MUST pass before any bundler change is called done.

## What the harness does

`run-spec-tests.ps1` orchestrates the full run end to end, and always tears down what it starts:

1. starts `anvil` (deterministic dev chain, chainId 1337 (the eth-infinitism eip7702 tests require 1337));
2. deploys our v0.9 `EntryPoint` + `SimpleAccountFactory` via the `DeployContracts` console tool
   (it prints `ENTRYPOINT=`/`FACTORY=` lines the script parses);
3. starts the Nethereum `RpcServer` pointed at anvil, serving JSON-RPC at the **root path `/`**;
4. runs `pytest` from a local `bundler-spec-tests` clone against `http://localhost:3000/`;
5. kills the anvil + RpcServer process trees.

## Prerequisites (one-time)

1. **.NET SDK 10** (the RpcServer and tool target `net10.0`).
2. **Foundry / anvil** on `PATH` (`anvil --version`).
3. **A clone of `bundler-spec-tests`** with its Python venv and the OpenRPC schema built:
   ```
   git clone https://github.com/eth-infinitism/bundler-spec-tests
   cd bundler-spec-tests
   python -m venv .venv
   .venv\Scripts\pip install -r requirements.txt          # or: pdm install, per that repo's README
   cd spec && yarn install && yarn build && cd ..          # builds spec/openrpc.json (rpc/ schema tests need it)
   git submodule update --init --recursive                 # pulls the @account-abstraction contracts
   ```

## Running

From this folder:

```powershell
./run-spec-tests.ps1 -SpecTests C:\path\to\bundler-spec-tests
```

Useful options (see the script header for all):

- `-TestPath tests/single/bundle/test_storage_rules.py` — run one module instead of the whole suite.
- `-EnableErc7562 $false` — run with the opcode/storage engine off (the RPC/mempool baseline).
- `-MinUnstakeDelay 86400 -MinStake 1000000000000000000` — use production thresholds instead of the suite's.

## RpcServer flags the harness uses

| Flag | Reason |
|---|---|
| `--enableErc7562=true` | turns on the opcode/storage validation engine (off by default in the RpcServer) |
| `--minUnstakeDelay=0 --minStake=0` | the suite stakes entities with a 2-second delay for test speed (`staked_contract()` in its `tests/utils.py`); the production defaults are 1 ETH / 1 day, so staked-only rules only pass when these are lowered |
| `--maxVerificationGas=10000000` | above the suite's highest `verificationGasLimit` so legitimately-expensive ops aren't rejected |
| `--unsafe=true` | matches how the reference bundler is run against the suite |

## Files

- `DeployContracts/` — console tool that deploys our v0.9 EntryPoint + SimpleAccountFactory and prints their addresses.
- `run-spec-tests.ps1` — the orchestration script.
