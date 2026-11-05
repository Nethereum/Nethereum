# EVM — Nethereum 7.1

Nethereum 7.1 removes the EVM's per-call overheads and makes it stricter about what it accepts. Blob target, max and base-fee update fraction come from a chain blob schedule resolved per block, so the Fusaka BPO steps are data and mainnet stays on the `Osaka` fork across them. Every exceptional halt carries an `EvmHaltReason`, precompiles report invalid input as a value instead of an exception, and transactions with an invalid signature are rejected per fork. Call-frame snapshots are an O(1) revision journal, a `Program` executes on a byte program counter with contract analysis cached by code hash, and the hot opcodes (MODEXP, memory, LOG, SIGNEXTEND, the CALL family, CREATE2) lose their per-op allocations. The default composition serves `ecAdd` and `ecMul` with `ManagedBn254Backend`. About 2,640 vendored GeneralStateTests JSON files and the old in-project test engines are gone, replaced by the shared EEST conformance runner.

## Where the time went

The per-transaction and per-call cost of the engine was measured with an in-process opcode microbenchmark shaped after the execution-specs benchmark suite and a per-transaction cost benchmark, and each overhead that was not the opcode's own work was removed.

* Frames: a call-frame snapshot deep-copied every cached account, slot, warm set and transient slot, and is now an O(1) revision journal; every frame asked the operating system for a GUID, allocated and zeroed a 1024-slot operand stack, and built a transient-storage dictionary and blob-hash array it never used; a reverting call decoded its revert reason whether or not anyone read it
* Dispatch: every opcode awaited its gas rule and executor through allocated tasks; synchronous opcodes now run in a plain loop with fixed prices read from an array, and only calls, creates, asynchronous rules and traced frames take the general path
* Addresses and words: addresses, salts, init code, topics and stack words were converted to hex strings and parsed back at nearly every state-service boundary (warm checks, precompile resolution, `CREATE2`, `LOG`, transaction context); they now stay bytes. Memory copies are bulk copies, and `MSTORE8`, `MSTORE`, `MLOAD`, `POP`, `SIGNEXTEND`, `TLOAD` and `TSTORE` no longer allocate
* Precompiles: `ecAdd` and `ecMul` run on managed Montgomery arithmetic with a safegcd inversion; MODEXP runs Montgomery multiplication on 64-bit limbs; a transaction no longer adds every precompile address to its warm set

The rewritten parts are checked against independent oracles: the snapshot journal against a deep copy taken at each snapshot under random nested take, revert and commit sequences, in both the synchronous and asynchronous engines; `ManagedBn254Backend` byte for byte against the reference `bn256Add` and `bn256ScalarMul` vectors and against an independent affine `BigInteger` reference on 2,000 seeded cases in every CI run and 100,000 in the large-fixture lane; MODEXP against `BigInteger` on 20,000 seeded cases in every run and 1,000,000 in the large-fixture lane; and the 256-bit division and reduction against `BigInteger`.

## Nethereum.EVM.Core — Blob schedule

Blob parameters are data. A chain states them as a `BlobSchedule`, and each block's `HardforkConfig` takes the entry in force.

* `BlobParameters(int target, int max, int baseFeeUpdateFraction)`, `BlobScheduleEntry.ForFork(HardforkName, BlobParameters)` / `AtTimestamp(string name, ulong activationTimestamp, BlobParameters)`, `BlobSchedule(IEnumerable<BlobScheduleEntry>)` with `Empty`, `Entries`, `TimedEntries` and `BlobParameters? At(HardforkName protocolFork, ulong protocolForkActivatedAt, ulong timestamp)`
* `IChainBlobSchedule.BlobParametersAt(long blockNumber, ulong timestamp)`; the extension `IChainActivations.BlobParametersAt(...)`; implemented by `MainnetChainActivations`, `PreviewingChainActivations` and `ScheduledChainActivations` (new constructor taking a `BlobSchedule`, property `BlobSchedule`)
* `HardforkConfigExtensions.AtBlock(this HardforkConfig, IChainActivations, long blockNumber, ulong timestamp)` and `WithBlobParameters(this HardforkConfig, BlobParameters?)` return a cached clone with the scheduled target, max and update fraction (`ScheduledBlobGasRule`); `WithBlobParameters` returns the config itself when the parameters are `null`, when the fork has no blob rule (every fork before Cancun), or when the config already matches
* `MainnetBlobSchedule` holds the mainnet values (`Cancun`, `Prague`, `Osaka`, `Bpo1`, `Bpo2`, `Amsterdam`, `Instance`, `Bpo1Timestamp`, `Bpo2Timestamp`)
* `ChainForkSchedule` gains `BlobSchedule` (`List<BlobScheduleActivationEntry>`) and `ResolveBlobSchedule()`; a timed entry with `CountsTowardForkIdentity` feeds the fork identity
* The witness carries the resolved parameters in `BlockFeatureConfig.Blob`, so a guest executes with the host's schedule

