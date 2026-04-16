# Nethereum.EVM.Precompiles.Kzg

EIP-4844 KZG point-evaluation precompile for
[`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md). Activated at
the Cancun hardfork.

## Overview

EIP-4844 (proto-danksharding) introduced a single new precompile at
address `0x0a`: `point_evaluation_precompile`. It verifies a KZG
opening proof against a given commitment / evaluation point / claimed
value using the trusted KZG setup.

This package plugs the handler into a `Nethereum.EVM.Core.HardforkConfig`
by extension method, delegating the cryptographic work to a pluggable
`IKzgOperations` implementation (native C-KZG by default, custom for
zkVM / alternative backends).

## Installation

```bash
dotnet add package Nethereum.EVM.Precompiles.Kzg
```

### Dependencies

- [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md) — `HardforkConfig`, `PrecompileRegistry`.
- The project ships `CkzgOperations` (wraps native C-KZG) as the
  default implementation of `IKzgOperations`.

## Usage

### `HardforkConfig.WithKzgBackend()`

Default (uses the embedded trusted setup + native C-KZG):

```csharp
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;      // DefaultHardforkConfigs

HardforkConfig cancun = DefaultHardforkConfigs.Cancun
    .WithKzgBackend();
```

### `HardforkConfig.WithKzgBackend(IKzgOperations)`

Inject a custom operations implementation (test double, alternative
backend, trimmed build, …):

```csharp
using Nethereum.EVM.Precompiles.Kzg;

IKzgOperations kzgOps = new CkzgOperations();      // or your own
HardforkConfig cancun = DefaultHardforkConfigs.Cancun
    .WithKzgBackend(kzgOps);
```

### `PrecompileRegistry.WithKzgBackend(...)`

Compose KZG on top of any base registry:

```csharp
using Nethereum.EVM.Execution.Precompiles;

var cancunBase = PrecompileRegistries.CancunBase(/* backends... */);
var withKzg    = cancunBase.WithKzgBackend(kzgOps);
```

## Requirement Note

`WithKzgBackend` throws `InvalidOperationException` when called against
a `HardforkConfig` whose `Precompiles` registry is null — the KZG
handler layers on top of the Cancun base registry. Obtain the
`HardforkConfig` through `MainnetHardforkRegistry.Build(backends).Get(HardforkName.Cancun)`
or `DefaultHardforkConfigs.Cancun` before calling the extension.

## See Also

- [`Nethereum.EVM.Precompiles`](../Nethereum.EVM.Precompiles/README.md)
  — default backend bundle and registry singleton.
- [`Nethereum.EVM.Precompiles.Bls`](../Nethereum.EVM.Precompiles.Bls/README.md)
  — sibling EIP-2537 BLS12-381 precompiles.
- [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md) — `HardforkConfig` / `PrecompileRegistry` contracts.
