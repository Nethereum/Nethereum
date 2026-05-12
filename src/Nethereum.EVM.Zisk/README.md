# Nethereum.EVM.Zisk

Bridge between [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md)
and the [Zisk zkVM](https://0xpolygonhermez.github.io/zisk/). Compiled
as the guest ELF, it reads a witness from Zisk's input channel,
executes the EVM, and writes state-root / block-hash commitments to
Zisk's output channel.

## Overview

The guest pipeline:

1. **Input** — `ZiskInput.Read()` returns the witness byte stream
   (serialised `BinaryBlockWitness` v1).
2. **Deserialise** — `BinaryBlockWitness.Deserialize(bytes)` produces
   a `BlockWitnessData` whose `Features.Fork` drives registry lookup.
3. **Registry** — `MainnetHardforkRegistry.Build(ZiskPrecompileBackends.Instance)`
   builds a fork registry wired with witness-backed crypto (see
   Backends section). Osaka is omitted unless a P256Verify backend is
   supplied — today it is not, so Osaka runs with Prague-level
   precompiles (all forks Frontier → Osaka available).
4. **Execute** — `BlockExecutor.Execute` (the `#if EVM_SYNC` sync
   build) walks transactions, computes state roots via
   `PatriciaStateRootCalculator` or `BinaryStateRootCalculator`
   (selected by witness `Features.StateTree`), and populates
   receipts / block commitments.
5. **Output** — `ZiskIO.SetOutput(slot, value)` emits: result flag,
   cumulative gas (2 × 32-bit), block hash (8 slots), state root
   (8 slots), transactions root (8 slots), receipts root (8 slots),
   transaction count. `WriteHex` / `WriteLong` helpers log human-
   readable diagnostics to the emulator stream.

## Installation

This package is referenced by the Zisk guest binary at build time.
The build produces a RISC-V ELF consumed by `ziskemu` (emulator) or
`cargo-zisk prove` (proof generator).

### Dependencies

- [`Nethereum.Zisk.Core`](../Nethereum.Zisk.Core/README.md) — Zisk
  runtime bindings (IO, crypto P/Invokes, memory map).
- [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md) — EVM engine
  source-shared into the guest.

## Backends

`ZiskPrecompileBackends.Instance` is a singleton `PrecompileBackends`
bundle whose fields route to witness-backed native Zisk operations:

| Backend | Routes to | CSRs |
|---------|----------|------|
| `ZiskEcRecoverBackend` | `zkvm_secp256k1_ecrecover` → keccak address derivation | secp256k1_add/dbl |
| `ZiskSha256Backend` | `zkvm_sha256` | sha256_f (0x805) |
| `ZiskRipemd160Backend` | Managed `Ripemd160` (no native CSR) | — |
| `ZiskModExpBackend` | `zkvm_modexp` | arith256_mod |
| `ZiskBn128Backend` | `zkvm_bn254_g1_add`, `zkvm_bn254_g1_mul`, `bn254_pairing_check_c` | bn254_curve_add/dbl, bn254_complex_* |
| `ZiskBlake2fBackend` | `zkvm_blake2f` | blake2b_round (0x819) |
| `ZiskP256VerifyBackend` | `zkvm_secp256r1_verify` | secp256r1_add/dbl |
| `ZiskBls12381Operations` | `zkvm_bls12_g1_add/msm`, `zkvm_bls12_g2_add/msm`, `bls12_381_pairing_check_c`, `zkvm_bls12_map_fp_to_g1/fp2_to_g2` | bls12_381_curve_add/dbl, bls12_381_complex_* |
| `ZiskKzgOperations` | `zkvm_kzg_point_eval`, `zkvm_sha256` (versioned hash) | BLS12-381 CSRs + sha256_f |

BN254 pairing and BLS12-381 pairing use direct `_check_c` exports
instead of the `zkvm_*` wrappers, which have an allocator bug in
libziskos 0.17.1 (`Vec::with_capacity` crashes in the Zisk memory model).

## State Tree Selection

The witness `BlockFeatureConfig.StateTree` field selects the trie:

| StateTree | HashFunction | Calculator |
|-----------|-------------|-----------|
| Patricia (default) | Keccak | `PatriciaStateRootCalculator` |
| Binary (EIP-7864) | Blake3 | `BinaryStateRootCalculator(Blake3HashProvider)` |
| Binary | Poseidon | `BinaryStateRootCalculator(ZiskPoseidonHashProvider)` |
| Binary | Sha256 | `BinaryStateRootCalculator(ZiskSha256HashProvider)` |

Poseidon uses `ZiskPoseidonHashProvider` — Poseidon2 over Goldilocks
field via CSR 0x812 (width=16, rate=12, x^7 S-box). The managed
equivalent is `GoldilocksPoseidon2HashProvider` in Nethereum.Util,
validated to produce identical state roots.

## Guest entry point

`ZiskBinaryWitness.Main` is the entry point the Zisk ELF links
against. It reads the witness, validates the version byte (must equal
`BinaryBlockWitness.VERSION` = 1), ensures `Features.Fork` is set
(rejects `HardforkName.Unspecified` with a loud output), executes the
block, and writes the output slots.

## Build Pipeline

The build orchestration lives in [`zisk/`](../../zisk/README.md) at
the repo root. It uses the `nethereum/bflat-riscv64` Docker image
built from the [Nethereum/bflat-riscv64 fork on the `nethereum`
branch](https://github.com/Nethereum/bflat-riscv64/tree/nethereum).

Two modes:

- **Source mode (default)** — `bflat` compiles all linked `.cs` files
  directly from the EVM.Core, Zisk.Core, and EVM.Zisk source trees.
  Smaller ELF; use for production proofs.
- **DLL mode** (`--dll`) — `dotnet build` produces the managed DLL
  first and `bflat` links against it. ~25% larger ELF but faster
  iteration when only the guest entry changes.

First-time setup (clones the fork, builds the image, installs Zisk on
the host):

```bash
bash zisk/scripts/setup-host.sh
```

Build:

```bash
bash zisk/scripts/build.sh           # source mode
bash zisk/scripts/build.sh --dll     # DLL mode
```

Output lands in `zisk/output/nethereum_evm_elf`. Run it:

```bash
ziskemu -e zisk/output/nethereum_evm_elf \
    --legacy-inputs zisk/output/witnesses/my_witness.bin \
    -n 500000000
```

See [`zisk/README.md`](../../zisk/README.md) for proving flow,
verification checklist, and environment overrides (`ZISK_IMAGE`,
`LIBZISKOS_DIR`).

## See Also

- [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md) — EVM engine
  and `BinaryBlockWitness` format.
- [`Nethereum.Zisk.Core`](../Nethereum.Zisk.Core/README.md) — Zisk
  runtime bindings.
- [`Nethereum.EVM.Precompiles`](../Nethereum.EVM.Precompiles/README.md)
  — the non-zkVM backend bundle this mirrors in shape.
