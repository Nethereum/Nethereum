# Nethereum.EVM.Core.Tests

Validates the portable `Nethereum.EVM.Core` engine against three execution paths: managed in-process, post-witness-roundtrip in-process, and the RISC-V Zisk guest ELF running in `ziskemu`. The same JSON state-test fixtures and same C# EVM source drive all three, so divergences indicate either a witness encoding bug, a sync-engine bug, or a zkVM bug.

## Test class taxonomy

`ZiskSyncTests` exposes four prefixes. Each runs the same suite of state-test categories (`stExample`, `stArgsZeroOneBalance`, `stSStoreTest`, `stPreCompiledContracts`, etc.) plus the execution-spec-tests precompile fixtures (`eip2537_bls_12_381`, `eip4844_blobs`, `eip7951_p256verify`).

| Prefix | Pipeline | What it isolates |
|---|---|---|
| `DirectSync_*` | JSON → state → sync `Execute` | The portable EVM engine in isolation — no witness, no zkVM. Quick. |
| `WitnessSync_*` | JSON → `WitnessData` → serialise → deserialise → sync `Execute` | Witness format roundtrip + sync engine. Catches encoder/decoder drift. |
| `ZiskEmu_*` | JSON → `WitnessData` → serialise → write `.bin` → `ziskemu -e <ELF>` via WSL | Full zkVM guest path. The ELF must exist and `ziskemu` must be installed. Slow. |
| `GenerateWitnesses_*` | JSON → `WitnessData` → serialise → write `.bin` | Offline witness generation for `cargo-zisk prove`. No execution. |

When `Direct` is green but `WitnessSync` is red, the bug is in `BinaryBlockWitness`. When `WitnessSync` is green but `ZiskEmu` is red, the bug is in the zkVM guest ELF (or its libziskos linkage).

## ELF discovery — fixed path

`ZiskStateTestRunner.FindElfPath()` looks at exactly one location:

```
<repo>/scripts/zisk-output/nethereum_evm_elf
```

It does **not** look in `zisk/output/` (where `zisk/scripts/build.sh` actually writes). After every guest rebuild:

```bash
cp zisk/output/nethereum_evm_elf scripts/zisk-output/nethereum_evm_elf
```

`ZiskEmu_*` tests silently skip (and report "ELF binary not found") if the file is missing.

## Test vector discovery

The runner walks up from the test working directory looking for `Nethereum.slnx` or `Nethereum.sln`, then reads:

- `tests/Nethereum.EVM.UnitTests/Tests/GeneralStateTests/` — Ethereum classic state tests
- `external/execution-spec-tests/fixtures/state_tests/` — EEST precompile coverage (BLS12-381, KZG, P256)

EEST fixtures are passed as absolute paths to `RunCategoryZiskEmu`. If `external/execution-spec-tests/` is missing, those tests will report "Category not found".

## Running tests

```bash
cd tests/Nethereum.EVM.Core.Tests
dotnet build -f net8.0
dotnet test --no-build -f net8.0 --filter "FullyQualifiedName~ZiskEmu_Osaka_Eip7951_P256Verify"
```

### vstest filter operators — easy to get wrong

| Operator | Semantics |
|---|---|
| `~` | Contains substring |
| `=` | Exact match |
| `!~`, `!=` | Negated |

Two pitfalls:

1. **No regex** — `~Foo$` is treated as the literal substring `Foo$`, not "ends with `Foo`". The runner won't error; it'll silently match nothing and the test list is empty.
2. **Substring collisions** — `~ZiskEmu_stPreCompiledContracts` matches **both** `ZiskEmu_stPreCompiledContracts` and `ZiskEmu_stPreCompiledContracts2`. To target only the first, use exact match:
   ```
   --filter "FullyQualifiedName=Nethereum.EVM.Core.Tests.GeneralStateTests.ZiskSyncTests.ZiskEmu_stPreCompiledContracts"
   ```

### Per-test runtime baselines (Zisk v0.18.0 emulator, single ELF process)

| Test | Runtime |
|---|---|
| `ZiskEmu_stPreCompiledContracts` (100 tests) | ~20s |
| `ZiskEmu_Cancun_Eip4844_KzgPointEvaluation` (10) | ~30s |
| `ZiskEmu_Osaka_Eip7951_P256Verify` (20) | ~1m10s |
| `ZiskEmu_Prague_Eip2537_Bls12_381` (50) | ~2m40s |

Running the four tests in one `dotnet test` invocation (single ELF startup amortised across all tests) takes ~5 min total. Running them one at a time can be 4-5× slower because each invocation pays full WSL + ziskemu startup overhead.

## Prerequisites

For `DirectSync_*` and `WitnessSync_*`: just `dotnet build`. Pure C#.

For `ZiskEmu_*`:
- WSL2 with Ubuntu (Windows host) or native Linux
- `~/.zisk/bin/ziskemu` installed (see `zisk/scripts/setup-host.sh`)
- A current guest ELF at `scripts/zisk-output/nethereum_evm_elf` — see `zisk/README.md` for build steps
- `HWLOC_COMPONENTS=-gl` set in WSL for headless / SSH hosts

For `GenerateWitnesses_*`: same as `DirectSync_*`. Outputs land in `scripts/zisk-output/witnesses/`.

## Related

- `zisk/README.md` — full Zisk pipeline architecture, ELF build, proving
- `src/Nethereum.EVM.Zisk/README.md` — guest bridge code (witness reader, precompile backends)
- `tests/Nethereum.EVM.UnitTests/` — main EVM correctness suite (state tests, blockchain tests, mainnet replay)
