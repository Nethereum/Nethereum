# Nethereum.Signer.Bls.Herumi

Native Herumi BLS implementation for Ethereum consensus layer (Beacon Chain) signature verification.

## Overview

Nethereum.Signer.Bls.Herumi provides a **production-ready native implementation** of BLS (Boneh-Lynn-Shacham) signatures using the Herumi BLS library. This is the recommended implementation for verifying Ethereum consensus layer signatures (sync committees, validator attestations, light clients).

**Key Features:**
- Native Herumi BLS library (MCL/BLST backend)
- High-performance BLS12-381 operations
- Cross-platform support (Windows, Linux, macOS)
- Ethereum 2.0 sync committee verification
- Light client signature verification
- Production-tested in Ethereum infrastructure

**Use Cases:**
- Light clients (verify beacon chain without full node)
- Sync committee signature verification
- Consensus layer data validation
- Portal Network implementations
- Ethereum beacon chain APIs

## Installation

```bash
dotnet add package Nethereum.Signer.Bls.Herumi
```

**Platform Support:**
- Windows (x64)
- Linux (x64)

## Dependencies

**Nethereum:**
- **Nethereum.Signer.Bls** - Core BLS abstraction

**Native Libraries** (included in package):
- Herumi BLS native binaries for all supported platforms

## Quick Start

```csharp
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;

// Create Herumi BLS instance
var blsBindings = new HerumiBlsBindings();
var bls = new NativeBls(blsBindings);

// Initialize (loads native library)
await bls.InitializeAsync();

// Verify aggregate BLS signature
bool isValid = bls.VerifyAggregate(
    aggregateSignature,  // 96 bytes
    publicKeys,          // Array of 48-byte validator public keys
    messages,            // Array of 32-byte signing roots
    domain               // 32 bytes: forkDigest|domainType
);

Console.WriteLine($"Signature valid: {isValid}");
```

## API Reference

### HerumiBlsBindings

Native Herumi BLS bindings implementation.

```csharp
public class HerumiBlsBindings : INativeBlsBindings
{
    public Task EnsureAvailableAsync(CancellationToken cancellationToken = default);
    public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain);
}
```

## Important Notes

### BLS12-381 Curve

- **Curve**: BLS12-381 (NOT secp256k1)
- **Public Key**: 48 bytes (compressed G1 point)
- **Signature**: 96 bytes (compressed G2 point)
- **Security Level**: ~128-bit (equivalent to 3072-bit RSA)

### Domain Separation

Ethereum consensus layer domain types:

```csharp
public static class DomainTypes
{
    public static readonly byte[] DOMAIN_BEACON_PROPOSER = new byte[] { 0x00, 0x00, 0x00, 0x00 };
    public static readonly byte[] DOMAIN_BEACON_ATTESTER = new byte[] { 0x01, 0x00, 0x00, 0x00 };
    public static readonly byte[] DOMAIN_RANDAO = new byte[] { 0x02, 0x00, 0x00, 0x00 };
    public static readonly byte[] DOMAIN_SYNC_COMMITTEE = new byte[] { 0x07, 0x00, 0x00, 0x00 };
}
```

### Native Library Loading

The package includes native binaries in the `runtimes/` folder:

```
runtimes/
  win-x64/native/bls_eth.dll
  win-x64/native/mcl.dll
  linux-x64/native/libbls_eth.so
```

.NET automatically loads the correct native library for your platform.

### Performance

| Operation | Time | Notes |
|-----------|------|-------|
| **Init** | ~10ms | One-time initialization |
| **Verify 1 sig** | ~5ms | Single signature |
| **Verify 512 aggregate** | ~70ms | Sync committee (modern CPU) |
| **Verify 1000 aggregate** | ~130ms | Large aggregation |

**Optimization Tips:**
- Initialize once and reuse `NativeBls` instance
- Aggregate verification is much faster than N individual verifications
- Use cached domain values

### Thread Safety

`NativeBls` is **thread-safe** after initialization:

```csharp
// Initialize once
var bls = new NativeBls(new HerumiBlsBindings());
await bls.InitializeAsync();

// Safe to use from multiple threads
Parallel.For(0, 100, i =>
{
    var isValid = bls.VerifyAggregate(...);
});
```

### Error Handling

```csharp
try
{
    await bls.InitializeAsync();
}
catch (DllNotFoundException ex)
{
    // Native library not found for platform
    Console.WriteLine($"Platform not supported: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    // BLS library initialization failed
    Console.WriteLine($"BLS init failed: {ex.Message}");
}

// Verification errors
try
{
    var isValid = bls.VerifyAggregate(...);
}
catch (ArgumentException ex)
{
    // Invalid input (wrong sizes, null arrays)
    Console.WriteLine($"Invalid input: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    // Not initialized
    Console.WriteLine($"Call InitializeAsync first: {ex.Message}");
}
```

## Platform-Specific Notes

### Windows (x64)
- Requires Visual C++ Redistributable (usually pre-installed)
- Includes both `bls_eth.dll` and `mcl.dll`

### Linux (x64)
- Works on most distros (Ubuntu, Debian, Fedora, Alpine)
- May require `libstdc++6` on some systems
- Uses `libbls_eth.so`

## Related Packages

### Dependencies
- **Nethereum.Signer.Bls** - Core BLS abstraction

### Used By
- **Nethereum.Consensus.Ssz** - SSZ encoding
- Light client implementations
- Beacon chain verification tools

## Additional Resources

- [Herumi BLS Library](https://github.com/herumi/bls)
- [BLS12-381 Specification](https://hackmd.io/@benjaminion/bls12-381)
- [Ethereum Consensus Specs](https://github.com/ethereum/consensus-specs)
- [Sync Committee Protocol](https://github.com/ethereum/consensus-specs/blob/dev/specs/altair/sync-protocol.md)
- [Nethereum Documentation](http://docs.nethereum.com/)
