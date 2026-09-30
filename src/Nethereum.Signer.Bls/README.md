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
var blsBindings = new HerumiNativeBindings();
var bls = new NativeBls(blsBindings);
await bls.InitializeAsync();

// Verify aggregate BLS signature (e.g., from sync committee)
bool isValid = bls.VerifyAggregate(
    aggregateSignature: aggregateSig,      // 96 bytes
    publicKeys: validatorPublicKeys,       // Array of 48-byte public keys
    messages: messages,                    // Array of signing roots
    domain: domain                         // 32-byte consensus-spec domain
);

Console.WriteLine($"Signature valid: {isValid}");
```

## API Reference

### IBls Interface

Core interface for BLS operations, implemented by `NativeBls`.

```csharp
public interface IBls
{
    // Verifies an aggregate BLS signature (typically sync committees) over one or
    // more messages and public keys; the domain is only length-checked (32 bytes).
    bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain);

    // Aggregates multiple BLS signatures into a single signature.
    // Used for ERC-4337 BLS signature aggregation to reduce gas costs.
    byte[] AggregateSignatures(byte[][] signatures);

    // Verifies an individual BLS signature over a message.
    bool Verify(byte[] signature, byte[] publicKey, byte[] message);

    // Splits a combined signature+publicKey byte array (ERC-4337 format) into its parts.
    (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey);
}
```

### NativeBls Class

`IBls` implementation that routes verification requests to a native BLST/MCL backend
via an `INativeBlsBindings` implementation (e.g. `HerumiNativeBindings`).

```csharp
public class NativeBls : IBls
{
    public NativeBls(INativeBlsBindings bindings);
    public Task InitializeAsync(CancellationToken cancellationToken = default);

    public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain);
    public byte[] AggregateSignatures(byte[][] signatures);
    public bool Verify(byte[] signature, byte[] publicKey, byte[] message);
    public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey);
}
```

`ExtractSignatureAndPublicKey` is implemented directly on `NativeBls` (it is pure byte
slicing — 96 bytes signature + 48 bytes public key — and needs no native call), so it
works even before `InitializeAsync` is called.

### IBls12381Operations Interface

EIP-2537 BLS12-381 precompile primitives (point/scalar operations), separate from the
signature-verification surface of `IBls`. Implemented by `Nethereum.Signer.Bls.Herumi`'s
`Bls12381Operations` and consumed by `Nethereum.EVM.Precompiles.Bls`.

```csharp
public interface IBls12381Operations
{
    byte[] G1Add(byte[] p1, byte[] p2);
    byte[] G1Mul(byte[] point, byte[] scalar);
    byte[] G1Msm(byte[][] points, byte[][] scalars);

    byte[] G2Add(byte[] p1, byte[] p2);
    byte[] G2Mul(byte[] point, byte[] scalar);
    byte[] G2Msm(byte[][] points, byte[][] scalars);

    bool Pairing(byte[][] g1Points, byte[][] g2Points);

    byte[] MapFpToG1(byte[] fp);
    byte[] MapFp2ToG2(byte[] fp2);
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

`domain` is the 32-byte consensus-spec domain (`domain_type` followed by the first 28 bytes of `fork_data_root`). `NativeBls`/`HerumiNativeBindings` only validate that it is 32 bytes when supplied.

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

### Used By (per `ProjectReference`/`PackageReference` in `src/`)
- **Nethereum.Consensus.LightClient** - Beacon chain sync committee / light client verification
- **Nethereum.EVM.Precompiles.Bls** - EIP-2537 BLS12-381 precompiles (`IBls12381Operations`)
- **Nethereum.AccountAbstraction.Bundler** - ERC-4337 BLS signature aggregation
- **Nethereum.Wallet** - Wallet BLS signing/verification
- **Nethereum.MainnetChain** / **Nethereum.MainnetChain.Server** - via the `Nethereum.Signer.Bls.Herumi` package
- **Nethereum.Node.HarnessServer** - via the `Nethereum.Signer.Bls.Herumi` package

## Additional Resources

- [BLS Signatures Spec](https://ethereum.github.io/consensus-specs/specs/phase0/beacon-chain#bls-signatures)
- [Sync Committee Spec](https://github.com/ethereum/consensus-specs/blob/dev/specs/altair/sync-protocol.md)
- [BLS12-381 For The Rest Of Us](https://hackmd.io/@benjaminion/bls12-381)
- [Nethereum Documentation](http://docs.nethereum.com/)
