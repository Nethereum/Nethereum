# CI & Test Environment — Nethereum 7.1

Nethereum 7.1 makes the test suites runnable the same way on any Docker host. Every external input is pinned in one file and fetched by one tool, tests read their paths from one settings file, and one script runs a lane in a test image. The Ethereum conformance corpora run through one runner on the production execution and import paths. This note is for contributors; nothing here changes a package.

## Pinned Externals and Settings

- `ci/externals.json` pins everything a test needs from outside the repository: the conformance corpora (ethereum/tests, the legacy tests, the EEST stable release and the Amsterdam and Fusaka devnet fixtures), the execution-apis vectors, the reference node and decoder binaries, the ERC-4337 bundler and its spec tests, and the cross-SDK Privacy Pools and x402 reference drivers.
- `dotnet run ci/fetch-externals.cs` fetches them, or one group (`fixtures`, `aa`, `npm` or the reference tools; `--list` names them). `--list` shows what is current and what is missing. An external is fetched again only when its pin changes or its installed output is gone, and worktrees share one externals folder.
- Tests read every external path, tool and service from `tests/Nethereum.XUnitEthereumClients/appsettings.test.json`. A machine overrides them in the gitignored `appsettings.test.local.json`; `NETHEREUM_EXTERNALS` is the only environment variable.

## Test Image and Lanes

- `ci/Dockerfile` builds the `nethereum-ci` image: the .NET 10 SDK with the 9, 8 and 6 runtimes, Go, Node, yarn, Foundry, PowerShell, Python and jemalloc. It runs as the calling user.
- `ci/run-tests.sh <lane>` runs a lane:
  - `build` builds the solution and packs the native packages first, so a fresh checkout restores.
  - `unit` runs everything that needs no external.
  - `full` runs everything that is not opt-in.
  - `Zisk` runs the Zisk emulator and verifier tests.
  - Any other name runs that test category.
- The opt-in categories (`LargeFixture`, `Performance`, `LivePeer`, `LiveNetwork`, `ExternalNode`, `MainnetFork`, `BundlerSpec`, `Zisk` and the diagnostic categories) run only when asked for.
- `ci/areas.txt` splits the suite into the areas `evm`, `eest`, `corechain`, `appchain`, `devp2p`, `aa`, `zk` and `rest`, and `--only` runs one project. The helper logic is a .NET file-based app (`ci/run-tests-tools.cs`), not inline Python.
- The Linux build lane leaves out Unity, MAUI and the .NET Framework 3.5 and 4.8 projects, and builds on case-sensitive file systems.

## Conformance

- The conformance runner owns each corpus: EEST stable (`tests@v20.0.2`, one sweep per fork), the EEST devnet state and blockchain tests, ethereum/tests GeneralStateTests and BlockchainTests, and the legacy Cancun and Constantinople tests. They run through the production state driver and the production RLP import path, with one rejection-rule table for every path.
- A failing case, or a case selected for diagnosis, gets an aligned trace and post-state comparison against a pinned reference client.
- The execution-apis RPC vectors run in sorted order against the in-house chain, from a pinned execution-apis commit.
- A reference-decoder differential runs in CI and compares the peer-message decoding against a pinned reference implementation.
- On the 7.1 release tip the runner passes every case it runs: 72,715 EEST blockchain tests, 61,813 EEST state tests, 38,881 ethereum/tests cases, 111,258 legacy Cancun and 33,212 legacy Constantinople cases, and 6,235 more in the remaining corpora

## Zisk Lane

- The `Zisk` lane builds the `nethereum-zisk` image (`ci/Dockerfile.zisk`), builds the guest ELF in it with `zisk/scripts/build.sh`, and runs the emulator and Solidity-verifier tests of `Nethereum.EVM.Core.Tests`, `Nethereum.EVM.UnitTests`, `Nethereum.CoreChain.IntegrationTests` and `Nethereum.AppChain.Anchoring.IntegrationTests`. The image carries `snarkjs` for the PLONK wrap (`zisk/scripts/wrap-plonk.sh`); proving is not part of the lane.
