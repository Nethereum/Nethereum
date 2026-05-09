---
name: zk-proof-verification
description: "Verify Groth16 zero-knowledge proofs from Circom/snarkjs circuits using Nethereum.ZkProofsVerifier (.NET): parse proof.json, validate BN128 pairing checks, detect tampered inputs, and integrate ZK verification into .NET applications. Use this skill whenever the user mentions ZK proofs, Groth16, snarkjs, Circom, zero-knowledge verification, BN128 pairing, proof.json, verification_key.json, or public.json in a C#/.NET context."
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

`Nethereum.ZkProofsVerifier` verifies Groth16 proofs natively in .NET by checking a BN128 elliptic curve pairing equation against the three JSON files snarkjs produces. The recommended entry point is `CircomGroth16Adapter.Verify()` — a single static method that parses all three files and returns a result.

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
| `ZkVerificationResult` | Result with `IsValid` and `Error` properties |

All parsing and cryptographic types (curve points, field extensions, verifier internals) are `internal` — consumers only interact with `CircomGroth16Adapter` and `ZkVerificationResult`.

## Supported Formats

Only snarkjs JSON output is supported:
- `proof.json` — G1/G2 curve points (`pi_a`, `pi_b`, `pi_c`)
- `verification_key.json` — trusted setup output (`vk_alpha_1`, `vk_beta_2`, `vk_gamma_2`, `vk_delta_2`, `IC`)
- `public.json` — decimal string array of public circuit inputs

For full documentation, see: https://docs.nethereum.com/docs/consensus-and-cryptography/guide-zk-proof-verification
