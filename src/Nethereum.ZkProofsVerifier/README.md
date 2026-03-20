# Nethereum.ZkProofsVerifier

Native .NET Groth16 proof verification for Circom/snarkjs circuits on the BN128 curve. Verify zero-knowledge proofs entirely in C# without external dependencies on native libraries or JavaScript runtimes.

## Overview

Nethereum.ZkProofsVerifier implements the Groth16 zero-knowledge proof verification algorithm using BN128 elliptic curve pairings. It directly consumes the JSON output files produced by [snarkjs](https://github.com/iden3/snarkjs) (`proof.json`, `verification_key.json`, `public.json`), making it straightforward to integrate Circom circuit verification into .NET applications.

**Key capabilities:**

- **One-liner verification** via `CircomGroth16Adapter.Verify(proofJson, vkJson, publicJson)`
- **Step-by-step API** for parsing and verifying proofs independently
- **Tamper detection** — modified proofs, inputs, or verification keys are rejected
- **EVM-compatible** — produces the same results as Solidity Groth16 verifier contracts

## Installation

```bash
dotnet add package Nethereum.ZkProofsVerifier
```

### Dependencies

- **Nethereum.Signer** — BN128 curve arithmetic, Fp2/Fp12 extension fields, and optimal Ate pairing

## Quick Start

```csharp
using Nethereum.ZkProofsVerifier.Circom;

// Load snarkjs output files
var proofJson = File.ReadAllText("proof.json");
var vkJson = File.ReadAllText("verification_key.json");
var publicJson = File.ReadAllText("public.json");

// Verify in one line
var result = CircomGroth16Adapter.Verify(proofJson, vkJson, publicJson);

if (result.IsValid)
    Console.WriteLine("Proof verified!");
else
    Console.WriteLine($"Verification failed: {result.Error}");
```

## Usage Examples

### Example 1: End-to-End Verification with CircomGroth16Adapter

```csharp
using Nethereum.ZkProofsVerifier.Circom;

var result = CircomGroth16Adapter.Verify(proofJson, vkJson, publicInputsJson);

if (result.IsValid)
{
    Console.WriteLine("Proof is valid!");
}
else
{
    Console.WriteLine($"Verification failed: {result.Error}");
}
```

### Example 2: Step-by-Step Parsing and Verification

Parse each component separately for inspection or custom logic before verification:

```csharp
using Nethereum.ZkProofsVerifier.Circom;
using Nethereum.ZkProofsVerifier.Groth16;

// Parse each component independently
var proof = SnarkjsProofParser.Parse(proofJson);
var vk = SnarkjsVerificationKeyParser.Parse(vkJson);
var publicInputs = SnarkjsPublicInputParser.Parse(publicInputsJson);

// Inspect parsed structure
Console.WriteLine($"Public inputs: {publicInputs.Length}");
Console.WriteLine($"IC points: {vk.IC.Length}");  // Should be publicInputs.Length + 1

// Verify
var verifier = new Groth16Verifier();
var result = verifier.Verify(proof, vk, publicInputs);

Console.WriteLine(result.IsValid ? "Valid" : $"Invalid: {result.Error}");
```

### Example 3: Custom Verification with Groth16Verifier Directly

Construct proof and verification key objects from code when not using snarkjs JSON:

```csharp
using Nethereum.ZkProofsVerifier.Groth16;
using Nethereum.Signer.Crypto.BN128;
using System.Numerics;

// Construct proof from known curve points
var proof = new Groth16Proof
{
    A = /* G1 ECPoint */,
    B = /* G2 TwistPoint */,
    C = /* G1 ECPoint */
};

var vk = new Groth16VerificationKey
{
    Alpha = /* G1 ECPoint */,
    Beta  = /* G2 TwistPoint */,
    Gamma = /* G2 TwistPoint */,
    Delta = /* G2 TwistPoint */,
    IC    = new ECPoint[] { /* IC[0], IC[1], ... */ }
};

var publicInputs = new BigInteger[] { /* field elements */ };

var verifier = new Groth16Verifier();
var result = verifier.Verify(proof, vk, publicInputs);
```

### Example 4: Detecting Tampered Proofs

```csharp
var proof = SnarkjsProofParser.Parse(proofJson);
var vk = SnarkjsVerificationKeyParser.Parse(vkJson);
var publicInputs = SnarkjsPublicInputParser.Parse(publicInputsJson);

// Tamper with proof point A
var tamperedProof = new Groth16Proof
{
    A = proof.A.Negate(),  // Negate the first element
    B = proof.B,
    C = proof.C
};

var verifier = new Groth16Verifier();
var result = verifier.Verify(tamperedProof, vk, publicInputs);
// result.IsValid == false — tampered proof rejected
```

## API Reference

### CircomGroth16Adapter

High-level convenience class for verifying snarkjs/Circom JSON output directly.

```csharp
public static class CircomGroth16Adapter
{
    public static ZkVerificationResult Verify(
        string proofJson,
        string vkJson,
        string publicInputsJson);
}
```

### IZkProofVerifier<TProof, TVerificationKey>

Generic interface for zero-knowledge proof verification.

```csharp
public interface IZkProofVerifier<TProof, TVerificationKey>
{
    ZkVerificationResult Verify(TProof proof, TVerificationKey vk, BigInteger[] publicInputs);
}
```

### ZkVerificationResult

Immutable result of a verification attempt.

```csharp
public class ZkVerificationResult
{
    public bool IsValid { get; }
    public string Error { get; }

    public static ZkVerificationResult Valid();
    public static ZkVerificationResult Invalid(string reason);
}
```

### Groth16Verifier

Implements `IZkProofVerifier<Groth16Proof, Groth16VerificationKey>`. Verifies the Groth16 pairing equation:

```
e(-A, B) * e(Alpha, Beta) * e(vkX, Gamma) * e(C, Delta) == 1
```

Where `vkX = IC[0] + sum(IC[i+1] * publicInputs[i])`.

### Groth16Proof

```csharp
public class Groth16Proof
{
    public ECPoint A { get; set; }      // G1 point
    public TwistPoint B { get; set; }   // G2 point
    public ECPoint C { get; set; }      // G1 point
}
```

### Groth16VerificationKey

```csharp
public class Groth16VerificationKey
{
    public ECPoint Alpha { get; set; }      // G1 point
    public TwistPoint Beta { get; set; }    // G2 point
    public TwistPoint Gamma { get; set; }   // G2 point
    public TwistPoint Delta { get; set; }   // G2 point
    public ECPoint[] IC { get; set; }       // Length = publicInputs.Length + 1
}
```

### Snarkjs Parsers

| Parser | Input | Output |
|--------|-------|--------|
| `SnarkjsProofParser.Parse(json)` | `proof.json` | `Groth16Proof` |
| `SnarkjsVerificationKeyParser.Parse(json)` | `verification_key.json` | `Groth16VerificationKey` |
| `SnarkjsPublicInputParser.Parse(json)` | `public.json` | `BigInteger[]` |

## G2 Coordinate Mapping (Fp2 Swap)

When parsing snarkjs G2 points (`pi_b`, `vk_beta_2`, `vk_gamma_2`, `vk_delta_2`), the parsers perform a critical coordinate swap. Snarkjs stores Fp2 elements as `[c0, c1]` (imaginary, real), but the internal `Fp2` constructor takes `Fp2(a, b)` where `a` is imaginary and `b` is real. The parser reads `c0` and `c1` then constructs `new Fp2(c1, c0)` to produce the correct field element.

This swap is handled automatically by the parsers — you only need to be aware of it when constructing G2 points manually or cross-validating with other implementations.

## Supported Formats

| File | Format | Description |
|------|--------|-------------|
| `proof.json` | `{ "pi_a": [x,y], "pi_b": [[x0,x1],[y0,y1]], "pi_c": [x,y] }` | Proof elements (G1 + G2 + G1) |
| `verification_key.json` | `{ "vk_alpha_1", "vk_beta_2", "vk_gamma_2", "vk_delta_2", "IC" }` | Verification key from trusted setup |
| `public.json` | `["input1", "input2", ...]` | Public circuit inputs as decimal strings |

## Related Packages

- **Nethereum.Signer** — BN128 curve operations, Fp2/Fp12 extension fields, optimal Ate pairing
- **Nethereum.Util** — Poseidon hashing (`PoseidonHasher` with `CircomT1`/`CircomT2`/`CircomT3` presets) for ZK circuit inputs
- **Nethereum.Merkle** — Sparse Merkle Binary Tree with `PoseidonSmtHasher` for ZK-compatible state trees
