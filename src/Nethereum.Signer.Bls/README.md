# Nethereum.Signer.Bls

Core BLS signature abstraction for Ethereum consensus layer operations (Beacon Chain, sync committees, light clients).

## Overview

Nethereum.Signer.Bls provides the **abstraction layer** for BLS (Boneh-Lynn-Shacham) signature verification in Nethereum. BLS signatures are used in Ethereum's consensus layer (Beacon Chain) for validator signatures, sync committees, and light client protocol. This package defines interfaces that are implemented by concrete BLS libraries like `Nethereum.Signer.Bls.Herumi`.

**Key Features:**
- BLS aggregate signature verification (consensus layer)
- Pluggable BLS implementation architecture
- Ethereum 2.0 sync committee signature verification
- Light client support (verify beacon chain data)
- Domain separation for Ethereum consensus layer

**Use Cases:**
- Light clients (verify beacon chain without running full node)
- Sync committee verification
- Consensus layer data validation
- Portal Network implementations
- Verkle tree state proofs (future Ethereum upgrades)

## Installation

```bash
dotnet add package Nethereum.Signer.Bls
dotnet add package Nethereum.Signer.Bls.Herumi  # Concrete implementation
```

## Dependencies

None - this is a pure abstraction package.

**Implementations:**
- **Nethereum.Signer.Bls.Herumi** - Native Herumi BLS library wrapper

## Quick Start

```csharp
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;  // Concrete implementation

// Create BLS instance with Herumi implementation
var blsBindings = new HerumiBlsBindings();
var bls = new NativeBls(blsBindings);
await bls.InitializeAsync();

// Verify aggregate BLS signature (e.g., from sync committee)
bool isValid = bls.VerifyAggregate(
    aggregateSignature: aggregateSig,      // 96 bytes
    publicKeys: validatorPublicKeys,       // Array of 48-byte public keys
    messages: messages,                    // Array of signing roots
    domain: domainSeparationTag            // 32 bytes: forkDigest|domainType
);

Console.WriteLine($"Signature valid: {isValid}");
```

## API Reference

### IBls Interface

Core interface for BLS operations.

```csharp
public interface IBls
{
    /// <summary>
    /// Verifies an aggregate BLS signature over one or more messages and public keys.
    /// </summary>
    bool VerifyAggregate(
        byte[] aggregateSignature,  // 96 bytes
        byte[][] publicKeys,        // Array of 48-byte public keys
        byte[][] messages,          // Array of 32-byte message hashes
        byte[] domain               // 32 bytes domain separation
    );
}
```

### NativeBls Class

Native BLS implementation wrapper.

```csharp
public class NativeBls : IBls
{
    public NativeBls(INativeBlsBindings bindings);
    public Task InitializeAsync(CancellationToken cancellationToken = default);
    public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain);
}
```

### BlsImplementationKind Enum

```csharp
public enum BlsImplementationKind
{
    None,
    HerumiNative,  // Herumi BLS (BLST/MCL)
    Managed        // Future: pure C# implementation
}
```

## Important Notes

### BLS12-381 Curve

- Ethereum consensus layer uses **BLS12-381** curve (NOT secp256k1)
- Public keys: 48 bytes (compressed G1 point)
- Signatures: 96 bytes (compressed G2 point)
- Different from execution layer (which uses secp256k1/ECDSA)

### Domain Separation

Ethereum consensus layer uses domain separation to prevent signature reuse:

```
domain = fork_version || domain_type
```

Common domain types:
- `DOMAIN_BEACON_PROPOSER` = `0x00000000` - Block proposals
- `DOMAIN_BEACON_ATTESTER` = `0x01000000` - Attestations
- `DOMAIN_SYNC_COMMITTEE` = `0x07000000` - Sync committee signatures

### Aggregate Signatures

BLS supports signature aggregation - multiple signatures can be combined into one:
- **Input**: N signatures from N validators
- **Output**: 1 aggregate signature (still 96 bytes)
- **Verification**: Verify all N signatures at once

### Performance

- Aggregate verification is **faster** than verifying N signatures individually
- ~50-100ms to verify 512-validator sync committee on modern hardware
- Native implementations (Herumi/BLST) are 100x faster than pure managed code

## Consensus Layer Use Cases

| Use Case | Description |
|----------|-------------|
| **Sync Committees** | 512 validators sign each beacon block |
| **Light Clients** | Verify beacon chain without full node |
| **Portal Network** | P2P light client network |
| **Validator Signatures** | Attest to beacon chain state |
| **Verkle Proofs** | Future: stateless client verification |

## Related Packages

### Implementations
- **Nethereum.Signer.Bls.Herumi** - Native Herumi BLS (production-ready)

### Used By
- **Nethereum.Consensus.Ssz** - SSZ encoding/decoding
- Light client implementations
- Beacon chain tools

## Additional Resources

- [BLS Signatures Spec](https://ethereum.github.io/consensus-specs/specs/phase0/beacon-chain#bls-signatures)
- [Sync Committee Spec](https://github.com/ethereum/consensus-specs/blob/dev/specs/altair/sync-protocol.md)
- [BLS12-381 For The Rest Of Us](https://hackmd.io/@benjaminion/bls12-381)
- [Nethereum Documentation](http://docs.nethereum.com/)
