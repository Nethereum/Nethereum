# Nethereum 6.1.0

Nethereum 6.1.0 introduces a complete zero-knowledge proof infrastructure with native and browser-based Groth16 proof generation, the Privacy Pools SDK for private deposits and withdrawals with Association Set Provider support, new Merkle tree libraries (EIP-7864 Binary Trie, Sparse Merkle Trees with Poseidon hashing), SSZ model improvements, cross-platform BLS runtime updates, and demo applications showcasing the full ZK proof pipeline.

[Full Changelog](https://github.com/Nethereum/Nethereum/compare/6.0.4...6.1.0)

## Nethereum.ZkProofs — ZK Proof Abstractions

New provider-agnostic package defining the core interfaces and models for zero-knowledge proof generation in .NET. All proof providers (browser, native, HTTP) implement these abstractions, making them interchangeable.

* `IZkProofProvider` — central interface with `FullProveAsync(ZkProofRequest, CancellationToken)` and `Scheme` property
* `ZkProofRequest` — input model carrying circuit WASM, zkey, input JSON, witness bytes, and circuit graph data
* `ZkProofResult` — output model with `ProofJson`, `PublicSignalsJson`, and parsed `BigInteger[] PublicSignals`
* `ZkProofScheme` enum — Groth16, Plonk, Fflonk, Stark
* `ICircuitArtifactSource` — pluggable circuit artifact loading (WASM, zkey)
* `ICircuitGraphSource` — circuit graph data for native witness generation
* `FileCircuitArtifactSource` and `EmbeddedCircuitArtifactSource` — filesystem and in-memory artifact loading
* `CircuitArtifactLocator` — standard directory layout resolver for circuit artifacts
* `HttpZkProofProvider` — delegates proof generation to a remote HTTP prover endpoint
* `Groth16ProofConverter` — parse snarkjs JSON proofs and convert to Solidity-compatible format for on-chain verification

Commits: e28026f2

## Nethereum.ZkProofsVerifier — Pure C# Groth16 Verification

New package providing Groth16 proof verification using BN128 elliptic curve pairing — entirely in managed C# with no native dependencies. Directly consumes snarkjs/Circom JSON output for one-liner verification.

* `CircomGroth16Adapter.Verify(proofJson, vkJson, publicInputsJson)` — high-level verification API
* `Groth16Verifier` — BN128 pairing check implementation using BouncyCastle
* Snarkjs JSON parsers for proofs, verification keys, and public signals
* Returns structured `ZkVerificationResult` with `IsValid` and `Error` properties

Commits: 22fe04a6, 6b3858e8

## Nethereum.ZkProofs.Snarkjs — Node.js Proof Backend

New `IZkProofProvider` implementation that generates Groth16 proofs by invoking snarkjs via a Node.js child process. Suitable for server-side applications where Node.js is available.

Commits: 6d998f5b

## Nethereum.ZkProofs.Snarkjs.Blazor — Browser-Based Proof Generation

New `IZkProofProvider` implementation for Blazor WebAssembly applications. Calls snarkjs via JavaScript interop, enabling client-side proof generation entirely in the browser — private inputs never leave the user's machine.

* `SnarkjsBlazorProvider` — implements `IZkProofProvider` and `IAsyncDisposable`
* Loads snarkjs via `<script>` tag (UMD bundle) for broad compatibility
* Sends circuit WASM and zkey as base64 to JavaScript, returns proof JSON
* Configurable snarkjs URL (self-hosted or CDN)

Commits: 074a8576, e2a6a738

## Nethereum.CircomWitnessCalc — Native Witness Generation

New package providing native circuit witness computation via P/Invoke to iden3/circom-witnesscalc (C library). Takes a compiled circuit graph (.graph.bin) and input JSON, returns witness bytes (.wtns format).

* `WitnessCalculator.CalculateWitness(byte[] graphData, string inputsJson)` — static witness generation
* Cross-platform native libraries: win-x64, linux-x64, android-arm64
* Automatic runtime library resolution via `ModuleInitializer`
* NuGet package includes native DLLs with buildTransitive targets

Commits: ca762463

## Nethereum.ZkProofs.RapidSnark — Native Groth16 Proof Generation

New package providing native Groth16 proof generation via P/Invoke to iden3/rapidsnark (C++ library). Typically 10-50x faster than browser-based snarkjs, making it suitable for server-side proof generation, desktop wallets, and mobile apps.

* `RapidSnarkProver.Prove(byte[] zkeyBytes, byte[] witnessBytes)` — one-shot proof generation
* Reusable prover pattern: `LoadZkey()` once, `ProveWithLoadedZkey()` multiple times
* `RapidSnarkProofProvider` — implements `IZkProofProvider` interface
* Cross-platform native libraries: win-x64, linux-x64, android-arm64
* NuGet package includes native DLLs with buildTransitive targets

Commits: e0c27e11

## Nethereum.PrivacyPools — Complete Privacy Pools SDK

New comprehensive SDK for the Privacy Pools protocol (0xbow), enabling private deposits and withdrawals with Association Set Provider (ASP) compliance. Cross-compatible with the 0xbow TypeScript SDK.

* `PrivacyPoolAccount` — deterministic HD wallet-derived accounts from BIP-39 mnemonic
* `PrivacyPoolProofProvider` — orchestrates witness input construction, circuit artifact loading, and proof generation via any `IZkProofProvider`
* `PrivacyPoolProofVerifier` — verifies ragequit and withdrawal proofs using the pure C# BN128 verifier
* `PrivacyPoolService` — full protocol orchestration: deploy, deposit, withdraw, recover
* `PoseidonMerkleTree` — LeanIMT Merkle tree for on-chain commitment tracking
* `ASPTreeService` — Association Set Provider tree management
* ERC-20 token pool support alongside native ETH pools
* Direct withdrawal without ASP attestation (ragequit path)
* Switch to native proof generation pipeline (no Node.js required)
* Cross-platform E2E tests validated against 0xbow TypeScript SDK

Commits: 2ecc2a4e, 34885816, 922469a3, a45f7185, 616a1182

## Nethereum.PrivacyPools.Circuits — Embedded Circuit Artifacts

New package providing pre-compiled Privacy Pools Circom circuit artifacts as embedded resources. Implements `ICircuitArtifactSource` and `ICircuitGraphSource`.

* Commitment circuit: WASM, zkey, graph.bin, verification key JSON
* Withdrawal circuit: WASM, zkey, graph.bin, verification key JSON
* `PrivacyPoolCircuitSource` — loads artifacts from embedded assembly resources

Commits: 99884c11

## Nethereum.Merkle.Binary — EIP-7864 Binary Merkle Trie

New library implementing the EIP-7864 binary trie for stateless Ethereum execution.

* Stem-based structure with 256 colocated values per stem node
* BLAKE3 hashing for trie nodes
* Key derivation following the EIP specification
* Compact inclusion proofs for state verification

Commits: 022388a1, 54254f42

## Nethereum.Merkle — Sparse Merkle Trees and LeanIMT Improvements

Major additions to the Merkle library for ZK-circuit-compatible data structures.

* `SparseMerkleBinaryTree<T>` — ZK-optimised sparse Merkle tree with pluggable hashing
* `PoseidonSmtHasher` — Poseidon hash provider for ZK circuits
* `CelestiaSha256SmtHasher` — Celestia-compatible SHA-256 hasher
* Persistent storage support with lazy node loading
* `ILeanIMTNodeStorage` interface for external node persistence
* `PoseidonPairHashProvider` — Poseidon-based pair hashing for LeanIMT trees
* `CircomT1` and `CircomT2` Poseidon presets added to `Nethereum.Util`
* LeanIMT Poseidon Merkle tree integration tests

Commits: 7f276501, 513b9dbf, b24e2a07, b333caf8, b3f47d16, c34d42ff, 2504dc13

## Nethereum.Model.SSZ and Nethereum.Ssz — SSZ Improvements

New `Nethereum.Model.SSZ` library providing separate model types for SSZ serialisation, plus fixes and extensions to the core SSZ library.

* New Model.SSZ library with dedicated consensus-layer model types
* Merkleize padding fixes for correct tree hashing
* Progressive Merkleize operations for streaming data
* Union type support in SSZ encoding/decoding
* Test vectors validation

Commits: 7ff7557a, 08f40291, eb8c3e37, f2000795

## ZK Proof Demo Applications

Two working demo applications showcasing the full ZK proof pipeline with educational UI explaining what zero-knowledge proofs are, how Privacy Pools work, and what each step does.

* **Blazor WebAssembly Demo** (`src/demos/Nethereum.ZkProofs.Blazor.Demo/`) — browser-based proof generation via snarkjs JS interop, MudBlazor UI, with expandable educational panels
* **Avalonia Desktop Demo** (`src/demos/Nethereum.ZkProofs.Avalonia.Demo/`) — native proof generation via circom-witnesscalc + rapidsnark P/Invoke, Fluent dark theme, with per-step timing breakdown
* Both demos generate and verify Privacy Pools commitment proofs with the same circuit artifacts
* Educational content covers: ZK proof concepts, Privacy Pools protocol, Groth16 mechanics, public vs private signals, and Nethereum package roles

Commits: d004a566, 209ee61a

## Nethereum.Signer.Bls.Herumi — Runtime Updates

Updated BLS Herumi native package with additional platform runtimes for broader cross-platform support.

* Added osx-arm64 (Apple Silicon) native library
* Added osx-x64 native library
* Added linux-arm64 native library
* Updated Windows x64 DLL

Commits: cd944d0f, b398cdb9

## Nethereum.TokenServices — Pricing Improvements

* Added retry policy for pricing service calls with configurable retry count and delay
* Fixed Coingecko API compatibility between System.Text.Json and Newtonsoft.Json serialisation for numeric overflow handling

Commits: e46587c7, 29361b47

## Nethereum.Wallet.UI.Components.Maui — .NET 10 Support

* Updated to support .NET 10 `StaticWebAssetsEndPoints` for Maui projects

Commits: 535f7d46

## Nethereum.Mud.Repositories.Postgres — Runtime Configuration

* Updated runtime configuration for Postgres MUD repository

Commits: 80b8b838

## Nethereum.DevChain — Mapping Update

* Updated DevChain mapping configuration

Commits: 5af44f83

## Bug Fixes

* **Contract deployment race condition**: Fixed `DeployContractAndWaitForReceiptAsync` failing when contract code wasn't yet available at the deployed address. Now polls `eth_getCode` to verify deployment before returning.

Commits: bb5b2209

* **EIP-6963 personal_sign**: Fixed `personal_sign` passing a JSON string instead of the raw address parameter, which caused MetaMask to reject the signing request.

Commits: ce721746

## Build & Packaging

* **Version bump**: 6.0.4 → 6.1.0
* Added 10 new packages to NuGet build script: Merkle.Binary, Model.SSZ, ZkProofs, ZkProofsVerifier, CircomWitnessCalc, ZkProofs.RapidSnark, ZkProofs.Snarkjs, ZkProofs.Snarkjs.Blazor, PrivacyPools, PrivacyPools.Circuits
* Native packages (CircomWitnessCalc, ZkProofs.RapidSnark) use two-phase packaging: first to nativeartifacts, then standard NuGet pack

## Documentation

* New READMEs for `Nethereum.ZkProofs` and `Nethereum.ZkProofs.Snarkjs.Blazor` packages
* Updated Docusaurus documentation site with ZK proof packages in Consensus & Cryptography section
* New guide: "ZK Proof Generation Demos" covering browser vs native pipeline architecture
* Updated Privacy Pools guide with native proof generation alternative
* README validations and fixes across ZK and Merkle packages

Commits: 209ee61a, 1e7c83e2, f298386d, 548726d2, 54254f42, b3f47d16, c34d42ff, 99884c11
