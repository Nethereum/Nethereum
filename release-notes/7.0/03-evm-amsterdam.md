# EVM (Amsterdam) — Nethereum 7.0

Nethereum 7.0 rebuilds the EVM as one execution engine that runs everywhere — a live node, a `debug_traceTransaction`-style simulator, and a zkVM guest — all compiled from a single source tree. You can now execute a block or a transaction statelessly from a **witness** (the pre-state of only the accounts it touches), replay a transaction against live chain state over plain JSON-RPC, and prove a Prague or Osaka-family block inside the Zisk zkVM. The engine applies every fork's rules from Frontier through Amsterdam, chosen per block. Amsterdam lands whole: the EIP-8037 two-dimensional gas model, EIP-8038 state-access repricing, EIP-7928 block access lists, and the four EIP-7002/7251/8282 request predeploys are all executable and test-covered. The engine ships as two new packages — `Nethereum.EVM.Core` (the synchronous, AOT/trim-safe stateless engine) and `Nethereum.EVM.Precompiles` (the default crypto backends) — alongside a from-source overhaul of `Nethereum.EVM` and a new `Nethereum.EVM.Zisk` guest program (not packaged).

## Nethereum.EVM.Core — Stateless EVM Engine

New package: a synchronous, `Task`-free, AOT- and trim-safe EVM that executes a block or a transaction from a witness with no node, no database and no `async` machinery. This is the same source that `Nethereum.EVM` compiles as its async host build, and the exact engine the Zisk zkVM guest proves.

* Stateless block execution — `BlockExecutor.Execute(BlockWitnessData, IBlockEncodingProvider, HardforkRegistry, IStateRootCalculator, IBlockRootCalculator)` returns a `BlockExecutionResult` (receipts, `CumulativeGasUsed`, `StateRoot`, `TransactionsRoot`, `ReceiptsRoot`, `BlockAccessList`)
* Transaction and single-frame execution — `new TransactionExecutor(HardforkConfig).Execute(TransactionExecutionContext)` and `new EVMSimulator(config).ExecuteWithCallStack(Program)`
* Fork rules as a registry lookup, not a static global — `HardforkName` (chronological enum, `Frontier`…`Amsterdam`), `HardforkConfig` (the executable rule bundle), `HardforkRegistry`, and `MainnetHardforkRegistry.Build(PrecompileBackends)`; `Get` throws on `Unspecified` or an unregistered fork rather than falling back
* Per-block, per-chain fork resolution — `IChainActivations.ResolveAt(long blockNumber, ulong timestamp)`, `MainnetChainActivations.Instance`, and `ChainActivationsRegistry` that refuses an unknown chain id instead of silently replaying under mainnet rules
* The state seam is ten methods — `IStateReader` (`GetBalance`/`GetCode`/`GetStorageAt`/`GetTransactionCount`/`AccountExists`/`GetBlockHash`), with `InMemoryStateReader` and its `Strict` reader (missing read → `MissingWitnessDataException`), `ExecutionStateService`, and `WitnessRecordingStateReader.GetWitnessAccounts()` for recording a minimal witness
* EIP-7685 execution requests — `ExecutionRequests` (`CommitmentFor`, `ComputeRequestsHash`, request-type constants), `BlockExecutionRequests`, `DepositRequests` (EIP-6110), returning no `requests_hash` before Prague rather than the hash of an empty list
* System calls with the failure split the EIPs specify — `SystemCallContracts`, `SystemCallExecution`, and `SystemCallFailurePolicy` (request predeploys invalidate the block on failure/absence; EIP-4788 beacon-roots and EIP-2935 history "fail silently")
* Pluggable crypto and state tree — `PrecompileBackends` (`EcRecover`, `Sha256`, `Ripemd160`, `ModExp`, `Bn128`, `Blake2f`, `P256Verify`), `PrecompileRegistry` with copy-on-write `WithHandlers`/`WithGasCalculators`, and a pluggable `IStateRootCalculator` seam (the concrete `PatriciaStateRootCalculator` and EIP-7864 `BinaryStateRootCalculator` — over Blake3, Goldilocks-Poseidon2 or SHA-256 — ship in `Nethereum.CoreChain`)
* `eth_simulateV1` support in the engine — opt-in switches on `TransactionExecutionContext` / `ProgramContext`: fee settlement (`SettleTransactionFees`), fee cap below base fee (`AllowFeeCapBelowBaseFee`), `SkipNonceMaxCheck`, `PreserveZeroBaseFee`, precompile relocation (`PrecompileRelocations`) and a BLOCKHASH override for previously simulated blocks. All default off, so committing execution is unchanged
* The two arms are held identical by an instrument, not by discipline — `BlockExecutorArmsAgreeTests` splits `BlockExecutor.cs` at its `#if EVM_SYNC` directives and diffs the arms statement by statement, permitting exactly one pinned asymmetry

