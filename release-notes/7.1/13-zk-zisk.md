# ZK / Zisk — Nethereum 7.1

Nethereum 7.1 moves the Zisk path that 7.0 introduced from the patched 0.17/0.18 toolchain to **Zisk 1.3.1-alpha**: the guest links Zisk's own `libziskos.a`, the stock release binaries prove and verify the ELF with no fork, no security-cookie patch and no `ziskfloat` rebuild, and the whole pipeline runs on the CPU. `cargo-zisk wrap --plonk` turns a STARK proof of a block into a PLONK proof that a Solidity contract verifies, and a block proof can be bound to the public inputs of an anchor contract: the guest commits `keccak256(AnchorInputs)` in its first eight public slots, so `ZiskVerifierAdapter` accepts a proof only for the inputs the guest committed to. The guest reports an invalid BN254 or BLS12-381 precompile input as a failed call instead of stopping, and reports a failing host accelerator as an `EvmHostException`. `Nethereum.ZkProofs.RapidSnark` ships Linux libraries that load on glibc 2.14 (x64) and 2.17 (arm64) and later, and the ZkProofs packages now carry their READMEs. The Zisk projects are in preview and nobody consumes them in production, so the changes below to `Nethereum.Zisk.Core`, the guest output layout and the witness format are made without a compatibility layer; they are listed under Breaking Changes. The witness format, the Solidity contracts and the CI lane are written up in their own slices and cross-referenced at the end of the package sections.

## Nethereum.Zisk.Core — Zisk 1.3.1 Bindings

`ZiskCrypto` binds the exports of the staticlib that Zisk 1.3.1 builds itself (`make libziskos`), following the zkvm-standards C header, and `ZiskMemoryMap` follows the Zisk 1.1+ memory layout.

- `ZiskCrypto.zkvm_bn254_pairing(byte[] pairs, nuint num_pairs, bool* verified)` and `ZiskCrypto.zkvm_bls12_pairing(byte[] pairs, nuint num_pairs, bool* verified)` return a `zkvm_status` (`0` is success) and the check through `verified`; `ZiskCrypto.syscall_poseidon2(ulong* state)` is the Poseidon2 permutation Zisk exports directly. Every other accelerator keeps its `zkvm_*` signature
- `ZiskMemoryMap.UartAddress` is `0xA040_0200` and `OutputBase` is `0xA041_0000`: Zisk 1.1+ puts a 4 MB stack and the system registers at the base of RAM. `ZiskIO`, `ZiskLog` and `ZiskOutput` write through those addresses
- `ZiskPoseidonHashProvider` builds the Goldilocks sponge over `syscall_poseidon2`
- The package now has its own `Description`, `PackageId` and packed `README.md`

## Nethereum.EVM.Zisk — Anchor Commitment and Failing-Input Backends

The guest entry point commits to the anchor inputs of its witness and no longer stops on a precompile input that is not a valid point. `Nethereum.EVM.Zisk` is not packed (`IsPackable` is `false`); it is compiled into the guest ELF with `-d EVM_SYNC -d EVM_ZISK`.

- `ZiskBinaryWitness.Main` writes `keccak256(block.AnchorInputs)` to public slots 0–7 when the witness carries anchor inputs (`AnchorBinding.CommitmentSlots` = 8) and writes every other output (status, gas, block hash, state root, transactions root, receipts root, transaction count, pre-state root, block number, chain id, parent hash) eight slots higher; a witness without anchor inputs keeps the slot layout of 7.0. A witness whose first byte is not `BinaryBlockWitness.VERSION` (4) stops the run with output code 3
- `ZiskBn128Backend` implements `IBn128OutcomeBackend` (`TryAdd`, `TryMul`, `TryPairing`) and `ZiskBls12381Operations` implements `IBls12381OutcomeOperations` (`TryG1Add`, `TryG1Msm`, `TryG2Add`, `TryG2Msm`, `TryPairing`, `TryMapFpToG1`, `TryMapFp2ToG2`). A non-zero accelerator status (an input that is not a valid point) is returned as `false`, which the precompile handler reports as a failed call; the guest has no exception handling, so nothing on the precompile input path throws. The throwing members (`Add`, `Mul`, `Pairing`, `G1Add`, ...) remain and throw `ArgumentException("... invalid input")`
- `ZiskSha256Backend`, `ZiskModExpBackend`, `ZiskBlake2fBackend` and `ZiskKzgOperations.ComputeVersionedHash` throw `EvmHostException` when the accelerator itself fails, not `ArgumentException`
- BN254 pairing and BLS12-381 pairing call `zkvm_bn254_pairing` and `zkvm_bls12_pairing`; the 7.0 `_check_c` exports are gone

