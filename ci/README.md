# Running the test suites

Everything a test needs from outside the repository is pinned in `ci/externals.json` and
configured in `tests/Nethereum.XUnitEthereumClients/appsettings.test.json` (see
[external/README.md](../external/README.md) for every key). The optional override is the environment variable
`NETHEREUM_EXTERNALS` (below); override any other value for your machine in the gitignored
`tests/Nethereum.XUnitEthereumClients/appsettings.test.local.json`.

## In the CI image (any Docker host)

```bash
docker build -t nethereum-ci ci/
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/src" nethereum-ci dotnet run ci/fetch-externals.cs
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/src" nethereum-ci ci/run-tests.sh unit
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/src" nethereum-ci ci/run-tests.sh full
```

The image carries the .NET 10 SDK with the 9, 8 and 6 runtimes, Go, Node, yarn, Foundry,
PowerShell, Python and jemalloc.

## Forgejo runner

`.forgejo/workflows/ci.yml` runs on a runner labelled `nethereum-ci` in host mode: every step is a
shell command that drives Docker, so the host needs only git and Docker 25 or later. Externals and
NuGet packages are cached in `$HOME/ci-cache`, and each run's logs and summaries are kept in
`$HOME/ci-results/<run number>`. A push runs the `build` lane. The nightly schedule runs `full` and a
manual dispatch runs the lane it is given, each as one job per area of `ci/areas.txt`, so every area
reports on its own and stays inside the server's per-task time limit. The jobs run one after another
and each removes any CI container a stopped job left running, so the runner's capacity stays 1.

```bash
forgejo-runner generate-config > config.yml   # set runner.labels to ["nethereum-ci:host"], capacity 1, timeout 12h
forgejo-runner register --no-interactive --config config.yml --instance <forgejo url>   --name <name> --labels nethereum-ci:host --token <registration token>
forgejo-runner daemon --config config.yml
```

## The Zisk lane

The `zisk` job of the Forgejo workflow (dispatch lane `Zisk`) builds the `nethereum-zisk` image, builds the guest ELF inside it with `zisk/scripts/build.sh`, and runs `ci/run-tests.sh Zisk` for `Nethereum.EVM.Core.Tests`, `Nethereum.EVM.UnitTests`, `Nethereum.CoreChain.IntegrationTests` and `Nethereum.AppChain.Anchoring.IntegrationTests`. The lane leaves out the `LiveNetwork` tests. The image is Ubuntu 24.04; it carries the .NET SDK, the bflat toolchain from the `zisk-1.3.1` branch of Nethereum/bflat-riscv64 (built once into `nethereum/bflat-riscv64:zisk-1.3.1`), `libziskos.a`, `ziskemu` and `cargo-zisk`.

```bash
docker build -f ci/Dockerfile.zisk -t nethereum-zisk zisk/scripts
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/src" nethereum-zisk bash zisk/scripts/build.sh
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/src" nethereum-zisk dotnet run ci/fetch-externals.cs
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/src" nethereum-zisk ci/run-tests.sh Zisk --only Nethereum.EVM.Core.Tests
```

Proving is not part of the lane. With the proving key mounted (`-v ~/.zisk/provingKey:/opt/zisk/provingKey`) the same image runs `cargo-zisk prove`, and `--gpus all` adds the GPU when the host has the NVIDIA container toolkit; see [zisk/README.md](../zisk/README.md).

## On your own machine

Install the same tools, then:

```bash
dotnet run ci/fetch-externals.cs                #  everything
dotnet run ci/fetch-externals.cs -- fixtures   # one group: fixtures, geth-tools, aa, npm
dotnet run ci/fetch-externals.cs -- --list     # what is current and what is missing
```

Each external is fetched once and re-fetched only when its pin in `ci/externals.json`
changes (or with `--force`).

In a git worktree, `fetch-externals.cs` fetches into the main checkout's `external/` folder and the tests
resolve every `external/...` path in `appsettings.test.json` against the worktree's own `external/` if it
has the path and the main checkout's otherwise, so every worktree shares one copy of the pinned externals.
`NETHEREUM_EXTERNALS` overrides both with one explicit folder. Then run one project the way CI does:

```bash
dotnet run ci/fetch-externals.cs -- fixtures
ci/run-tests.sh full --only Nethereum.EVM.UnitTests
```
## Lanes

`ci/run-tests.sh <lane>` runs every test project in `Nethereum.slnx`, each target
framework, except the projects listed in `ci/skip-projects.txt`; the `build` lane builds
the solution instead. `--area <area>` narrows a lane to one area of `ci/areas.txt` (`evm`, `eest`,
`corechain`, `appchain`, `devp2p`, `aa`, `zk`, or `rest` for every project no other area
matches), and `--only <project>` to named projects.

| Lane | Runs |
|---|---|
| `build` | builds every project in `Nethereum.slnx` except those in `ci/skip-build.txt`: the MAUI projects (iOS / Mac Catalyst targets need macOS), Unity (needs the Unity Editor assemblies) and the .NET Framework 3.5 / 4.8 projects (Windows targeting packs), plus any project that references one of them |
| `unit` | tests that need no fetched external and no opt-in category |
| `full` | everything except the opt-in categories |
| `<Category>` | only tests with `[Trait("Category", "<Category>")]` |

Opt-in categories are tests that need something the lanes cannot provision:

| Category | Needs |
|---|---|
| `Zisk` | ziskemu / cargo-zisk and the guest ELF: the `nethereum-zisk` image (`ci/Dockerfile.zisk`), WSL on Windows |
| `MainnetFork` | mainnet RPC: `Services:MainnetRpcUrl` for the fork-proof tests, `Services:AnvilForkUrl` for the account abstraction anvil fork and strict bundler interop |
| `BundlerSpec` | the eth-infinitism bundler-spec-tests run against our bundler |
| `LivePeer` | a reachable devp2p peer (`Services:Peer*`) |
| `LiveNetwork` | live external services: the x402 reference facilitator, fork RPCs, `Services:MainnetRpcUrl` (Safe encoding), Etherscan / Sourcify / 4byte / Chainlist (DataServices) and the Circles RPC |
| `LargeFixture` | a path-keyed 2M-account RocksDB (`ExternalPaths:PathKeyed2MDb`) |
| `Performance` | nothing: wall-clock ratio and time-ceiling assertions that fail under I/O contention; run on a quiet machine by category |
| `ExternalNode` | a separately started node: PrivacyPools against geth, and the managed-account tests, which need a node that still serves `personal_*` (geth removed it in 1.14) |
| `LegacyFork-Debug`, `LegacyFork-DebugSubtest`, `MainnetBlockCell-Trace`, `GasDiagnostic` | nothing: single-case debug and diagnostic dumps, run locally by category |

Results land in `.testresults/<lane>/`: one log and trx per project and framework, and
`summary.txt`. The previous run's summary is kept as `summary.previous.txt`, and the
runner prints every project whose result changed. The script exits non-zero when any
project fails, times out (one hour per project) or does not build.
