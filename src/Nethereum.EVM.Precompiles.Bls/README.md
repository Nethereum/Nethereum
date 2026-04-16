# Nethereum.EVM.Precompiles.Bls

EIP-2537 BLS12-381 precompile handlers for
[`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md). Activated at
the Prague hardfork.

## Overview

EIP-2537 adds seven BLS12-381 precompiles at addresses `0x0b`…`0x11`:

| Address | Precompile |
|---------|-----------|
| `0x0b` | G1 addition |
| `0x0c` | G1 multi-scalar multiplication |
| `0x0d` | G2 addition |
| `0x0e` | G2 multi-scalar multiplication |
| `0x0f` | Pairing check |
| `0x10` | `map_fp_to_g1` |
| `0x11` | `map_fp2_to_g2` |

This package plugs those handlers into a
`Nethereum.EVM.Core.HardforkConfig` by extension method, calling into
a pluggable `IBls12381Operations` implementation (managed, native, or
zkVM-backed).

## Installation

```bash
dotnet add package Nethereum.EVM.Precompiles.Bls
```

### Dependencies

- [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md) — `HardforkConfig`, `PrecompileRegistry`.
- `Nethereum.Signer.Bls` — `IBls12381Operations` contract and managed backend.

## Usage

### `HardforkConfig.WithBlsBackend(IBls12381Operations)`

```csharp
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;      // DefaultHardforkConfigs
using Nethereum.Signer.Bls;           // IBls12381Operations, Bls12381Operations

IBls12381Operations blsOps = new Bls12381Operations();
HardforkConfig prague = DefaultHardforkConfigs.Prague
    .WithBlsBackend(blsOps);
```

The extension wraps `prague.Precompiles` with BLS handlers, returning
a new `HardforkConfig` instance. The original is unchanged.

### `PrecompileRegistry.WithBlsBackend(...)`

Compose BLS on top of any `PrecompileRegistry` returned from
`PrecompileRegistries.PragueBase(...)` or a custom composition:

```csharp
using Nethereum.EVM.Execution.Precompiles;

var baseRegistry   = PrecompileRegistries.PragueBase(/* backends... */);
var withBls        = baseRegistry.WithBlsBackend(blsOps);
```

## Requirement Note

`WithBlsBackend` throws `InvalidOperationException` when called against
a `HardforkConfig` whose `Precompiles` registry is null — BLS handlers
must layer on top of a base registry (e.g. Prague's). Obtain the
`HardforkConfig` through `MainnetHardforkRegistry.Build(backends).Get(HardforkName.Prague)`
or `DefaultHardforkConfigs.Prague` before calling the extension.

## See Also

- [`Nethereum.EVM.Precompiles`](../Nethereum.EVM.Precompiles/README.md)
  — the default backend bundle this layers on top of.
- [`Nethereum.EVM.Precompiles.Kzg`](../Nethereum.EVM.Precompiles.Kzg/README.md)
  — the sibling EIP-4844 KZG precompile.
- [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md) — `HardforkConfig` / `PrecompileRegistry` contracts.