## Nethereum.EVM — Transaction Simulation, Tracing & Debugging

The host build of the same engine, reworked from source for 7.0. Point it at a node and it replays a transaction against real chain state; point it at a dictionary and it runs a contract with no node at all — either way you get the gas, the logs, the storage writes, the revert reason, a decoded call tree, and a source-level trace, without a tracing node and without broadcasting anything.

* End-to-end transaction simulation — `new TransactionExecutor(config).ExecuteAsync(TransactionExecutionContext)` handling intrinsic gas, validation, EIP-7702 setup, execution and refunds, with `TransactionExecutionResult` reporting `Success`, gas, `Logs`, `ProgramResult.IsRevert` and the pre-execution `TransactionError` code
* Live-chain state over ordinary JSON-RPC — `RpcNodeDataService : IStateReader, IAccountStorageReader`, whose reads map to `eth_getBalance`/`eth_getCode`/`eth_getStorageAt`/`eth_getTransactionCount`/`eth_getBlockByNumber` (plus `eth_getProof` and `debug_storageRangeAt`), including a mid-block constructor that reads state at a given transaction index
* Result decoding — `ProgramResultDecoder` turns a `ProgramResult` into `DecodedProgramResult` (`RootCall`, `DecodedLogs`, `ReturnValue`, decoded `RevertReason`, inner-call tree with `CallType`) using ABIs from `IABIInfoStorage`
* "Who gained and lost what" — `StateChangesExtractor : IStateChangesExtractor` produces `BalanceChange` records for native/ERC-20/721/1155 movements and cross-checks them against observed balances, surfacing `FeeOnTransfer`, `Rebasing` and `HasDiscrepancy`
* Source-level debugging — `EVMDebuggerSession` (breakpoints by file/line via `FindStepsForSourceLine`, stack/memory/storage per step, `GetCallInfoForStep`) and `program.CreateDebugSession(abiStorage, chainId)`
* Bytecode analysis — `ProgramInstructionsUtils.GetProgramInstructions`, disassembly, and function-selector detection (`ContainsFunctionSignature`)
* Fork presets and precompile composition — `DefaultHardforkConfigs` (`Frontier`…`Osaka`, `Default` == Osaka), `DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Amsterdam)`, and `RPC ↔ engine` bridging via `EvmTypeConversions`

## Amsterdam & Recent-Fork Execution

Amsterdam is executable and test-covered across the engine, and the Osaka/Fusaka blob-parameter forks that precede it are resolved and priced correctly. Every fork below is reached per block through `MainnetChainActivations` and the `HardforkRegistry` — there is no process-global fork.

