# Nethereum.ZkProofs.Snarkjs

**A local Groth16 proof provider that runs snarkjs in a Node.js process, for desktop, server and CLI applications**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.ZkProofs.Snarkjs.svg)](https://www.nuget.org/packages/Nethereum.ZkProofs.Snarkjs/)

## Overview

`Nethereum.ZkProofs.Snarkjs` implements the `IZkProofProvider` abstraction from [Nethereum.ZkProofs](../Nethereum.ZkProofs/README.md) by shelling out to the [snarkjs](https://github.com/iden3/snarkjs) command line. It takes a Circom circuit (`.wasm`), its proving key (`.zkey`) and the input JSON, and returns a Groth16 proof with its public signals. Use it when your .NET process can launch Node.js (desktop apps, servers, CLIs, test runs). The package description says "Local snarkjs proof provider for desktop/server/CLI (Node.js process or native interop)".

The backend is behind an interface (`ISnarkjsBackend`), so `SnarkjsProofProvider` can be pointed at another way of running snarkjs; `NodeJsSnarkjsBackend` is the one implementation in this package. The package only proves with Groth16 (`Scheme` is always `ZkProofScheme.Groth16`).

## Installation

```bash
dotnet add package Nethereum.ZkProofs.Snarkjs
```

Targets `net8.0` and `net10.0`; depends on `Nethereum.ZkProofs`. To run proofs with the Node.js backend you need Node.js on the `PATH` (or pass its path) and snarkjs reachable either through `npx snarkjs` or through an explicit path to snarkjs's `cli.js`.

## SnarkjsProofProvider

Namespace `Nethereum.ZkProofs.Snarkjs`.

```csharp
public class SnarkjsProofProvider : IZkProofProvider
{
    public ZkProofScheme Scheme { get; }

    public SnarkjsProofProvider()
    public SnarkjsProofProvider(ISnarkjsBackend backend)

    public static SnarkjsProofProvider CreateNodeJs(string? nodePath = null, string? snarkjsPath = null)

    public async Task<ZkProofResult> FullProveAsync(ZkProofRequest request, CancellationToken cancellationToken = default)
}

public interface ISnarkjsBackend
{
    Task<(string proofJson, string publicJson)> FullProveAsync(
        string wasmPath, string zkeyPath, string inputJsonPath, CancellationToken cancellationToken = default);
}
```

- The parameterless constructor uses `new NodeJsSnarkjsBackend()`; `CreateNodeJs(nodePath, snarkjsPath)` uses `new NodeJsSnarkjsBackend(nodePath, snarkjsPath)`.
- A null backend or a null request throws `ArgumentNullException`.
- `FullProveAsync` writes `request.CircuitWasm`, `request.CircuitZkey` and `request.InputJson` to `circuit.wasm`, `circuit.zkey` and `input.json` in a fresh temporary directory (`nethereum_zkproofs_` plus a random suffix), calls the backend with those paths, builds the `ZkProofResult` from the returned JSON with `ZkProofResult.BuildFromJson(ZkProofScheme.Groth16, proofJson, publicJson)`, and deletes the directory afterwards. Only `CircuitWasm`, `CircuitZkey` and `InputJson` of the request are used; `WitnessBytes`, `CircuitGraph`, `CircuitName` and `Scheme` are ignored by this provider.

`ZkProofResult` (from `Nethereum.ZkProofs`) carries `Scheme`, `ProofJson`, `PublicSignalsJson` and the parsed `PublicSignals` (`BigInteger[]`).

### Prove through a backend

```csharp
var backend = new RecordingBackend();
IZkProofProvider provider = new SnarkjsProofProvider(backend);

var result = await provider.FullProveAsync(new ZkProofRequest
{
    CircuitWasm = new byte[] { 1, 2, 3 },
    CircuitZkey = new byte[] { 4, 5, 6 },
    InputJson = "{\"a\":\"3\"}"
});
```

Here `RecordingBackend` is the test's own `ISnarkjsBackend` that records the three files it was given and returns `("{\"pi_a\":[\"1\",\"2\",\"1\"]}", "[\"7\",\"11\"]")`; `result.PublicSignals` is `[7, 11]`, `result.Scheme` is `Groth16`, and the temporary files are gone after the call. Backed by `SnarkjsProofProviderTests.Given_ABackend_When_FullProveAsyncRuns_Then_TheCircuitFilesAreWrittenAndTheResultIsParsed` (`DocSection.Protocols`, `snarkjs-proof-provider`, Order 1) in `tests/Nethereum.ZkProofs.RapidSnark.Tests/SnarkjsProofProviderTests.cs`.

## NodeJsSnarkjsBackend

```csharp
public class NodeJsSnarkjsBackend : ISnarkjsBackend
{
    public NodeJsSnarkjsBackend(string nodePath = null, string snarkjsPath = null)

    public async Task<(string proofJson, string publicJson)> FullProveAsync(
        string wasmPath, string zkeyPath, string inputJsonPath, CancellationToken cancellationToken = default)
}
```

- `nodePath` defaults to the first `node` found on the `PATH` (with `.exe`, `.cmd`, `.bat` extensions tried on Windows), or the bare name `node`.
- If `snarkjsPath` is given, snarkjs runs as `node "<snarkjsPath>" <command>`; if not, as `npx snarkjs <command>` (`npx` is also located on the `PATH`).
- `FullProveAsync` first checks that the wasm, zkey and input files exist (`FileNotFoundException` otherwise), then runs `wtns calculate <wasm> <input> witness.wtns` and `groth16 prove <zkey> witness.wtns proof.json public.json`, with the outputs written beside the input file, and returns the contents of `proof.json` and `public.json`. A non-zero exit code throws `InvalidOperationException` with the exit code, stderr and stdout.

The guard clauses are covered by `SnarkjsProofProviderTests.Given_InvalidArguments_When_TheProviderOrBackendIsUsed_Then_ItRefuses` (`snarkjs-proof-provider`, Order 2): a null backend or request throws `ArgumentNullException`, and a `NodeJsSnarkjsBackend` pointed at missing files throws `FileNotFoundException` without starting any process.

### Using real snarkjs

```csharp
var provider = SnarkjsProofProvider.CreateNodeJs(nodePath: "node", snarkjsPath: "snarkjs/cli.js");
var defaultProvider = SnarkjsProofProvider.CreateNodeJs();
```

`CreateNodeJs` only builds the provider; no process starts until `FullProveAsync` is called. Backed by `SnarkjsProofProviderTests.Given_NodeAndSnarkjsPaths_When_CreateNodeJsRuns_Then_AGroth16ProviderIsReturnedWithoutStartingAnyProcess` (`snarkjs-proof-provider`, Order 3). Calling `FullProveAsync` on it with a `ZkProofRequest` (`CircuitWasm`, `CircuitZkey`, `InputJson`) needs Node.js, snarkjs and real circuit artifacts, so that call is not run by any README example. The repository exercises it end to end in `CrossProviderTests.CrossProvider_SnarkjsWitness_RapidSnarkProof_BothVerify` (`tests/Nethereum.ZkProofs.RapidSnark.Tests`, trait `Native-Integration`), which proves the privacy-pool `commitment` circuit with snarkjs and again with rapidsnark and verifies both; it returns early with a skip message when the circuit artifacts are not present.

## Related providers

The other `IZkProofProvider` implementations are listed in [Nethereum.ZkProofs](../Nethereum.ZkProofs/README.md): for example `RapidSnarkProofProvider` (native) in `Nethereum.ZkProofs.RapidSnark` and `SnarkjsBlazorProvider` for the browser.
