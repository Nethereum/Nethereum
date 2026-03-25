# Nethereum.ZkProofs.Snarkjs.Blazor

Browser-based Groth16 zero-knowledge proof generation for Blazor WebAssembly applications. This package provides a `IZkProofProvider` implementation that calls [snarkjs](https://github.com/iden3/snarkjs) via JavaScript interop, enabling client-side proof generation entirely in the browser.

## Installation

```bash
dotnet add package Nethereum.ZkProofs.Snarkjs.Blazor
```

You also need the snarkjs JavaScript library in your Blazor app's `wwwroot`:

```bash
npm install snarkjs
cp node_modules/snarkjs/build/snarkjs.min.js wwwroot/js/snarkjs.min.js
```

## Usage

### 1. Initialize the provider

```csharp
@inject IJSRuntime JSRuntime
@implements IAsyncDisposable

@code {
    private SnarkjsBlazorProvider? _provider;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        // Point to snarkjs in your wwwroot
        _provider = new SnarkjsBlazorProvider(JSRuntime, "./js/snarkjs.min.js");
        await _provider.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
            await _provider.DisposeAsync();
    }
}
```

### 2. Generate a proof

```csharp
var result = await _provider.FullProveAsync(new ZkProofRequest
{
    CircuitWasm = wasmBytes,    // .wasm circuit binary
    CircuitZkey = zkeyBytes,    // .zkey proving key
    InputJson = "{\"nullifier\": \"12345\", \"secret\": \"67890\", \"value\": \"1000000000000000000\", \"label\": \"1\"}"
});

// result.ProofJson      — Groth16 proof JSON (pi_a, pi_b, pi_c)
// result.PublicSignals   — parsed BigInteger[] of public outputs
// result.PublicSignalsJson — raw JSON array of public signals
```

### 3. Verify the proof (pure C#)

```csharp
using Nethereum.ZkProofsVerifier.Circom;

var verification = CircomGroth16Adapter.Verify(
    result.ProofJson, verificationKeyJson, result.PublicSignalsJson);

if (verification.IsValid)
    Console.WriteLine("Proof verified!");
```

## How It Works

1. **`InitializeAsync()`** loads the `snarkjsInterop.js` ES module (shipped with this package) and then loads snarkjs via a `<script>` tag from the URL you provide.

2. **`FullProveAsync()`** sends the circuit WASM and zkey as base64 to JavaScript, where snarkjs evaluates the circuit constraints and computes the Groth16 proof. The proof and public signals are returned as JSON.

3. All private inputs stay in the browser — nothing is sent to a server.

## Hosting snarkjs

The snarkjs library is **not bundled** with this package. You provide the URL at construction time. Options:

| Approach | URL | Notes |
|----------|-----|-------|
| Self-hosted | `"./js/snarkjs.min.js"` | Copy from `node_modules/snarkjs/build/snarkjs.min.js` to `wwwroot/js/` |
| CDN | `"https://cdn.jsdelivr.net/npm/snarkjs@0.7.5/build/snarkjs.min.js"` | No local files needed |

The library must be a UMD bundle that sets `window.snarkjs` (the standard npm build).

## Demo App

See the working demo at `src/demos/Nethereum.ZkProofs.Blazor.Demo/` which generates and verifies Privacy Pools commitment proofs with educational UI.

## Native Alternative

For desktop, server, or mobile applications where JavaScript is not available, use the native proof generation pipeline instead:

| Package | Role |
|---------|------|
| `Nethereum.CircomWitnessCalc` | Native witness generation (C, P/Invoke) |
| `Nethereum.ZkProofs.RapidSnark` | Native Groth16 proof generation (C++, P/Invoke) |

The native pipeline is typically 10-50x faster than browser-based snarkjs.

## API Reference

### SnarkjsBlazorProvider

```csharp
public class SnarkjsBlazorProvider : IZkProofProvider, IAsyncDisposable
{
    // Constructor: jsRuntime from DI, snarkjsUrl points to snarkjs.min.js
    public SnarkjsBlazorProvider(IJSRuntime jsRuntime, string snarkjsUrl);

    // Always returns ZkProofScheme.Groth16
    public ZkProofScheme Scheme { get; }

    // Load JS interop module and initialize snarkjs — call once after first render
    public Task InitializeAsync(CancellationToken cancellationToken = default);

    // Generate a Groth16 proof from circuit WASM, zkey, and input JSON
    public Task<ZkProofResult> FullProveAsync(ZkProofRequest request, CancellationToken cancellationToken = default);

    // Dispose the JS module reference
    public ValueTask DisposeAsync();
}
```

## Supported Frameworks

`net8.0`, `net10.0`
