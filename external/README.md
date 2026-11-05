# External test fixtures

Several test projects load large third-party Ethereum test-vector suites at runtime from
this directory. The suites themselves are **not committed** — each is hundreds of MB to
several GB — so this folder is gitignored except for this README. Obtain each suite at the
pinned version below before running the tests that depend on it.

## Sources

| Subfolder | Source repository | Pinned version | Loaded by |
|---|---|---|---|
| `ethereum-tests/` | https://github.com/ethereum/tests | commit `c67e485ff8b5be9abc8ad15345ec21aa22e290d9` | Trie / ABI / Basic / Blockchain / KeyStore / RLP test runners (`Nethereum.CoreChain.IntegrationTests`, ~12 source files); `GeneralStateTests` unpacked from `fixtures_general_state_tests.tgz` (`Nethereum.EEST.ConformanceRunner`) |
| `legacytests/` | https://github.com/ethereum/legacytests | commit `1f581b8ccdc4c63acf5f2c5c1b155c690c32a8eb` | historical per-fork general-state sweeps (`Nethereum.EVM.UnitTests`, ~4 source files) |
| `eest/<release>/fixtures/` | https://github.com/ethereum/execution-spec-tests (EEST) | one fixtures release per `EestStable` / `EestAmsterdam` / `EestFusakaDevnet5` (see below) | EEST state / blockchain / conformance suites (`Nethereum.EVM.UnitTests`, `Nethereum.EVM.Core.Tests`, `Nethereum.CoreChain.IntegrationTests`, `Nethereum.EEST.ConformanceRunner`) |
| `bls/` | https://github.com/herumi/bls | commit `86c167db926f293f30d6d7f45aea1785622e1462` | BLS12-381 (herumi/bls) — native build / reference source for `Nethereum.Signer.Bls.Herumi` |

## Obtaining the suites

From the repository root:

```bash
cd external

# ethereum/tests
git clone https://github.com/ethereum/tests.git ethereum-tests
git -C ethereum-tests checkout c67e485ff8b5be9abc8ad15345ec21aa22e290d9
tar -xzf ethereum-tests/fixtures_general_state_tests.tgz -C ethereum-tests

# ethereum/legacytests
git clone https://github.com/ethereum/legacytests.git legacytests
git -C legacytests checkout 1f581b8ccdc4c63acf5f2c5c1b155c690c32a8eb

# herumi/bls
git clone https://github.com/herumi/bls.git bls
git -C bls checkout 86c167db926f293f30d6d7f45aea1785622e1462

# execution-spec-tests fixtures (a release download, NOT a git clone)
#   download fixtures_stable.tar.gz from
#   https://github.com/ethereum/execution-spec-tests/releases
#   and extract so the fixtures land at the `fixtures` directory named by the matching
#   EEST key below (e.g. external/eest/stable-v20.0.2/fixtures/)
mkdir -p eest/stable-v20.0.2
tar -xzf fixtures_stable.tar.gz -C eest/stable-v20.0.2
```

You only need the suites for the test areas you intend to run. `dotnet run ci/fetch-externals.cs`
provisions every pinned external listed in `ci/externals.json` (or one group or name, e.g.
`dotnet run ci/fetch-externals.cs -- fixtures`); the manual steps above remain valid for the
suites they cover.

## Where tests look for externals

Every external path, tool and service the tests use is configured in one place:
`tests/Nethereum.XUnitEthereumClients/appsettings.test.json`. Override any value per machine
in `tests/Nethereum.XUnitEthereumClients/appsettings.test.local.json` (gitignored). Relative
paths are resolved from the repository root. Tests read these through
`tests/Shared/TestEnvironment.cs`; a missing external fails with a message naming the key.

`ExternalPaths` (default location):

| Key | Default |
|---|---|
| `EthereumTests` | `external/ethereum-tests` |
| `LegacyTests` | `external/legacytests` |
| `EestStable` | `external/eest/stable-v20.0.2/fixtures` |
| `EestAmsterdam` | `external/eest/glamsterdam-devnet-v8.1.0/fixtures` |
| `EestFusakaDevnet5` | `external/eest/fusaka-devnet-5-v2.1.0/fixtures` |
| `ExecutionApis` | `external/execution-apis` |
| `GoEthereum` | `external/go-ethereum` |
| `StrictBundler` | `external/bundler` |
| `BundlerSpecTests` | `external/bundler-spec-tests` |
| `ZiskOutput` | `zisk/output` |
| `PathKeyed2MDb` | empty (set to a path-keyed 2M RocksDB to run the large-fixture tests) |

`Tools`: `Geth` = `external/bin/geth`, `Geth1_13` = `external/bin/geth-1.13.15`,
`Evm` = `external/bin/evm`, `EvmStateTest` = `external/bin/evm-1.14.12`,
`Devp2p` = `external/bin/devp2p`, `Anvil` = `anvil`, `Node` = `node`, `Ziskemu` = `ziskemu`.

`Services`: `AnvilForkUrl`, `X402AnvilUrl`, `X402ForkRpc`,
`X402RefFacilitator`, `X402RefSignScript`, `MainnetRpcUrl`, `PeerHost`, `PeerRpcHost`,
`PeerRpcPort`, `PeerDevp2pPort`, `PeerDiscv4Port`, `PeerDiscv5Port`, `PeerEnode`,
`GethEnode`, `GethRpcUrl`.

Test classes that need an `ExternalPaths` or `Tools` entry carry the trait
`Requires=Externals`, so a lane without externals can exclude them with
`--filter "Requires!=Externals"`. Opt-in lanes use the `Category` traits `Zisk`, `MainnetFork`,
`BundlerSpec`, `ExternalNode`, `LivePeer`, `LiveNetwork` and `LargeFixture`.