## zisk/ — Zisk 1.3.1 Pipeline, Test Artifacts and Proving

- `zisk/scripts/build-libziskos.sh` runs Zisk's `make libziskos` on the release tag (default `v1.3.1-alpha`) and renames the four DMA routines to `__wrap_*` for bflat's `--wrap`; the eight source patches and the `poseidon2_c` trampoline of 7.0 are gone. `zisk/scripts/build.sh` compiles the `Compile` items of `Nethereum.EVM.Zisk.csproj` and lets bflat run `patch_elf`; `signal_patch` is no longer needed
- `zisk/scripts/wrap-plonk.sh <stark-proof> <plonk-output>` wraps a STARK proof into a PLONK proof in the `nethereum-zisk` image (`ci/Dockerfile.zisk`) and verifies it. `zisk/scripts/setup-host.sh` and `zisk/scripts/prepare-input.py` are removed: the image replaces the first, the `GenerateTestWitness` test class the second
- `zisk/test-artifacts/` is regenerated with Zisk 1.3.1-alpha and witness version 4: the guest ELF `nethereum_evm_elf` (5,838,600 bytes, sha256 `ff82bb51368df779e49211d207c03de4050c67841f411870d4d1391092ef0f7c`, built from the 7.1 source; two builds are byte-identical), ten witnesses (nine generic plus `witness_eth_transfer_anchor_standard.bin`), ten STARK proofs of about 415 KB and the two PLONK proofs `plonk_eth_transfer_noroot` (2,912 bytes) and `plonk_eth_transfer_anchor` (3,072 bytes). On a 24-thread Linux host, `cargo-zisk prove -y` proved and verified every witness: `eth_transfer_noroot` 2,898,761 steps in 475 s, `eth_transfer_anchor` 3,989,705 in 666 s, `eth_transfer_patricia` 3,985,714 in 475 s, `sstore_noroot` 2,952,542 in 460 s, `sstore_patricia` 4,082,405 in 463 s, `contract_creation` 3,961,228 in 702 s, `sha256_precompile` 4,107,712 in 461 s, `modexp_precompile` 4,156,166 in 475 s, `multi_tx_3transfers` 5,579,207 in 478 s, `multi_tx_mixed` 5,373,535 in 470 s. `cargo-zisk wrap --plonk` took 500 s for the generic proof and 341 s for the anchor proof, and needs the 25 GB SNARK key from `ziskup setup_snark`
- `zisk/README.md` and `zisk/test-artifacts/README.md` describe the 1.3.1 flow: execute, prove, verify, the PLONK wrap, the Solidity verifier, the verification checklist and the public-output layout with the anchor shift

## Nethereum.Zisk.Prover.Server and Nethereum.BlockProver.Server — Not Packed

Both are executable host projects and are now marked `IsPackable` `false`, so `dotnet pack` skips them.

## Nethereum.ZkProofs.RapidSnark — Linux Libraries for Older glibc

`runtimes/linux-x64/native/librapidsnark.so` and `runtimes/linux-arm64/native/librapidsnark.so` are rebuilt from rapidsnark v0.0.8 in an Ubuntu 20.04 container with a static libstdc++ and libgcc (`scripts/build-rapidsnark-linux.sh x64|arm64`). They required `GLIBC_2.38` and `GLIBCXX_3.4.32` before, so they failed to load on Debian 12, Ubuntu 22.04 and RHEL 9; the highest versions they now need are `GLIBC_2.14` (x64) and `GLIBC_2.17` (arm64), and they need no `GLIBCXX`. The exports are unchanged. The libraries grow to 2.8 MB (x64) and 2.6 MB (arm64), and the README lists the new sizes and the build script.

