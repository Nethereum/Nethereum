# ZK / Zisk — Nethereum 7.0

Nethereum 7.0 turns the EVM into something a zkVM can prove. The whole ZK-proof and Privacy Pools stack that headlined 6.1.0 — `Nethereum.ZkProofs`, the pure-C# Groth16 verifier, the snarkjs/rapidsnark/circom-witnesscalc backends, and the Privacy Pools SDK — ships in 7.0 essentially as it shipped in 6.1.0 and is **not** re-announced here. What is new is the **Zisk** path: `Nethereum.EVM.Core` cross-compiles to a RISC-V ELF (via NativeAOT/bflat) that runs inside the [Zisk zkVM](https://0xpolygonhermez.github.io/zisk/), where a witness — a block plus only the pre-state its transactions touch — executes and emits state-root and block-hash commitments, and `cargo-zisk` turns that execution into a STARK proof. One new NuGet package, `Nethereum.Zisk.Core` (the guest-side runtime bindings), carries the guest side. The host-side `Nethereum.Zisk.Prover.Server` (emulate/STARK/SNARK block provers) and `Nethereum.BlockProver.Server` (a continuous block-proving service) are new executable projects in the repository, not published to NuGet in 7.0, and sit alongside the `zisk/` build-and-proving pipeline at the repo root. The one shipped-in-6.1.0 package with a real code delta is `Nethereum.PrivacyPools`, which gains full 256-bit key derivation, migration-aware recovery, and artifact-integrity checking. The EVM-on-Zisk guest bridge itself (`Nethereum.EVM.Zisk`) is written up in the EVM release note and only cross-referenced here.

## Nethereum.Zisk.Core — Guest Runtime Bindings for the Zisk zkVM

New package: the low-level, AOT/trim-safe runtime that wires C# onto Zisk's execution environment. It is referenced by the guest ELF at build time — end users do not consume it directly — and it carries no LINQ, reflection or `BinaryFormatter` so it survives NativeAOT cross-compilation to `rv64ima`.

* Guest I/O channel — `ZiskInput` reads the serialised witness buffer the prover supplies, `ZiskOutput` is the generic public-output slot writer (`Set`/`SetBytes32`/`SetExitCode`; the `Nethereum.EVM.Zisk` guest entry point maps result flag, gas, and block/state/transactions/receipts roots into those slots), and `ZiskIO` / `ZiskLog` provide structured stdout for `ziskemu` runs (redacted in proof generation), with `ZiskMemoryMap` holding the fixed input/output/heap region constants
* AOT-safe byte-span serialisation — `ZiskBinaryReader` (a `ref struct`) and `ZiskBinaryWriter` replace `System.IO.BinaryReader`/`Writer` in the trimmed guest, and `ZiskBinaryWriter.ToStandardInputFormat()` wraps native witness bytes in the 24-byte header `cargo-zisk prove -i` requires
* Accelerator P/Invoke surface — `ZiskCrypto`, a static class of `[DllImport("__Internal")]` bindings statically linked from `libziskos.a`: the C# entry points are `zkvm_keccak256`, `zkvm_sha256`, `zkvm_secp256k1_ecrecover`, `zkvm_modexp`, the `zkvm_bn254_*` / `zkvm_bls12_*` curve+field ops plus `bn254_pairing_check_c` / `bls12_381_pairing_check_c`, `zkvm_blake2f`, `zkvm_kzg_point_eval`, `zkvm_secp256r1_verify` and `poseidon2_c` — each wrapping a native symbol that issues Zisk CSR hardware instructions for near-zero proving cost
* `IHashProvider` implementations for the EIP-7864 binary state trie — `ZiskKeccakHashProvider` and `ZiskSha256HashProvider` delegate to `zkvm_keccak256` / `zkvm_sha256` (CSR 0x800 / 0x805), while `ZiskPoseidonHashProvider` builds the Goldilocks sponge itself in C# (absorb, `poseidon2_c` permute, squeeze) because Zisk exports only the raw permutation
* `Ripemd160` — a managed RIPEMD-160 used as the fallback for precompile `0x03`, which has no Zisk accelerator

The package README was rebuilt from source; it points to the `zisk/` build orchestration (`zisk/scripts/setup-host.sh`, `zisk/scripts/build.sh`).

