# Nethereum.EVM.Precompiles

Default crypto backend bundle and pre-built mainnet hardfork registry
for [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md).

## Overview

`Nethereum.EVM.Core` defines a `HardforkRegistry` plus a
`PrecompileBackends` POCO that holds the concrete crypto
implementations behind Ethereum's precompiled contracts
(`0x01`…`0x0a`, BLS12-381 at `0x0b`…`0x11`, P256Verify at `0x0100`).

`Nethereum.EVM.Precompiles` ships:

- **Default backends** — managed .NET implementations of every
  mainnet precompile's crypto primitive.
- **`DefaultPrecompileBackends.Instance`** — a singleton bundle you
  feed into `MainnetHardforkRegistry.Build(backends)` to get a
  ready-to-use mainnet registry.
- **`DefaultMainnetHardforkRegistry.Instance`** — the pre-built
  singleton (`MainnetHardforkRegistry.Build(DefaultPrecompileBackends.Instance)`).
  Use this when you don't need to customise the crypto backends.
- **`DefaultHardforkConfigs`** — per-fork `HardforkConfig` accessors
  (Cancun / Prague / Osaka / …) wired with the default registry.
  Convenient for unit tests that target a single fork.
- **`DefaultPrecompileRegistries`** — standalone fork-scoped precompile
  registry factories (`FrontierBase`, `ByzantiumBase`, `CancunBase`, etc.)
  for hand-composing a registry for a single fork.

Ship this package when you run an EVM on a standard .NET host. For
zkVM guest builds (Zisk), use
[`Nethereum.EVM.Zisk`](../Nethereum.EVM.Zisk/README.md)'s
`ZiskPrecompileBackends` instead — same `PrecompileBackends` POCO
shape, but every crypto call routes through witness-backed ZiskCrypto
P/Invokes.

## Installation

```bash
dotnet add package Nethereum.EVM.Precompiles
```

### Dependencies

- [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md) — `PrecompileBackends`, `HardforkRegistry`, `HardforkConfig`, `PrecompileRegistry`.
- [`Nethereum.Signer`](../Nethereum.Signer/README.md) — secp256k1 recover for the EcRecover backend and `BN128Curve` for the BN128 backend.

(`Nethereum.Util` and BouncyCastle arrive transitively; they are not direct references.)

For BLS12-381 (EIP-2537) and KZG (EIP-4844) add the satellite packages:
- [`Nethereum.EVM.Precompiles.Bls`](../Nethereum.EVM.Precompiles.Bls/README.md)
- [`Nethereum.EVM.Precompiles.Kzg`](../Nethereum.EVM.Precompiles.Kzg/README.md)

## Quick Start

```csharp
using Nethereum.EVM;                // HardforkName
using Nethereum.EVM.Precompiles;

// Ready-to-use mainnet registry with default crypto backends
var registry = DefaultMainnetHardforkRegistry.Instance;

// Resolve a HardforkConfig for a given fork
var cancun = registry.Get(HardforkName.Cancun);
```

## Key Backends

| Backend | Precompile address | Wraps |
|---------|-------------------|-------|
| `DefaultEcRecoverBackend` | `0x01` | `Nethereum.Signer` secp256k1 recovery |
| `DefaultSha256Backend` | `0x02` | `System.Security.Cryptography.SHA256` |
| `DefaultRipemd160Backend` | `0x03` | BouncyCastle `RipeMD160Digest` (in this package) |
| `DefaultModExpBackend` | `0x05` | EIP-2565 modular exponentiation |
| `DefaultBn128Backend` | `0x06`, `0x07`, `0x08` | BN128 add / mul / pairing (`Nethereum.Signer.Crypto.BN128Curve`) |
| `DefaultBlake2fBackend` | `0x09` | EIP-152 Blake2 F compression |
| `DefaultP256VerifyBackend` | `0x0100` | EIP-7951 secp256r1 verification via BouncyCastle `ECDsaSigner` over NIST P-256 |

Swap any backend by constructing your own `PrecompileBackends`:

```csharp
using Nethereum.EVM;                        // MainnetHardforkRegistry
using Nethereum.EVM.Execution.Precompiles;  // PrecompileBackends
using Nethereum.EVM.Precompiles.Backends;   // Default*Backend

var customBackends = new PrecompileBackends(
    DefaultEcRecoverBackend.Instance,
    myCustomSha256Backend,   // e.g. hardware-accelerated
    DefaultRipemd160Backend.Instance,
    DefaultModExpBackend.Instance,
    DefaultBn128Backend.Instance,
    DefaultBlake2fBackend.Instance,
    DefaultP256VerifyBackend.Instance);

var registry = MainnetHardforkRegistry.Build(customBackends);
```

## `DefaultHardforkConfigs` for Targeted Tests

```csharp
using Nethereum.EVM;                // TransactionExecutor
using Nethereum.EVM.Precompiles;    // DefaultHardforkConfigs

// Single-fork configs with default backends, handy for unit tests:
var prague = DefaultHardforkConfigs.Prague;
var cancun = DefaultHardforkConfigs.Cancun;
var osaka  = DefaultHardforkConfigs.Osaka;

var executor = new TransactionExecutor(prague);
```

## See Also

- [`Nethereum.EVM.Core`](../Nethereum.EVM.Core/README.md) — hardfork
  registry machinery and `PrecompileBackends` contract.
- [`Nethereum.EVM.Precompiles.Bls`](../Nethereum.EVM.Precompiles.Bls/README.md)
  — EIP-2537 BLS12-381 precompiles.
- [`Nethereum.EVM.Precompiles.Kzg`](../Nethereum.EVM.Precompiles.Kzg/README.md)
  — EIP-4844 KZG point-evaluation precompile.
- [`Nethereum.EVM.Zisk`](../Nethereum.EVM.Zisk/README.md) — Zisk
  zkVM-backed alternative to these defaults.
