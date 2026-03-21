# Nethereum.PrivacyPools.Circuits

Embedded circuit artifacts (WASM, zkey, verification keys) for the 0xbow Privacy Pools protocol. Provides `PrivacyPoolCircuitSource` — an `ICircuitArtifactSource` implementation that loads compiled Circom circuits from embedded resources, requiring no file-system setup.

## Usage

```csharp
var source = new PrivacyPoolCircuitSource();

// Check availability
if (source.HasCircuit(PrivacyPoolCircuitSource.CommitmentCircuit))
{
    // Load artifacts for proof generation
    byte[] wasm = await source.GetWasmAsync("commitment");
    byte[] zkey = await source.GetZkeyAsync("commitment");

    // Load verification key for local verification
    string vkJson = source.GetVerificationKeyJson("commitment");
}

// Available circuits
// PrivacyPoolCircuitSource.CommitmentCircuit  — "commitment" (ragequit proofs)
// PrivacyPoolCircuitSource.WithdrawalCircuit  — "withdrawal" (withdrawal proofs)
```

## With Proof Generation

This package provides the circuit artifacts. You also need a proof provider to generate proofs — choose one based on your runtime:

| Provider | Package | Runtime |
|----------|---------|---------|
| `SnarkjsProofProvider` | `Nethereum.ZkProofs.Snarkjs` | Node.js (CLI / server) |
| `SnarkjsBlazorProvider` | `Nethereum.ZkProofs.Snarkjs.Blazor` | Browser (Blazor WASM) |

**CLI / server** (requires Node.js installed):

```csharp
var circuitSource = new PrivacyPoolCircuitSource();
var proofProvider = new PrivacyPoolProofProvider(
    new SnarkjsProofProvider(), circuitSource);

var ragequitResult = await proofProvider.GenerateRagequitProofAsync(
    new RagequitWitnessInput
    {
        Nullifier = commitment.Nullifier,
        Secret = commitment.Secret,
        Value = commitment.Value,
        Label = commitment.Label
    });
// ragequitResult.ProofJson, ragequitResult.PublicJson, ragequitResult.Signals
```

**Blazor WASM** (runs in-browser via JS interop, snarkjs.min.mjs must be in wwwroot):

```csharp
var circuitSource = new PrivacyPoolCircuitSource();
var blazorProvider = new SnarkjsBlazorProvider(jsRuntime, "./js/snarkjs.min.mjs");
await blazorProvider.InitializeAsync();
var proofProvider = new PrivacyPoolProofProvider(blazorProvider, circuitSource);
```

## Alternative: Download from URL

If you prefer not to embed circuit artifacts, use `UrlCircuitArtifactSource` from `Nethereum.PrivacyPools` to fetch them from a URL with local disk caching:

```csharp
var source = new UrlCircuitArtifactSource(
    "https://example.com/circuits/v1",
    cacheDir: "./circuit-cache");
await source.InitializeAsync("commitment", "withdrawal");
```

## Package Relationship

| Package | Role |
|---------|------|
| **Nethereum.PrivacyPools** | Core SDK (commitments, tree, accounts, contracts) |
| **Nethereum.PrivacyPools.Circuits** (this) | Embedded WASM/zkey/vk resources |
| **Nethereum.ZkProofs.Snarkjs** | Proof generation engine (CLI/server, requires Node.js) |
| **Nethereum.ZkProofs.Snarkjs.Blazor** | Proof generation engine (browser, Blazor WASM) |

## Dependencies

- `Nethereum.PrivacyPools` — `ICircuitArtifactSource` interface
- `Nethereum.ZkProofs` — ZK proof abstractions