## Nethereum.ZkProofs, Nethereum.ZkProofsVerifier, Nethereum.ZkProofs.Snarkjs and Nethereum.ZkProofs.Snarkjs.Blazor — README in the Package

The four packages set `PackageReadmeFile` and pack `README.md`, as `Nethereum.ZkProofs.RapidSnark` and `Nethereum.CircomWitnessCalc` already did.

## Nethereum.ZkProofsVerifier, Nethereum.ZkProofs and Nethereum.PrivacyPools — Proof Verification Checks

The proof verifiers were audited over four rounds before 7.1, and each finding was fixed with a test that fails before the fix, including checks against real circuit proofs and allocation bounds.

- Groth16 public inputs must lie in [0, r); before this, a valid proof also verified with an input of x + r or x − r.
- snarkjs proof and verification-key parsing requires coordinates in [0, p), G1 points on the curve, and G2 points on the twist and in the r-order subgroup. `Groth16Verifier.Verify` called directly also checks the proof points.
- Every field element read from proof, key or public-signal JSON is at most 78 decimal digits (a 200,000-digit input had allocated about 890 MB). A public-input array is checked against the key's input count before any element is parsed, and the standalone parsers accept at most 1,024 values (`SnarkjsPublicInputParser.MaxPublicInputs`, `ZkProofResult.MaxPublicSignals`).
- `CircomGroth16Adapter.Verify(string proofJson, Groth16VerificationKey vk, string publicInputsJson)` verifies against a key parsed once, and `PrivacyPoolProofVerifier` parses each verification key once.
- `PoseidonMerkleTree` range-checks commitments, roots and siblings, and a value that does not fit 32 bytes throws instead of being truncated; before this, a proof for c also verified for c + r and c + 2^256.

## Shipped in Other Slices

- Witness version 4, `BlockWitnessData.AnchorInputs`, `AnchorBinding` and the requests hash through a caller-supplied SHA-256 (slice 03, EVM)
- `ZiskVerifier.sol`, `PlonkVerifier.sol`, `IZiskVerifier` with `bytes32` keys and the `ZiskVerifierAdapter` commitment check (slice 08, AppChain)
- The `Zisk` CI lane, `ci/Dockerfile.zisk`, `tests/Shared/ZiskHost.cs` and the `Category=Zisk` / `Category=ZiskVerifier` filters (CI and test environment)

## Bug Fixes

- `Nethereum.ZkProofs.RapidSnark` Linux libraries load on glibc 2.14+ (x64) and 2.17+ (arm64) (see above)
- The Zisk guest reads the UART and the output registers at the addresses Zisk 1.1+ uses; the 7.0 addresses fall inside the 4 MB stack that Zisk 1.1+ places at the base of RAM, not on the output registers
- The guest binds the pairing and Poseidon2 symbols that the 1.3.1 staticlib exports; the 7.0 names (`bn254_pairing_check_c`, `bls12_381_pairing_check_c`, `poseidon2_c`) existed only in the Nethereum-built library
- The Zisk guest builds again after the shared-helper merges: it links `EvmUInt256.Division.cs`, `StateRootAccountRules.cs`, `IBls12381OutcomeOperations.cs`, `TransactionDecodingExceptions.cs`, and leaves out `TrieNodeVerification.cs`
- `PrivacyPoolsE2EGethTests` carries `[FunctionOutput]` only on `WithdrawalContextParams`, the class it was written for

## Breaking Changes

- Groth16 verification and snarkjs parsing reject public inputs outside [0, r), off-curve or out-of-subgroup points, field elements over 78 digits and more than 1,024 public values; `PoseidonMerkleTree` throws for values outside the field or over 32 bytes when building a tree or a proof (verification returns `false`).

