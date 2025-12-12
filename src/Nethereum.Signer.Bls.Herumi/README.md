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
- Windows (x64, ARM64)
- Linux (x64, ARM64)
- macOS (x64, ARM64)

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

## Usage Examples

### Example 1: Basic Setup and Verification

```csharp
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;

// Initialize Herumi BLS
var blsBindings = new HerumiBlsBindings();
var bls = new NativeBls(blsBindings);
await bls.InitializeAsync();

// Example sync committee signature verification
byte[] aggregateSignature = Convert.FromHexString("0x..."); // 96 bytes
byte[][] publicKeys = new byte[512][]; // 512 validators
for (int i = 0; i < 512; i++)
{
    publicKeys[i] = Convert.FromHexString("0x..."); // 48 bytes each
}

byte[] signingRoot = Convert.FromHexString("0x..."); // 32 bytes
byte[] domain = Convert.FromHexString("0x..."); // 32 bytes

// Verify
bool isValid = bls.VerifyAggregate(
    aggregateSignature,
    publicKeys,
    new[] { signingRoot },  // Single message for sync committee
    domain
);

Console.WriteLine($"Sync committee signature valid: {isValid}");
```

### Example 2: Light Client Integration

```csharp
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;

public class EthereumLightClient
{
    private readonly NativeBls _bls;

    public EthereumLightClient()
    {
        var blsBindings = new HerumiBlsBindings();
        _bls = new NativeBls(blsBindings);
    }

    public async Task InitializeAsync()
    {
        await _bls.InitializeAsync();
    }

    public async Task<bool> VerifyLightClientUpdateAsync(LightClientUpdate update)
    {
        // Extract sync committee data
        var aggregateSignature = update.SyncAggregate.SyncCommitteeSignature;
        var syncCommittee = update.SyncCommittee;
        var participantPubKeys = GetParticipatingValidators(syncCommittee, update.SyncAggregate.SyncCommitteeBits);

        // Compute signing root
        var signingRoot = update.AttestedHeader.GetSigningRoot();

        // Compute domain
        var forkVersion = update.ForkVersion;
        var domain = ComputeDomain(forkVersion, DOMAIN_SYNC_COMMITTEE);

        // Verify aggregate signature
        return _bls.VerifyAggregate(
            aggregateSignature,
            participantPubKeys,
            new[] { signingRoot },
            domain
        );
    }

    private byte[][] GetParticipatingValidators(SyncCommittee committee, byte[] bits)
    {
        var participating = new List<byte[]>();
        for (int i = 0; i < 512; i++)
        {
            if ((bits[i / 8] & (1 << (i % 8))) != 0)
            {
                participating.Add(committee.PublicKeys[i]);
            }
        }
        return participating.ToArray();
    }

    private byte[] ComputeDomain(byte[] forkVersion, byte[] domainType)
    {
        // domain = fork_version || domain_type
        var domain = new byte[32];
        Array.Copy(forkVersion, 0, domain, 0, 4);
        Array.Copy(domainType, 0, domain, 4, 4);
        return domain;
    }
}
```

### Example 3: Batch Verification Performance

```csharp
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;
using System.Diagnostics;

var blsBindings = new HerumiBlsBindings();
var bls = new NativeBls(blsBindings);
await bls.InitializeAsync();

// Prepare test data
var aggregateSignature = GetSyncCommitteeSignature();
var publicKeys = GetValidatorPublicKeys(512);
var signingRoot = GetBeaconBlockRoot();
var domain = ComputeSyncCommitteeDomain();

// Benchmark verification
var sw = Stopwatch.StartNew();
bool isValid = bls.VerifyAggregate(
    aggregateSignature,
    publicKeys,
    new[] { signingRoot },
    domain
);
sw.Stop();

Console.WriteLine($"Verified 512-validator aggregate in {sw.ElapsedMilliseconds}ms");
Console.WriteLine($"Result: {isValid}");

// Typical results:
// - Modern CPU: 50-100ms for 512 validators
// - Raspberry Pi 4: 200-500ms
```

### Example 4: Multi-Message Verification

```csharp
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;

var blsBindings = new HerumiBlsBindings();
var bls = new NativeBls(blsBindings);
await bls.InitializeAsync();

// Different validators signing different messages
var aggregateSignature = GetAggregateAttestation();
var publicKeys = new[]
{
    validator1PubKey,  // 48 bytes
    validator2PubKey,
    validator3PubKey
};

var messages = new[]
{
    attestationRoot1,  // 32 bytes each
    attestationRoot2,
    attestationRoot3
};

var domain = ComputeDomain(DOMAIN_BEACON_ATTESTER);

// Verify multi-message aggregate
bool isValid = bls.VerifyAggregate(
    aggregateSignature,
    publicKeys,
    messages,  // Different message per validator
    domain
);

Console.WriteLine($"Multi-message aggregate valid: {isValid}");
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

The package includes native binaries for all platforms in the `runtimes/` folder:

```
runtimes/
  win-x64/native/herumi.dll
  win-arm64/native/herumi.dll
  linux-x64/native/libherumi.so
  linux-arm64/native/libherumi.so
  osx-x64/native/libherumi.dylib
  osx-arm64/native/libherumi.dylib
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

### Windows
- Works out of the box
- Requires Visual C++ Redistributable (usually pre-installed)

### Linux
- Works on most distros (Ubuntu, Debian, Fedora, Alpine)
- May require `libstdc++6` on some systems

### macOS
- Supports both Intel (x64) and Apple Silicon (ARM64)
- No additional dependencies required

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

## License

This package is part of the Nethereum project and follows the same MIT license.

The Herumi BLS library is licensed under the Modified BSD License.
