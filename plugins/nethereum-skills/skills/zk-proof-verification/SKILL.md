---
name: zk-proof-verification
description: Help users verify Groth16 zero-knowledge proofs from Circom/snarkjs circuits using Nethereum.ZkProofsVerifier (.NET). Use this skill whenever the user mentions ZK proofs, Groth16, snarkjs, Circom, zero-knowledge verification, BN128 pairing, proof.json, verification_key.json, or public.json in a C#/.NET context.
user-invocable: true
---

# ZK Proof Verification — Nethereum.ZkProofsVerifier

## When to Use This

Use this skill when a user wants to:
- Verify a Groth16 zero-knowledge proof in .NET
- Consume snarkjs/Circom output (proof.json, verification_key.json, public.json)
- Check if a ZK proof is valid before submitting a transaction
- Cross-validate native .NET verification against a Solidity Groth16 verifier
- Detect tampered proofs, inputs, or verification keys

## Required Packages

```bash
dotnet add package Nethereum.ZkProofsVerifier
```

## Core Concept

Groth16 is the most widely used ZK proof system in Ethereum. `Nethereum.ZkProofsVerifier` verifies proofs natively in .NET by checking a BN128 elliptic curve pairing equation. It directly consumes the three JSON files that snarkjs produces after proving a Circom circuit.

The recommended entry point is `CircomGroth16Adapter.Verify()` — a single static method that parses all three JSON files and returns a result. This hides all cryptographic internals (curve points, field extensions, BouncyCastle types) behind a clean API.

## One-Liner Verification (Recommended)

```csharp
using Nethereum.ZkProofsVerifier.Circom;

var proofJson = File.ReadAllText("proof.json");
var vkJson = File.ReadAllText("verification_key.json");
var publicJson = File.ReadAllText("public.json");

var result = CircomGroth16Adapter.Verify(proofJson, vkJson, publicJson);

if (result.IsValid)
    Console.WriteLine("Proof verified!");
else
    Console.WriteLine($"Failed: {result.Error}");
```

## Step-by-Step Parsing (When You Need to Inspect)

Parse each component separately to inspect structure before verifying:

```csharp
using Nethereum.ZkProofsVerifier.Circom;
using Nethereum.ZkProofsVerifier.Groth16;

var proof = SnarkjsProofParser.Parse(proofJson);
var vk = SnarkjsVerificationKeyParser.Parse(vkJson);
var publicInputs = SnarkjsPublicInputParser.Parse(publicJson);

// Inspect: IC array length must be publicInputs.Length + 1
Console.WriteLine($"Public inputs: {publicInputs.Length}");
Console.WriteLine($"IC points: {vk.IC.Length}");

var verifier = new Groth16Verifier();
var result = verifier.Verify(proof, vk, publicInputs);
```

## Tamper Detection

Verification rejects any modification to proof, inputs, or VK:

```csharp
// Wrong public inputs — verification fails
var tamperedPublicJson = "[\"999\"]";
var result = CircomGroth16Adapter.Verify(proofJson, vkJson, tamperedPublicJson);
// result.IsValid == false
```

## Error Messages

| Error | Meaning |
|-------|---------|
| `"Proof is null"` | Null proof passed |
| `"Verification key is null"` | Null VK passed |
| `"Public inputs array is null"` | Null inputs passed |
| `"Verification key IC array is empty"` | VK has no IC points |
| `"Expected N public inputs but got M"` | IC length doesn't match inputs + 1 |
| `"Pairing check failed"` | Proof is invalid (tampered or wrong inputs) |

## Key Types

| Type | Purpose |
|------|---------|
| `CircomGroth16Adapter` | One-liner verification from JSON strings |
| `Groth16Verifier` | Lower-level verifier (accepts parsed proof/VK objects) |
| `ZkVerificationResult` | Result with `IsValid` and `Error` properties |
| `SnarkjsProofParser` | Parses `proof.json` → `Groth16Proof` |
| `SnarkjsVerificationKeyParser` | Parses `verification_key.json` → `Groth16VerificationKey` |
| `SnarkjsPublicInputParser` | Parses `public.json` → `BigInteger[]` |

## Supported Formats

Only snarkjs JSON output is supported:
- `proof.json` — G1/G2 curve points (`pi_a`, `pi_b`, `pi_c`)
- `verification_key.json` — trusted setup output (`vk_alpha_1`, `vk_beta_2`, `vk_gamma_2`, `vk_delta_2`, `IC`)
- `public.json` — decimal string array of public circuit inputs

For full documentation, see: https://docs.nethereum.com/docs/consensus-and-cryptography/guide-zk-proof-verification
