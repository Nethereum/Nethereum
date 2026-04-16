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
   supplied — today it is not, so the guest supports Frontier → Prague.
4. **Execute** — `BlockExecutor.Execute` (the `#if EVM_SYNC` sync
   build) walks transactions, computes state roots via
   `PatriciaStateRootCalculator`, and populates receipts / block
   commitments.
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

| Backend | Routes to |
|---------|----------|
| `ZiskEcRecoverBackend` | `ZiskCrypto.secp256k1_recover` |
| `ZiskSha256Backend` | `ZiskCrypto.sha256` |
| `ZiskRipemd160Backend` | `ZiskCrypto.ripemd160` (fallback: managed `Nethereum.Zisk.Core.Ripemd160`) |
| `ZiskModExpBackend` | `ZiskCrypto.modexp` |
| `ZiskBn128Backend` | `ZiskCrypto.bn128_*` |
| `ZiskBlake2fBackend` | `ZiskCrypto.blake2f_compress` |

P256Verify is not yet wired (the Zisk runtime doesn't yet ship a
P/Invoke binding). `MainnetHardforkRegistry.Build` detects a `null`
`P256Verify` backend and omits the Osaka fork registration, leaving
Frontier → Prague available to the guest.

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
