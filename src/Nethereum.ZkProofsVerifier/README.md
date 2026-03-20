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

### Example 2: Detecting Tampered Proofs

Verification rejects any modification to the proof, inputs, or verification key:

```csharp
using Nethereum.ZkProofsVerifier.Circom;

// Verify with wrong public inputs
var tamperedPublicJson = "[\"999\"]";
var result = CircomGroth16Adapter.Verify(proofJson, vkJson, tamperedPublicJson);
// result.IsValid == false — inputs don't match what was proven

// Verify with mismatched circuit files
var result2 = CircomGroth16Adapter.Verify(proofJsonA, vkJsonB, publicJsonA);
// result2.IsValid == false — VK doesn't match the proof's circuit
```

## API Reference

### CircomGroth16Adapter

The public entry point for verifying snarkjs/Circom proofs. Parses all three JSON files internally and runs the BN128 pairing check.

```csharp
public static class CircomGroth16Adapter
{
    public static ZkVerificationResult Verify(
        string proofJson,
        string vkJson,
        string publicInputsJson);
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

### Verification Algorithm

Internally, the adapter parses the JSON inputs into BN128 curve points and checks the Groth16 pairing equation:

```
e(-A, B) · e(Alpha, Beta) · e(vkX, Gamma) · e(C, Delta) == 1
```

Where `vkX = IC[0] + sum(IC[i+1] * publicInputs[i])`.

All cryptographic types (curve points, field extensions, parsers) are internal implementation details — consumers only interact with `CircomGroth16Adapter` and `ZkVerificationResult`.

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
