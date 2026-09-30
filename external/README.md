# External test fixtures

Several test projects load large third-party Ethereum test-vector suites at runtime from
this directory. The suites themselves are **not committed** — each is hundreds of MB to
several GB — so this folder is gitignored except for this README. Obtain each suite at the
pinned version below before running the tests that depend on it.

## Sources

| Subfolder | Source repository | Pinned version | Loaded by |
|---|---|---|---|
| `ethereum-tests/` | https://github.com/ethereum/tests | commit `c67e485ff8b5be9abc8ad15345ec21aa22e290d9` | Trie / ABI / Basic / Blockchain / KeyStore / RLP test runners (`Nethereum.CoreChain.IntegrationTests`, ~12 source files) |
| `legacytests/` | https://github.com/ethereum/legacytests | commit `1f581b8ccdc4c63acf5f2c5c1b155c690c32a8eb` | historical per-fork general-state sweeps (`Nethereum.EVM.UnitTests`, ~4 source files) |
| `execution-spec-tests/` | https://github.com/ethereum/execution-spec-tests (EEST) | `fixtures_develop` fixtures release, extracted under `execution-spec-tests/fixtures/` | EEST blob / BLS spec theories (`Nethereum.EVM.UnitTests`, ~2 source files) |
| `bls/` | https://github.com/herumi/bls | commit `86c167db926f293f30d6d7f45aea1785622e1462` | BLS12-381 (herumi/bls) — native build / reference source for `Nethereum.Signer.Bls.Herumi` |

## Obtaining the suites

From the repository root:

```bash
cd external

# ethereum/tests
git clone https://github.com/ethereum/tests.git ethereum-tests
git -C ethereum-tests checkout c67e485ff8b5be9abc8ad15345ec21aa22e290d9

# ethereum/legacytests
git clone https://github.com/ethereum/legacytests.git legacytests
git -C legacytests checkout 1f581b8ccdc4c63acf5f2c5c1b155c690c32a8eb

# herumi/bls
git clone https://github.com/herumi/bls.git bls
git -C bls checkout 86c167db926f293f30d6d7f45aea1785622e1462

# execution-spec-tests fixtures (a release download, NOT a git clone)
#   download fixtures_develop.tar.gz from
#   https://github.com/ethereum/execution-spec-tests/releases
#   and extract so the fixtures land at external/execution-spec-tests/fixtures/
mkdir -p execution-spec-tests
tar -xzf fixtures_develop.tar.gz -C execution-spec-tests
```

You only need the suites for the test areas you intend to run. The test runners resolve
these paths relative to the repository root (e.g. `external/ethereum-tests/BlockchainTests`),
so keep the subfolder names exactly as above.
