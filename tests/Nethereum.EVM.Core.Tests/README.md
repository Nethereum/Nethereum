# Nethereum.EVM.Core.Tests

Validates the portable `Nethereum.EVM.Core` engine against three execution paths: managed in-process, post-witness-roundtrip in-process, and the RISC-V Zisk guest ELF running in `ziskemu`. The same JSON state-test fixtures and same C# EVM source drive all three, so divergences indicate either a witness encoding bug, a sync-engine bug, or a zkVM bug.

## Test class taxonomy

`ZiskSyncTests` exposes four prefixes. Each runs the same suite of state-test categories (`stExample`, `stArgsZeroOneBalance`, `stSStoreTest`, `stPreCompiledContracts`, etc.) plus the execution-spec-tests precompile fixtures (`eip2537_bls_12_381`, `eip4844_blobs`, `eip7951_p256verify`).

| Prefix | Pipeline | What it isolates |
|---|---|---|
| `DirectSync_*` | JSON → state → sync `Execute` | The portable EVM engine in isolation — no witness, no zkVM. Quick. |
| `WitnessSync_*` | JSON → `WitnessData` → serialise → deserialise → sync `Execute` | Witness format roundtrip + sync engine. Catches encoder/decoder drift. |
| `ZiskEmu_*` | JSON → `WitnessData` → serialise → write `.bin` → `ziskemu -e <ELF>` (through WSL on Windows) | Full zkVM guest path. The ELF must exist and `ziskemu` must be installed. Slow. |
| `GenerateWitnesses_*` | JSON → `WitnessData` → serialise → write `.bin` | Offline witness generation for `cargo-zisk prove`. No execution. |

When `Direct` is green but `WitnessSync` is red, the bug is in `BinaryBlockWitness`. When `WitnessSync` is green but `ZiskEmu` is red, the bug is in the zkVM guest ELF (or its libziskos linkage).

## ELF discovery — fixed path

`ZiskSyncStateTestRunner` and `ZiskGuestRunner` read the guest ELF from exactly one location, `ExternalPaths:ZiskOutput` (`zisk/output`) plus `nethereum_evm_elf`:

```
<repo>/zisk/output/nethereum_evm_elf
```

That is where `zisk/scripts/build.sh` writes it. A `ZiskEmu_*` test fails with "Zisk ELF binary not found" when the file is missing, and a category that ran no case fails as well.

## Test vector discovery

The runner reads the fixtures named in `tests/Nethereum.XUnitEthereumClients/appsettings.test.json`:

- `ExternalPaths:EthereumTests` (`external/ethereum-tests`), `GeneralStateTests/` — Ethereum state tests
- `ExternalPaths:EestStable` (`external/eest/stable-v20.0.2/fixtures`), `state_tests/` — EEST precompile coverage (BLS12-381, KZG, P256)

Fetch them with `dotnet run ci/fetch-externals.cs`. A `ZiskEmu_*` test fails with "Category ... not found" when its directory is missing.

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

### Per-test runtime baselines (Zisk 1.3.1-alpha emulator, single ELF process)

| Test | Runtime |
|---|---|
| `ZiskEmu_stPreCompiledContracts` (6 tests) | ~5s |
| `ZiskEmu_stPreCompiledContracts2` (100) | ~1m20s |
| `ZiskEmu_Prague_Eip4844_KzgPointEvaluation` (10) | ~8s |
| `ZiskEmu_Osaka_Eip7951_P256Verify` (20) | ~15s |
| `ZiskEmu_Prague_Eip2537_Bls12_381` (50) | ~35s |

The whole `Category=Zisk` set of this project takes about 2m30s in the `nethereum-zisk` image on 8 CPUs.

`ZiskEmu_Prague_Eip2537_Bls12_381` runs 50 cases: 26 pass and the 24 `invalid` cases stop the guest, because a precompile rejects bad input by throwing and the guest cannot catch.

## Prerequisites

For `DirectSync_*` and `WitnessSync_*`: just `dotnet build`. Pure C#.

For `ZiskEmu_*`:
- WSL2 with Ubuntu (Windows host) or native Linux
- `ziskemu` on the `PATH` or in `~/.zisk/bin` — the `nethereum-zisk` image (`ci/Dockerfile.zisk`) has it
- A current guest ELF at `zisk/output/nethereum_evm_elf` — see `zisk/README.md` for build steps
- `HWLOC_COMPONENTS=-gl` set for headless / SSH hosts

For `GenerateWitnesses_*`: same as `DirectSync_*`. Outputs land in `zisk/output/witnesses/`.

## Related

- `zisk/README.md` — full Zisk pipeline architecture, ELF build, proving
- `src/Nethereum.EVM.Zisk/README.md` — guest bridge code (witness reader, precompile backends)
- `tests/Nethereum.EVM.UnitTests/` — main EVM correctness suite (state tests, blockchain tests, mainnet replay)
