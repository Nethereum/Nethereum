# Nethereum.ZkProofs

Provider-agnostic interfaces and models for zero-knowledge proof generation and verification in .NET. This package defines the core abstractions that all Nethereum ZK proof providers implement.

## Installation

```bash
dotnet add package Nethereum.ZkProofs
```

## Core Interfaces

### IZkProofProvider

The central abstraction for proof generation. All providers (browser-based, native, HTTP) implement this interface:

```csharp
IZkProofProvider provider = ...; // SnarkjsBlazorProvider, RapidSnarkProofProvider, HttpZkProofProvider

var result = await provider.FullProveAsync(new ZkProofRequest
{
    CircuitWasm = wasmBytes,
    CircuitZkey = zkeyBytes,
    InputJson = "{\"nullifier\": \"12345\", \"secret\": \"67890\"}"
});

Console.WriteLine($"Proof: {result.ProofJson}");
Console.WriteLine($"Public signals: {string.Join(", ", result.PublicSignals)}");
```

### ICircuitArtifactSource

Abstracts how circuit artifacts (WASM binaries, proving keys) are loaded:

```csharp
// From filesystem
ICircuitArtifactSource source = new FileCircuitArtifactSource("./circuits");
byte[] wasm = await source.GetWasmAsync("commitment");
byte[] zkey = await source.GetZkeyAsync("commitment");

// From memory (pre-loaded bytes)
ICircuitArtifactSource embedded = new EmbeddedCircuitArtifactSource(wasmBytes, zkeyBytes);

// From embedded resources (e.g., Nethereum.PrivacyPools.Circuits)
var privacyPools = new PrivacyPoolCircuitSource();
byte[] wasm = await privacyPools.GetWasmAsync("commitment");
```

### ICircuitGraphSource

For native witness generation, circuits can be compiled to a graph format (.graph.bin) instead of WASM:

```csharp
ICircuitGraphSource graphSource = ...;
if (graphSource.HasGraph("commitment"))
{
    byte[] graphData = graphSource.GetGraphData("commitment");
    // Pass to WitnessCalculator.CalculateWitness(graphData, inputJson)
}
```

## Models

### ZkProofRequest

Input model for proof generation:

| Property | Type | Description |
|----------|------|-------------|
| `CircuitWasm` | `byte[]` | WebAssembly circuit binary |
| `CircuitZkey` | `byte[]` | Proving key (zkey) binary |
| `InputJson` | `string` | JSON-serialized circuit inputs |
| `WitnessBytes` | `byte[]` | Pre-computed witness (.wtns) for native providers |
| `CircuitGraph` | `byte[]` | Circuit graph data for native witness generation |
| `CircuitName` | `string` | Circuit identifier |
| `Scheme` | `ZkProofScheme` | Proof scheme (default: Groth16) |

### ZkProofResult

Output model with parsed proof data:

```csharp
ZkProofResult result = await provider.FullProveAsync(request);

// JSON representations
string proofJson = result.ProofJson;           // {"pi_a": [...], "pi_b": [...], "pi_c": [...]}
string signalsJson = result.PublicSignalsJson;  // ["12345", "67890", ...]

// Parsed public signals as BigInteger[]
BigInteger commitmentHash = result.PublicSignals[0];
BigInteger nullifierHash = result.PublicSignals[1];
```

Factory method for creating results from JSON:

```csharp
var result = ZkProofResult.BuildFromJson(ZkProofScheme.Groth16, proofJson, publicSignalsJson);
```

### ZkProofScheme

```csharp
public enum ZkProofScheme
{
    Unknown = 0,
    Groth16 = 1,
    Plonk   = 2,
    Fflonk  = 3,
    Stark   = 4
}
```

## Providers

### Built-in: HttpZkProofProvider

Delegates proof generation to a remote HTTP endpoint:

```csharp
var httpClient = new HttpClient();
var provider = new HttpZkProofProvider(httpClient, "https://prover.example.com/prove");

var result = await provider.FullProveAsync(new ZkProofRequest
{
    CircuitWasm = wasmBytes,
    CircuitZkey = zkeyBytes,
    InputJson = inputJson
});
```

### External Providers

| Package | Provider | Platform | Description |
|---------|----------|----------|-------------|
| `Nethereum.ZkProofs.Snarkjs.Blazor` | `SnarkjsBlazorProvider` | Browser (Blazor WASM) | JS interop with snarkjs |
| `Nethereum.ZkProofs.RapidSnark` | `RapidSnarkProofProvider` | Desktop/Server | Native C++ via P/Invoke |
| `Nethereum.ZkProofsVerifier` | N/A (verification only) | All platforms | Pure C# BN128 Groth16 verifier |

## Groth16 Utilities

Convert proof JSON to Solidity-compatible format for on-chain verification:

```csharp
using Nethereum.ZkProofs.Groth16;

var proof = Groth16ProofConverter.ParseProofJson(result.ProofJson);
var (pA, pB, pC) = Groth16ProofConverter.ToSolidityProof(proof);

// pA, pB, pC are ready for Solidity Groth16Verifier.verifyProof(pA, pB, pC, publicSignals)
```

## Circuit Artifact Locator

Resolves filesystem paths following the standard circuit directory layout:

```csharp
var locator = new CircuitArtifactLocator("./circuits");

string wasmPath = locator.GetWasmPath("commitment");  // ./circuits/commitment/commitment.wasm
string zkeyPath = locator.GetZkeyPath("commitment");  // ./circuits/commitment/commitment.zkey
string vkPath   = locator.GetVkPath("commitment");    // ./circuits/commitment/commitment_vk.json

if (locator.HasArtifacts("commitment"))
{
    // Both WASM and zkey exist
}
```

## Supported Frameworks

`net451`, `net461`, `netstandard2.0`, `net6.0`, `net8.0`, `net9.0`, `net10.0`