## Nethereum.Zisk.Prover.Server — Emulate and STARK/SNARK Block Provers

New executable project (not packaged): two `IBlockProver` implementations that drive the Zisk toolchain over a built EVM ELF, plus a minimal-API host that exposes proving over HTTP. (`IBlockProver` and `BlockProofResult` are defined in `Nethereum.CoreChain`, so a prover is swappable independently of the proving backend.)

* `ZiskEmuBlockProver` — runs the witness through `ziskemu` (fast; it produces no zk proof: `ProofBytes` is a SHA-256 integrity digest of the pre-state root, post-state root and witness hash), parses the guest's `BIN:state_root=` / `BIN:block_hash=` / `BIN:OK gas=` output lines, cross-checks the prover-computed state root against the expected post-state root, and returns a `BlockProofResult` with `ProverMode = "Emulate"`; the ELF is SHA-256-fingerprinted into `ElfHash` so a proof is bound to the binary that produced it
* `ZiskProveBlockProver` — runs `cargo-zisk prove` to generate a STARK proof (`ProverMode = "Zisk"`), with an optional PLONK SNARK wrap step (`cargo-zisk wrap-proof --plonk`) when a proving key is configured; sets `HWLOC_COMPONENTS=-gl` to survive headless proving, and supports WSL path translation for Windows hosts
* HTTP prover host (`Program.cs`) — selects the backend from `ProverMode` (`Emulate` / `Prove` / `Mock`) and serves `POST /prove` taking a base64 `ProveBlockRequest` and returning a `ProveBlockResponse` (both defined in `Nethereum.CoreChain`)

The actual proving path requires a built RISC-V ELF plus `ziskemu` / `cargo-zisk`; it is validated through the `zisk/` harness (see below), not in CI. STARK proving is the working target; the PLONK SNARK wrap runs only when a proving key is configured.

## Nethereum.BlockProver.Server — Continuous Block-Proving Service

New executable project (not packaged): a hosted service that watches a witness store and proves blocks as they arrive, with a queue, a cadence, retries, retention and metrics — the piece that makes proving a running system rather than a one-shot CLI call. It composes the storage/queue/cadence types from `Nethereum.CoreChain` with any `IBlockProver`.

* `BlockProverProcessingService` — the prove loop: it dequeues explicit proof requests, otherwise auto-enqueues unproven blocks per the configured cadence, proves each with bounded retries and exponential backoff, records the state-root / block-hash verification result, upserts block progress, and applies the witness-retention policy after a successful proof
* `BlockProverOptions` — `Enabled` (proving is **opt-in**, off by default), `PollIntervalMs`, `MaxRetries`, `RetryDelayMs`
* Hosting glue — `BlockProverHostedService` (a `BackgroundService` wrapper), `BlockProverServiceCollectionExtensions.AddBlockProverOptions`, and `BlockProverEndpoints.MapBlockProverEndpoints` exposing `/status`, `/witness/{n}`, `/queue/{n}` (+ `/retry`), `/queue` and `/proof/{n}`
* `BlockProverMetrics` — an `IDisposable` metrics sink tracking last-proven block, queue depth, retries and failures

The loop is fully CI-tested against `MockBlockProver` (queue processing, progress-across-restart, cadence, retention, retry-then-succeed, give-up-after-max-retries, explicit requests, failed-request status, metrics). The default host wires an in-memory witness store, request queue and progress repository and selects a `Mock` or `Remote` (`HttpBlockProverClient`) prover.

## zisk/ — EVM-to-RISC-V Build and Proving Pipeline

New build-orchestration directory at the repo root: the tooling that compiles `Nethereum.EVM.Core` + `Nethereum.EVM.Zisk` + `Nethereum.Zisk.Core` down to a Zisk-runnable RISC-V ELF and proves it. It is scripts, patches, a documented pipeline, and pre-built verification artefacts — not a shipped NuGet package.