## Nethereum.EVM.Core — Exceptional halts and the Program surface

A frame that halts records why, as a value, on both engines.

* `EvmHaltReason` (`None`, `ExceptionalHalt`, `OutOfGas`, `StackUnderflow`, `StackOverflow`, `InvalidOpcode`, `InvalidJumpDestination`, `WriteInStaticContext`, `OutOfBoundsRead`, `InvalidParameter`), `EvmHaltException(Reason)`, `Program.HaltReason`, `Program.MarkHalted(EvmHaltReason)`, `Program.Halt(EvmHaltReason)`
* `OutOfGasException`, `StaticCallViolationException` and `SStoreGasStipendException` derive from `EvmHaltException`; the async opcode loop catches that type only, so any other exception surfaces
* `Program` runs on a byte program counter and a PUSH-only jump bitmap; the instruction list is decoded on demand. New: `Program(byte[] bytecode, byte[] codeHash, ProgramContext = null)`, `Program.ForInitCode(...)`, `CurrentOpcodeByte`, `CurrentOpcode`, `CurrentImmediate`, `CurrentPushValue`, `CurrentImmediateByte`, `Advance(int)`
* Contract analysis is cached by keccak256 code hash in a least-recently-used map bounded to 64 MiB of estimated bytes; `Program.Instructions` returns a per-`Program` copy
* A frame that halts exceptionally on its last instruction no longer records a synthetic STOP on the synchronous arm

## Nethereum.EVM.Core — Transaction signatures and context

* `TransactionSignatureValidationRule` (`Frontier`, `Homestead`, `SpuriousDragon`) rejects `r` or `s` outside `[1, n-1]`, a high `s` from Homestead (EIP-2), an unprotected `v` other than 27/28 (EIP-155 `v` from Spurious Dragon), and a typed `y_parity` above 1, as `TransactionError.InvalidSignature`; `TransactionExecutionContext.Signature` is filled by `TransactionContextFactory.From`
* In `TransactionValidationRuleSets`, `Cancun`, `Prague` and `Osaka` check the chain id before the signature and before the blob and EIP-7702 authorisation-list checks; `Amsterdam` runs transaction type, signature, blob, maximum blobs per transaction, authorisation list and intrinsic gas cap, and checks the chain id last
* `TransactionExecutionContext.SenderAddress`, `ToAddress`, `CoinbaseAddress` (`EvmAddress`, parsed once), `BlobBaseFeeOverride`, `LogIndexCounter`; `ProgramContext.LogIndexCounter`
* `LogIndexCounter` and `IndexingEthTransferLogRule(IEthTransferLogRule inner, LogIndexCounter counter)` number ETH transfer logs from a block-wide counter
* `InnerCallResult.Reverted` and a lazily decoded `RevertReason`; `AccessSetWarmUp.WarmOriginPrecompilesAndCoinbase(...)` with `EvmAddress` parameters; `AccountDeployability.IsDeployable(...)` / `IsDeployableAsync(ExecutionStateService, EvmAddress)`
* STATICCALL touches its target, so an EIP-161-empty account it calls is deleted at the end of the transaction; a Tangerine Whistle SELFDESTRUCT prices the beneficiary by whether it exists in chain state

