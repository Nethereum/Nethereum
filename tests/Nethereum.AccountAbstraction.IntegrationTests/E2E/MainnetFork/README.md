# Mainnet-fork test environment for Account Abstraction

These tests are the **external proof tier** for the AA client: everything the client
signs and sends is checked against code this repository did not write — the canonical
EntryPoint deployments on real mainnet state, and the eth-infinitism reference bundler
with strict request validation.

Two tiers, two fixtures:

| Tier | Fixture | Oracle | Tests |
|---|---|---|---|
| 1 | `AnvilMainnetForkFixture` | Canonical EntryPoint v0.8 + v0.9 bytecode on forked mainnet | `CanonicalEntryPointSignatureTests` (hash/sign byte-equality) |
| 2 | `StrictBundlerFixture` (composes Tier 1) | eth-infinitism bundler over real HTTP | `StrictBundlerInteropTests` (estimate/send/receipt/byHash full cycle) |

## Architecture

```
mainnet node (yours, e.g. geth --http)          <- AnvilForkUrl (appsettings.test.local.json)
        |
        v  fork at tip - 10 (lazy state fetch)
anvil :8547  --block-time 1                     <- launched by AnvilMainnetForkFixture
        |
        v  network url in generated config
eth-infinitism bundler :3100 (rpc) / :3101      <- launched by StrictBundlerFixture
        |
        v  bundlerUrl http://localhost:3100/rpc
xunit tests (our client under test)
```

Both fixtures skip (with a reason) when unconfigured, and **throw loudly** when
configured but broken — a misconfigured environment must never look like a pass.

## One-time setup

1. **Foundry** — install from https://getfoundry.sh so `anvil` is on the PATH.
2. **Bundler checkout** — clone https://github.com/eth-infinitism/bundler
   (proven at commit `aae7714`, targets `@account-abstraction/contracts` 0.8.0 =
   EntryPoint v0.8; v0.9 is not supported there yet).
3. **Windows only**: yarn's preprocess scripts use `rm`; point yarn at Git Bash by
   creating `.yarnrc` in the bundler repo root:

   ```
   script-shell "C:\\Program Files\\Git\\bin\\bash.exe"
   ```

4. In the bundler repo: `yarn install && yarn preprocess`.
5. **Apply our patch** (required — see "Why the bundler needs patching" below):

   ```
   git apply <nethereum-repo>/tests/Nethereum.AccountAbstraction.IntegrationTests/E2E/MainnetFork/nethereum-strict-bundler.patch
   ```

6. Configure `tests/Nethereum.XUnitEthereumClients/appsettings.test.local.json`
   (this file is gitignored — node urls never go into the repo):

   ```json
   {
     "EthereumTestSettings": {
       "AnvilForkUrl": "http://<your-mainnet-node>:8545",
       "StrictBundlerPath": "C:\\path\\to\\bundler"
     }
   }
   ```

   An ordinary full node is enough — anvil fetches state lazily at the fork block.
   Rebuild after editing so the file is copied to the test output.

Everything else is automatic: the fixtures fund accounts, write the bundler config
(`packages/bundler/localconfig/bundler.nethereum.config.json`), write the anvil-standard
mnemonic if missing, and poll `eth_supportedEntryPoints` (typed Nethereum client) until
the bundler answers with the expected EntryPoint.

## Why the bundler needs patching (`nethereum-strict-bundler.patch`)

The reference bundler assumes it is talking to a node whose history starts near block 0
(dev chain) or a production node with local log indexes. A **mainnet fork** breaks both
assumptions; we hit these live:

1. **`minLogBlock` config field** (`BundlerConfig.ts`, `MethodHandlerERC4337.ts`):
   `eth_getUserOperationReceipt`/`getUserOperationByHash` scan for `UserOperationEvent`
   with `queryFilter(...)` **from block 0**. Against a fork of mainnet (block ~25.6M)
   anvil forwards the historical range to the remote node, which times out / resets the
   connection (`ECONNRESET`) and receipt polling never completes. The patch adds an
   optional `minLogBlock` and the fixture sets it to the fork block, so event scans
   cover exactly the blocks that can contain our ops.
2. **`decodeRevertReason` hardening** (`decodeRevertReason.ts`): anvil's error payload
   shape differs from geth's; the original `err.error.data` access throws on estimation
   failures and masks the real revert with a TypeError. The patch makes the extraction
   defensive and logs the raw payload instead of crashing.

The patch is additive and behavior-preserving on unforked setups (`minLogBlock` defaults
to the old behavior when absent).

## Bundler flags and their reasons

- `--unsafe`: anvil does not implement geth's native js tracer, which the bundler needs
  for full ERC-7562 opcode-rule tracing. Strict request schema validation and the real
  `simulateValidation`/`simulateHandleOp` calls against the canonical EntryPoint remain
  active — which is exactly what these tests prove. Full 7562 tracing needs a geth-backed
  harness (separate, later).
- `gasFactor 1`, `maxBundleGas 5000000`, `autoBundleInterval 2`: deterministic small-op
  bundling with ~2s inclusion latency on the 1s-block fork.

## Known reference-bundler limitation (affects test design)

`eth_estimateUserOperationGas` computes `callGasLimit` with a plain `eth_estimateGas`
from the EntryPoint **to the sender address** minus 21000 (their source carries
`todo: use simulateHandleOp for this too`). For a **counterfactual (not yet deployed)
account** the sender has no code, so the estimate is just calldata cost (~2.5k): the
account deploys fine in the verification phase, then the execution phase runs out of gas
and `UserOperationEvent.success=false` **with an empty revert reason** (out-of-gas
produces no revert data). This is why `FullSend_ThroughStrictBundler_...` sets an
explicit `Gas` on the deployment op — explicit message values are authoritative in
`AAContractHandler`, the same semantics as a regular transaction send. Production
bundlers estimate deployment ops correctly; our own bundler must too (Batch 2
requirement).

## Logs and troubleshooting

| What | Where |
|---|---|
| anvil output (every tx, revert reasons, RPC errors) | `%TEMP%\anvil-fork-8547.log` |
| bundler output (config echo, patch traces) | `%TEMP%\strict-bundler-3100.log` |

- **Read the anvil log first** when an op fails: the on-chain error (custom error
  selector, `AAxx` code, gas used per tx) is there; the xunit assertion often is not.
- **Empty revert reason + `success=false`** on a deployment op = execution-phase
  out-of-gas; check the `callGasLimit` that was signed (see limitation above).
- **Stale port 8547**: the fixture proves ownership — a freshly forked anvil sits at the
  fork block, so a node with block drift outside 0..200 fails fast with a "stale process
  holding the port" error. Kill leftover `anvil`/`node` processes and rerun.
- **Stale port 3100**: a leftover bundler from an aborted run can answer the readiness
  poll; if behavior looks impossible (wrong fork block in `minLogBlock` echo), check for
  an old `node` process holding 3100.
- **Fork source constraints**: the fork reads state at `tip - 10`; a non-archive source
  node serves roughly the last 128 blocks of state, so do not keep a fork alive for
  hours and expect historical reads to keep working — fixtures fork fresh per run.
- `chainId` is 1 (it is a mainnet fork): fixtures build accounts with the real chain id;
  reusing a `Web3` `Account` bound to another chain id produces signer errors.
