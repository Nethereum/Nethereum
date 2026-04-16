# Nethereum.Zisk.Core

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
- **`ZiskMemoryMap`** — fixed memory-region constants (input offset,
  output offset, heap base) matching the Zisk ELF layout.
- **`ZiskBinaryReader`** / **`ZiskBinaryWriter`** — AOT-safe readers
  / writers over byte spans, replacing `System.IO.BinaryReader` in the
  trimmed guest build.
- **`ZiskCrypto`** — P/Invoke surface for Zisk's witness-backed
  cryptographic primitives (keccak, sha256, ecrecover, …). These
  produce cheaper proof traces than naive managed crypto.
- **`Ripemd160`** — managed implementation used as a fallback when the
  Zisk P/Invoke isn't available.

## Installation

This package is referenced by the Zisk guest binary at build time;
end users typically do not consume it directly. See
[`Nethereum.EVM.Zisk`](../Nethereum.EVM.Zisk/README.md) for the
EVM-on-Zisk bridge.

### Dependencies

None beyond `System.Runtime.InteropServices`. The runtime stays AOT-
and trim-safe — no LINQ, no reflection, no `BinaryFormatter`.

## Build Pipeline

The guest binary is built via `bflat` (RISC-V C# compiler) with a
Zisk-compatible link script. See `scripts/build-evm-zisk.sh` and
`scripts/Dockerfile.zisk-evm` in the Nethereum repo root for the
reference build.

## See Also

- [`Nethereum.EVM.Zisk`](../Nethereum.EVM.Zisk/README.md) — the
  EVM-on-Zisk bridge (binary witness entry point, witness-backed
  precompile backends).
- [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md) — the EVM
  engine source-shared into the Zisk guest.