## Nethereum.EVM.Core — State journal

`ExecutionStateService.TakeSnapshot`, `RevertToSnapshot` and `CommitSnapshot` are O(1): the setters of `AccountExecutionState` and `AccountExecutionBalance` record a revision journal and a revert undoes only what the frame wrote.

* `ExecutionStateService.SetTransientStorage(EvmAddress, EvmUInt256, byte[])` and, in the async build, `CancellationToken`
* `IAddressAccessRecorder : IStateAccessRecorder` and `IAccountStorageAddressReader : IAccountStorageReader` are optional byte-native seams, checked with `is`; no shipped interface gained a member
* `GetCodeReadOnly` reads a target's code once per execution; an account whose chain code is empty is not read again

## Nethereum.EVM.Core — Precompile outcomes

Invalid precompile input is a value, in both engines.

* `PrecompileHandlerBase.TryExecute(byte[] input, out byte[] output)` (overridden by the built-in handlers, including BLS and KZG; `PlaceholderPrecompile` implements `IPrecompileHandler` directly and the registry runs its `Execute`); `Execute` is still the throwing form
* `PrecompileRegistry.TryExecute(int address, byte[] input, out byte[] output)`, `Handles(EvmAddress)`, `WithCallObserver(IPrecompileCallObserver)`; `IPrecompileCallObserver.OnCall(int address)`
* `IBn128OutcomeBackend` (`TryAdd`, `TryMul`, `TryPairing`); ECRECOVER and P256VERIFY (EIP-7951 range rule) reject without an exception
* A transaction no longer adds every precompile to its warm set; the registry answers EIP-2929 warmth from its own address set

## Nethereum.EVM.Core — Witness anchor binding

* `AnchorBinding.Encode(IReadOnlyList<EvmUInt256>)`, `Commitment(byte[])`, `WordSize`, `CommitmentSlots`; `BlockWitnessData.AnchorInputs`
* `BinaryBlockWitness.VERSION` is 4 (a trailer-flags byte marks the anchor trailer); a version 3 witness is refused
* `ExecutionRequests.CommitmentFor(...)` and `ComputeRequestsHash(...)` gain overloads taking a `Func<byte[], byte[]> sha256`; `BlockExecutor` passes the hardfork's SHA-256 backend

## Nethereum.EVM.Precompiles — Managed bn254 and Montgomery MODEXP

* `ManagedBn254Backend` (`Instance`, `Add`, `Mul`, `Pairing`, `TryAdd`, `TryMul`, `TryPairing`) serves `ecAdd` and `ecMul` through `BN128Curve.TryAdd` / `TryMul` in `Nethereum.Signer`; it is the bn128 backend of `DefaultPrecompileBackends.Instance` and every `DefaultPrecompileRegistries` base, so the follower, DevChain, AppChain and the conformance runner use it. `DefaultBn128Backend` implements `IBn128OutcomeBackend` and stays available
* `DefaultModExpBackend.ModExp(byte[], byte[], byte[])` runs Montgomery multiplication on 64-bit limbs for the odd modulus (or the odd part of an even modulus), with a 1-bit window when the exponent has 64 bits or fewer and a 4-bit window above that, and a truncated power for the power-of-two part of an even modulus, combined by the Chinese remainder theorem. An exponent of 8 bits or fewer uses `BigInteger.ModPow` for any modulus; a modulus of one returns zero and a zero exponent returns one. The targets without `BitOperations` use the `BigInteger` path. Gas pricing is unchanged
* The `ModExpHeaderParser` reads its length words with `EvmUInt256.FromBigEndian`

## Nethereum.EVM, Precompiles.Bls, Precompiles.Kzg, Contracts — Packaging

* `Nethereum.EVM`, `Nethereum.EVM.Core` and `Nethereum.EVM.Contracts` are strong-name signed; the three precompile packages pack their README and take `<Version>` from `$(NethereumVersion)`
* `Nethereum.EVM` links the Montgomery MODEXP sources and builds again for net451, net461 and netstandard2.0
* READMEs: Core (blob schedule, exceptional halts, precompile outcomes, signature rule, log index, anchor binding), EVM and Precompiles