* EIP-8037 two-dimensional gas — a transaction gets an execution-gas allowance **and** a state-gas reservoir; state growth is charged against the reservoir and only spills into execution gas when it runs dry (`GasConstants.EIP8037_*`, `StateGasAccount`, `StateGasMeter`, `TransactionExecutionResult.StateGasUsed`), and reports `StateGasUsed == 0` at Prague where the dimension does not exist
* EIP-8038 state-access repricing — cold-account access charged 3000 (not Berlin's 2600), plus SSTORE, call-value and delegation-access repricing at Amsterdam
* Transaction gas cap — the Osaka EIP-7825 rule rejects a declared gas limit above 2^24; at Amsterdam EIP-8037 restates the cap as an intrinsic-gas bound (`EIP8037_TX_MAX_GAS_LIMIT` = 16,777,216, rejected as `IntrinsicGasTooLow`)
* EIP-7928 block access lists — `BlockAccessListBuilder`, `BlockAccessListCollector`, size/structure rules, and `DeclaredBlockAccessList` validation against the executed list
* EIP-7002/7251/8282 request predeploys — an Amsterdam block system-calls all **four** request contracts (`WithdrawalRequests`, `ConsolidationRequests`, `BuilderDeposit`, `BuilderExit`); `SystemCallContracts.RequestContractsFor` returns two through Osaka and four from Amsterdam
* EIP-7843 slot number, EIP-7708 ETH-transfer logs, EIP-8246 selfdestruct-no-burn, EIP-8024 `DUPN`/`SWAPN`/`EXCHANGE`, EIP-7954 code-size limits — each with its own rule set
* Amsterdam is not Prague — a distinct rule set (SSTORE pricing they disagree about), its own per-fork blob schedule (not mainnet's BPO2), and a 23-field header the Amsterdam codec round-trips to the fixture's own hash
* Osaka / Fusaka BPO — `HardforkName.Osaka`, `OsakaBpo1`, `OsakaBpo2` resolved by timestamp with their own blob schedules, and the EIP-7951 `P256VERIFY` precompile at `0x0100` wired from Osaka (verified against EEST state-test vectors)
* EIP-7918 blob reserve price — `BlobGasCalculator.CalculateExcessBlobGas` gains the reserve-price branch (`BLOB_BASE_COST` = 8192), switched on per fork by `IBlobGasRule.AppliesReservePrice` (Osaka, OsakaBpo1, OsakaBpo2 and Amsterdam) and using the child block's blob schedule, so excess blob gas matches mainnet after the Osaka activation; `HardforkNames.Parse` also accepts `BPO1` / `BPO2`

## Nethereum.EVM.Precompiles — Default Crypto Backends

New package: the default managed .NET crypto backends behind Ethereum's precompiled contracts, plus a ready-to-use mainnet hardfork registry. The `Nethereum.EVM.Core` engine carries no crypto of its own; this is what wires it for a standard .NET host.

* `DefaultPrecompileBackends.Instance` — a `PrecompileBackends` bundle of `DefaultEcRecoverBackend` (0x01, `Nethereum.Signer` secp256k1), `DefaultSha256Backend` (0x02), `DefaultRipemd160Backend` (0x03), `DefaultModExpBackend` (0x05, EIP-2565), `DefaultBn128Backend` (0x06–0x08), `DefaultBlake2fBackend` (0x09, EIP-152) and `DefaultP256VerifyBackend` (0x0100, EIP-7951)
* `DefaultMainnetHardforkRegistry.Instance` — the pre-built `MainnetHardforkRegistry.Build(DefaultPrecompileBackends.Instance)`, every mainnet fork wired with default crypto
* `DefaultHardforkConfigs` — per-fork `HardforkConfig` accessors (`Cancun`/`Prague`/`Osaka`/…) for targeted single-fork tests
* `DefaultPrecompileRegistries` — standalone fork-scoped registry factories (`FrontierBase`, `ByzantiumBase`, `CancunBase`, `PragueBase`, `OsakaBase`) for hand-composing a registry, with `WithBlsBackend`/`WithKzgBackend` extension points
* Any backend is swappable by constructing your own `PrecompileBackends`; an address a fork registers but whose backend is not supplied becomes a `PlaceholderPrecompile` that throws `UnwiredPrecompileException` rather than returning a wrong answer

## Nethereum.EVM.Precompiles.Bls — EIP-2537 BLS12-381

The seven BLS12-381 precompiles (`0x0b`–`0x11`) EIP-2537 activates at Prague — G1/G2 addition and multi-scalar multiplication, pairing check, and the two field-to-curve maps — plugged into a `HardforkConfig` through a pluggable backend.

* `HardforkConfig.WithBlsBackend(IBls12381Operations)` and `PrecompileRegistry.WithBlsBackend(...)` — layer the seven handlers onto any base registry, returning a new instance (the original is unchanged); throws `InvalidOperationException` if the base `Precompiles` registry is null
* `Bls12381AwareMainnetHardforkRegistry.Build(...)` — a mainnet registry with BLS wired, leaving no Osaka precompile as an unwired placeholder
* Backend contract `IBls12381Operations` (`Nethereum.Signer.Bls`) with the native Herumi MCL implementation `Bls12381Operations` from `Nethereum.Signer.Bls.Herumi`; G1Add, G2Add and `map_fp_to_g1` executed against EIP-2537 vectors

## Nethereum.EVM.Precompiles.Kzg — EIP-4844 KZG

The EIP-4844 point-evaluation precompile (`0x0a`, activated at Cancun) plus the KZG operations needed to build blob transactions, delegating the cryptographic work to a pluggable backend.

* `HardforkConfig.WithKzgBackend()` / `WithKzgBackend(IKzgOperations)` and `PrecompileRegistry.WithKzgBackend(...)` — install the `0x0a` handler (gas 50000) on top of the Cancun base registry
* `CkzgOperations` — the default `IKzgOperations` over the native c-kzg-4844 bindings (`Ckzg.Bindings`), with `InitializeFromEmbeddedSetup()` and the embedded trusted setup
* Blob pipeline — `BlobSidecarBuilder.BuildFromData` builds a blob sidecar's KZG commitment (`BlobToKzgCommitment`), cell proof (`ComputeBlobKzgProof`) and versioned hash (`ComputeVersionedHash`)
* Coexists with a light-client build in the same process

## Nethereum.EVM.Contracts — Contract Simulators

High-level contract simulators built on `Nethereum.EVM` for testing and analysing contract behaviour without broadcasting. Refined for 7.0 against the reworked engine.

* `ERC20ContractSimulator(IWeb3, BigInteger chainId, string contractAddress, byte[] code = null, ChainForkResolver forkResolver = null)` — transfer and balance simulation against live or in-memory state
* `SimulateTransferAndBalanceStateAsync` → `TransferSimulationResult` (before/after balances from both storage and `balanceOf`, plus `TransferLogs`)
* `CalculateMappingBalanceSlotAsync` — reverse-engineers where a balance mapping is stored by simulating `balanceOf` and matching observed storage
* `SimulateGetBalanceAsync` / `SimulateTransferAsync` over a caller-supplied `ExecutionStateService`, so a sequence of simulations can share evolving state

## Nethereum.EVM.Zisk — zkVM Guest

New guest program (not packaged to NuGet): the bridge between `Nethereum.EVM.Core` and the [Zisk zkVM](https://0xpolygonhermez.github.io/zisk/). Compiled as the guest ELF, it reads a witness from Zisk's input channel, executes the `EVM_SYNC` build, and writes state-root / block-hash commitments to Zisk's output channel — a full stateless-execution-to-proof path in .NET.

* Guest entry point `ZiskBinaryWitness.Main` — reads the witness, validates the `BinaryBlockWitness.VERSION` (3) byte, builds a minimal single-fork `HardforkRegistry` (`BuildMinimalRegistry`), executes `BlockExecutor.Execute`, and emits the output slots (result flag, gas, block/state/transactions/receipts roots, pre-state root, block number, chain id, parent hash)
* Witness-backed crypto — `ZiskPrecompileBackends.Instance` routing `EcRecover`, `Sha256`, `ModExp`, `Bn128`, `Blake2f`, `P256Verify` (and managed Ripemd160) to native Zisk CSR operations, with `ZiskBls12381Operations`/`ZiskKzgOperations` layered on when the fork's precompile set declares BLS/KZG
* State-tree selection from the witness — `PatriciaStateRootCalculator`, or `BinaryStateRootCalculator` over Blake3, Goldilocks-Poseidon2 (`ZiskPoseidonHashProvider`, which wraps the Zisk native Poseidon2 accelerator with the parameters of the managed `GoldilocksPoseidon2HashProvider`), SHA-256 or Keccak
* Fork safety — the guest accepts only **Prague, Osaka, OsakaBpo1, OsakaBpo2**; any other fork (pre-Prague, and Amsterdam) is refused with output code 5 rather than proving a block under a neighbouring fork's rules

The Zisk guest proof path (build + emulator + proving) is exercised by a manual harness that requires the built RISC-V ELF and `ziskemu`; those tests skip when the ELF is absent and are not CI-gated. The CI-runnable coverage of the guest is the synchronous `EVM_SYNC` execution path itself.