* `zisk/scripts/setup-host.sh` — one-shot host installer (system deps, .NET 10 SDK, Rust, the Zisk emulator + prover via `ziskup`, and the `nethereum/bflat-riscv64` Docker image)
* `zisk/scripts/build-libziskos.sh` — builds `libziskos.a` from Zisk upstream inside Docker, applying seven documented patches so the Rust zkVM runtime co-exists with bflat/NativeAOT (static-lib output, `no_entrypoint`, shared allocator, `--wrap` DMA symbols) and appending a `poseidon2_c` trampoline for binary-trie state roots
* `zisk/scripts/build.sh` — cross-compiles the hand-curated EVM source set to an ELF with `bflat` (`-d EVM_SYNC`, size-optimised, static), then post-processes with `patch_elf` and `signal_patch`; `--source` (default) and `--dll` modes documented with their trade-offs
* Pre-built, verified artefacts in `zisk/test-artifacts/` — an EVM ELF, witnesses in standard input format, and STARK proofs (~329 KB each) for eth-transfer, SSTORE, contract-creation, modexp/sha256 precompiles and multi-tx blocks, cross-validated so the `ziskemu` state roots match the managed .NET EVM (`PatriciaStateRootCalculator` / `BinaryStateRootCalculator`)
* The README documents the full CSR precompile map, the Ethereum-precompile → Rust `_c` → CSR wiring, the three input framing formats, the proving cost profile, and the Nethereum Zisk-fork constraint fixes (`Nethereum/zisk` branch `fix/nativeaot-constraints`) that NativeAOT ELFs require

## Nethereum.PrivacyPools — Safe 256-bit Keys, Migration-Aware Recovery, Artifact Integrity

Shipped in 6.1.0; the only 6.1.0-shipped ZK package with a substantive 7.0 code delta (+463 / −77, 3 commits), tracking 0xbow SDK v1.2.0. The 6.1.0 capabilities are unchanged and not re-listed.

* Full 256-bit key derivation — `new PrivacyPoolAccount(mnemonic)` derives master keys through `BytesToBigInt` (all 32 bytes preserved), replacing the lossy 53-bit `BytesToBigIntViaDouble` path now reachable only via `PrivacyPoolAccount.CreateLegacy`; both paths remain so pre-v1.2.0 ("legacy") deposits stay recoverable
* Migration-aware recovery — `PrivacyPoolAccountRecovery.RecoverAccounts` / `DiscoverMigratedCommitments` follow a legacy deposit through its on-chain migration into a safe-key commitment chain, marking the migrated legacy leg `IsMigrated` / unspendable and surfacing the spendable safe tail; `PrivacyPool.RecoverAccounts` refuses to run without a legacy context (so migrated funds are never silently skipped), while `PrivacyPool.RecoverSafeAccounts` recovers safe-only accounts when `LegacyAccount` is null
* Artifact integrity — `UrlCircuitArtifactSource` verifies every downloaded/cached circuit artifact (WASM, zkey, vkey) against a SHA-256 allow-list and refuses to load an unverified or mismatched file, with the canonical hashes in `CircuitArtifactHashes.Default`

## Nethereum.EVM.Zisk — zkVM Guest Bridge (written up in the EVM release note)

The bridge between `Nethereum.EVM.Core` and the Zisk guest runtime — `ZiskBinaryWitness` guest entry point, witness-backed precompile backends, and state-tree selection — is a new-in-7.0 guest-ELF project (not packaged) documented in the EVM release note, where the engine it proves lives. It is listed here only so the ZK inventory is complete; see the EVM release note for its API and traceability.

## Shipped in 6.1.0 with no 7.0 code delta

For completeness, the remaining ZK packages that headlined 6.1.0 carry no shipped-code change between `6.1.0` and HEAD and are **not** re-announced:

* `Nethereum.ZkProofs`, `Nethereum.ZkProofsVerifier`, `Nethereum.ZkProofs.RapidSnark`, `Nethereum.ZkProofs.Snarkjs`, `Nethereum.PrivacyPools.Circuits` — zero commits and an empty `6.1.0..HEAD` diff
* `Nethereum.ZkProofs.Snarkjs.Blazor` and `Nethereum.CircomWitnessCalc` — README-only deltas (a verifier-package install note; a `gw_free` C-API doc line), no code change
* The ZK demo apps `src/demos/Nethereum.ZkProofs.Blazor.Demo` and `Nethereum.ZkProofs.Avalonia.Demo` — no delta since 6.1.0