## Bug Fixes

* A transaction with an invalid signature (`r`/`s` out of range, high `s`, bad `v`, bad `y_parity`) is rejected as `InvalidSignature` instead of executing
* STATICCALL touches its target, so an EIP-161-empty account it calls is deleted at the end of the transaction
* A Tangerine Whistle SELFDESTRUCT charges the new-account cost only when the beneficiary does not exist in chain state; previously every existing beneficiary was charged between Tangerine Whistle and Spurious Dragon
* A typed transaction's chain id is checked before its signature and the blob and authorisation-list checks in Cancun, Prague and Osaka, as EELS orders them
* An undefined opcode byte halts with `InvalidOpcode` and no longer reaches the loop as a null instruction; `StackPush` and `StackDup` no longer write past the 1024-slot stack after the overflow flag
* `TransactionExecutor` no longer settles an arbitrary exception as a failed transaction
* ECRECOVER, P256VERIFY and the BN254, BLS, KZG and BLAKE2F handlers return an empty or failed result for invalid input without relying on exceptions
* A frame that halts on its last instruction records no implicit STOP on the sync arm
* Contract code analysis is shared by code hash (previously per `byte[]` reference, which a fresh read defeated) and bounded by bytes, not entries
* `Program.Instructions` is a per-`Program` copy, so a debugger assigning source maps no longer changes every `Program` with the same code

## Breaking Changes

* `IStateSnapshot`, `StateSnapshot` and `AccountStateSnapshot` are removed. Use `ExecutionStateService.TakeSnapshot`, `RevertToSnapshot` and `CommitSnapshot`, which keep their signatures
* `MainnetChainActivations.ResolveAt` returns `HardforkName.Osaka` at and after the BPO1 and BPO2 timestamps; it no longer returns `OsakaBpo1` or `OsakaBpo2`. Read the BPO numbers with `BlobParametersAt` or `HardforkConfig.AtBlock`. The enum members and their specs remain
* The mainnet `ChainForkSchedule` (`MainnetChainSchedule.Instance`) no longer lists `OsakaBpo1` and `OsakaBpo2` among its `Schedule` activations. The BPO timestamps are now entries of `ChainForkSchedule.BlobSchedule` (`MainnetBlobSchedule.Instance.Entries` as `BlobScheduleActivationEntry`); read them with `ResolveBlobSchedule()`, `MainnetBlobSchedule.Bpo1Timestamp` / `Bpo2Timestamp`, or `BlobParametersAt` / `HardforkConfig.AtBlock`. They count toward the fork identity through `CountsTowardForkIdentity`
* `BinaryBlockWitness.VERSION` is 4; version 3 witnesses are refused and must be regenerated
* `TransactionValidationRuleSets.Homestead`, `Byzantium`, `Constantinople` and `Istanbul` are no longer the Frontier rule set: they validate signatures
* `OutOfGasException`, `StaticCallViolationException` and `SStoreGasStipendException` derive from `EvmHaltException` instead of `Exception`; catches by those types still bind
* `EvmPreCompiledContractsExecution` is `[Obsolete]`; use the precompile registry and backends
* The default bn128 backend is `ManagedBn254Backend`; code that compared `DefaultPrecompileBackends.Instance.Bn128` with `DefaultBn128Backend.Instance` sees a different instance

## Build & Packaging

* Test infrastructure: 2,642 vendored GeneralStateTests JSON files and one `stShift/.stub` under `tests/Nethereum.EVM.UnitTests/Tests/` and seven in-project engines (`BlockchainSyncTestRunner`, `BlockchainSyncTests`, `ChildProcessReaper`, `EthereumTestVectors`, `GethT8nRunner`, `PostStateSignatureClassifier`, `TraceComparer`) are removed; the EEST conformance runner owns that corpus
* New test projects: `Nethereum.EVM.Microbench` (execution-specs MODEXP shapes and single-opcode cases), `Nethereum.EVM.Precompiles.Bn254PortableTests` and `Nethereum.EVM.Precompiles.ModExpPortableTests` (the portable arithmetic paths)