- `Nethereum.Zisk.Core.ZiskCrypto`: `bn254_pairing_check_c`, `bls12_381_pairing_check_c` and `poseidon2_c` are removed; use `zkvm_bn254_pairing` and `zkvm_bls12_pairing` (status plus `bool* verified`) and `syscall_poseidon2`
- `Nethereum.Zisk.Core.ZiskMemoryMap`: `UartAddress` `0xA000_0200` → `0xA040_0200` and `OutputBase` `0xA001_0000` → `0xA041_0000` (Zisk 1.1+ layout: 4 MB stack and system registers at the base of RAM). As `const` fields they are inlined, so an assembly compiled against 7.0 keeps the old addresses until it is rebuilt
- Guest public outputs: a witness with `AnchorInputs` puts `keccak256(AnchorInputs)` in slots 0–7 and every generic output moves up eight slots; a witness without anchor inputs keeps the 7.0 layout. A reader of the public values must know which kind of witness it proved
- `BinaryBlockWitness.VERSION` 3 → 4 (slice 03): the guest stops with output code 3 on any other version, so version-3 witnesses, the 7.0 guest ELF and every 7.0 proof must be regenerated
- `Nethereum.EVM.Zisk.Backends`: a failing `zkvm_sha256`, `zkvm_modexp`, `zkvm_blake2f` or KZG versioned-hash accelerator throws `EvmHostException` instead of `ArgumentException`; the BN254 and BLS12-381 throwing members throw `ArgumentException` with the message `<operation>: invalid input` instead of `<operation> failed (status n)`
- `Nethereum.Zisk.Prover.Server.ZiskProveBlockProver`: the constructor gained a trailing optional parameter, `ZiskProveBlockProver(string elfPath, string outputDir = null, string cargoZiskPath = "cargo-zisk", string provingKeySnarkPath = null, bool convertPathsForWsl = false, bool useWsl = false, int timeoutMs = 1800000, ILogger logger = null, Dictionary<string, string> environmentVariables = null, bool useGpu = false)`. An optional parameter changes the constructor's binary signature, so an assembly compiled against 7.0 must be recompiled
- `zisk/scripts/setup-host.sh` and `zisk/scripts/prepare-input.py` are removed
- Tests: `PRIVACY_POOLS_USE_GETH` no longer selects the external node for `PrivacyPoolsE2ETests`; run `PrivacyPoolsE2EGethTests` (`Category=ExternalNode`, `Requires=Externals`), which finds the node binary through `Tools:Geth` (`dotnet run ci/fetch-externals.cs` or `appsettings.test.json`)

## Build & Packaging

- `Nethereum.EVM.Zisk`, `Nethereum.Zisk.Prover.Server` and `Nethereum.BlockProver.Server` are `IsPackable` `false`; `dotnet pack src/Nethereum.EVM.Zisk` and `src/Nethereum.Zisk.Prover.Server` create no package
- `Nethereum.Zisk.Core` is packed with its README, `Description`, `AssemblyTitle` and `PackageId`
- `Nethereum.ZkProofs`, `Nethereum.ZkProofsVerifier` and `Nethereum.ZkProofs.Snarkjs.Blazor` pack their README
- `Nethereum.EVM.Zisk.csproj` defines `EVM_SYNC;EVM_ZISK` and links the shared sources the 1.3.1 guest needs (see Bug Fixes)
- `Nethereum.PrivacyPools.Circuits.Tests`, `Nethereum.PrivacyPools.CrossSdk.Tests` and `Nethereum.ZkProofs.RapidSnark.Tests` reference `src/Nethereum.ZkProofs.RapidSnark` (they referenced the published 6.0.4 and 6.1.0 packages, so they tested the shipped library, not the repository) and copy the RapidSnark and CircomWitnessCalc natives to the output; the external node path comes from `TestEnvironment.Tool("Geth")`; `CrossSdkTests` is tagged `Requires=Externals`; the CrossSdk script folder commits its `package-lock.json`
