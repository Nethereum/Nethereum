# Nethereum.Zisk.Core

> **PREVIEW** — This package is in preview. APIs may change between releases.

Managed runtime bindings for the [Zisk zkVM](https://0xpolygonhermez.github.io/zisk/).
Used by guest binaries compiled to RISC-V that execute inside a
zero-knowledge virtual machine.

## Overview

Zisk is a RISC-V zkVM. A program's execution trace can be proven
cryptographically — the proof attests that `f(public_input) == public_output`
without re-running the program. Nethereum targets Zisk for stateless
block verification: the guest reads a witness (block + pre-state),
executes the EVM, and emits state-root / block-hash commitments.

`Nethereum.Zisk.Core` is the low-level guest-side runtime that wires
C# onto Zisk's execution environment:

- **`ZiskInput`** — read the serialised input buffer (witness bytes)
  the prover supplies.
- **`ZiskIO`** / **`ZiskLog`** — structured text I/O for debugging the
  guest (stdout lines appear in `ziskemu` runs; redacted in proof
  generation).
- **`ZiskOutput`** — write the public-output slots that become
  commitments. Typical slots: result flag, gas used, block hash,
  state root, transactions root, receipts root.
- **`ZiskMemoryMap`** — fixed memory-region constants (input, ROM, RAM,
  UART and output-register bases) matching the Zisk ELF layout.
- **`ZiskBinaryReader`** / **`ZiskBinaryWriter`** — AOT-safe readers
  / writers over byte spans, replacing `System.IO.BinaryReader` in the
  trimmed guest build.
- **`ZiskCrypto`** — P/Invoke surface for Zisk's `zkvm_*` accelerator
  API (keccak, sha256, ecrecover, modexp, bn254, bls12-381, blake2f,
  kzg, secp256r1, poseidon2). These map to CSR hardware instructions
  inside the zkVM for near-zero proving cost.
- **`ZiskKeccakHashProvider`** / **`ZiskSha256HashProvider`** — `IHashProvider`
  wrappers over the native keccak and sha256 accelerators.
- **`ZiskPoseidonHashProvider`** — `IHashProvider` wrapping the Poseidon2
  accelerator (the native `poseidon2_c` entry point) for Goldilocks-field
  binary trie state roots.
  Width=16, rate=12, capacity=4, XOR-based sponge, 32-byte digest.
- **`Ripemd160`** — managed implementation used as a fallback when the
  Zisk P/Invoke isn't available.

## Installation

This package is referenced by the Zisk guest binary at build time;
end users typically do not consume it directly. See
[`Nethereum.EVM.Zisk`](../Nethereum.EVM.Zisk/README.md) for the
EVM-on-Zisk bridge.

### Dependencies

- A project reference to `Nethereum.Util` — the hash providers
  (`ZiskKeccakHashProvider`, `ZiskSha256HashProvider`,
  `ZiskPoseidonHashProvider`) implement its `IHashProvider` interface
  (`Nethereum.Util.HashProviders`).
- `System.Runtime.InteropServices` (BCL) for the `[DllImport]`
  accelerator bindings.

The Zisk.Core code itself uses no LINQ, reflection, or `BinaryFormatter`,
keeping the guest side AOT- and trim-friendly.

## Build Pipeline

The guest binary is built with `bflat`/NativeAOT cross-compiled to
RISC-V (via the `nethereum/bflat-riscv64` Docker image) and linked
against `libziskos.a`. The build orchestration lives in
[`zisk/`](https://github.com/Nethereum/Nethereum/blob/master/zisk/README.md) at the repo root:

```bash
bash zisk/scripts/setup-host.sh   # first-time setup (image + Zisk host tools)
bash zisk/scripts/build.sh        # source mode
bash zisk/scripts/build.sh --dll  # DLL mode
```

See [`zisk/README.md`](https://github.com/Nethereum/Nethereum/blob/master/zisk/README.md) for the full build,
proving, and verification flow.

## See Also

- [`Nethereum.EVM.Zisk`](../Nethereum.EVM.Zisk/README.md) — the
  EVM-on-Zisk bridge (binary witness entry point, witness-backed
  precompile backends).
- [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md) — the EVM
  engine source-shared into the Zisk guest.
