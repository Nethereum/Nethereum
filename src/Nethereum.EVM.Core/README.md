# Nethereum.EVM.Core

A synchronous, `Task`-free, AOT- and trim-safe EVM. Execute a block or a transaction from a **witness** — the pre-state of only the accounts the block touches — with no node, no database and no `async` machinery. This is the engine the Zisk zkVM guest proves.

## What you can do with it

- **Execute a block statelessly** from a witness and get the receipts, the gas and the post-state root.
- **Re-execute a transaction** anywhere you can carry its pre-state — inside a zkVM, an enclave, or a trimmed AOT binary.
- **Record the minimal witness** a block needs, by wrapping any state reader and capturing exactly what execution touched.
- **Catch a witness gap immediately** — a strict reader turns a missing account or slot into an exception instead of a silent zero.
- **Execute under any fork's rules**, Frontier to Amsterdam, resolved per block rather than per process.
- **Swap the crypto** — supply your own ecrecover, SHA-256, modexp, BN128, Blake2f and P-256 backends, which is how a guest gets circuit-friendly implementations.
- **Commit to EIP-7685 execution requests** and account for the EIP-8037 state-gas dimension.
- **Choose the state tree** — Patricia, or the EIP-7864 binary tree with Blake3 or Poseidon.

Most applications want [`Nethereum.EVM`](../Nethereum.EVM/README.md) instead — the asynchronous build of this same engine, with RPC-backed state, decoding and a source-level debugger. Reach for `Nethereum.EVM.Core` directly for zkVM guests, AOT or trimmed hosts, and the minimum dependency footprint.

## Quick start

Execute a block. You hand `BlockExecutor` a **witness** — the block header fields, the signed transactions, and the pre-state of every account they touch — and it returns the receipts, the gas and the post-state root.

*From `QuickStartDocExampleTests.ExecuteABlockFromAWitness` (use case `block-execution`) — a tagged, passing test.*

```csharp
var block = new BlockWitnessData
{
    BlockNumber = 1,
    Timestamp = 1_000_000,
    BaseFee = 7,
    BlockGasLimit = 30_000_000,
    ChainId = 1,
    Coinbase = "0x0000000000000000000000000000000000000000",
    Difficulty = new byte[32],
    ParentHash = new byte[32],
    ExtraData = new byte[0],
    MixHash = new byte[32],
    Nonce = new byte[8],
    ComputePostStateRoot = true,
    Features = BlockFeatureConfig.Prague,
    Transactions = new List<BlockWitnessTransaction>
    {
        TestTransactionHelper.CreateSignedContractCall(
            ContractAddress, new byte[0], EvmUInt256.Zero, 0, 10, 100_000, SenderKey)
    },
    Accounts = new List<WitnessAccount>
    {
        new WitnessAccount { Address = sender, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
        new WitnessAccount { Address = ContractAddress, Balance = EvmUInt256.Zero, Nonce = 0, Code = storesFortyTwoInSlotZero, Storage = new List<WitnessStorageSlot>() }
    }
};

var result = BlockExecutionHelper.ExecuteBlock(block);

Assert.True(result.TxResults[0].Success, result.TxResults[0].Error);
Assert.True(result.CumulativeGasUsed > 21_000);
Assert.NotNull(result.StateRoot);
```

`BlockExecutionHelper` lives in `tests/Nethereum.EVM.Core.Tests`, not in the package, and it is not a one-liner over the real entry point:

```csharp
public static BlockExecutionResult Execute(
    BlockWitnessData block,
    IBlockEncodingProvider encodingProvider,
    HardforkRegistry hardforkRegistry,
    IStateRootCalculator stateRootCalculator = null,
    IBlockRootCalculator blockRootCalculator = null)
```

The fork is taken from `block.Features.Fork` and looked up in the registry — there is no ambient global to set. Before calling `Execute`, the helper does four things a consumer would otherwise have to do:

- it defaults `block.Features.Fork` to `HardforkName.Prague` when the witness leaves it `Unspecified`;
- it chooses the state-root calculator from `Features.StateTree` and `Features.HashFunction` — `PatriciaStateRootCalculator`, or `BinaryStateRootCalculator` over Blake3, Goldilocks-Poseidon2 or SHA-256;
- it passes a `PatriciaBlockRootCalculator` only when `ProduceBlockCommitments` is set;
- it calls `block.AddRequestPredeploys()`, adding a code-bearing account for every address in `SystemCallContracts.RequestContractsFor(fork)`.

That last step is not cosmetic. From Prague the block system-calls each request predeploy, and EIP-7002 requires that *“if there is no code at `WITHDRAWAL_REQUEST_PREDEPLOY_ADDRESS`, the corresponding block MUST be marked invalid”* — EIP-7251 and EIP-8282 say the same for the other three. Hand the quick-start witness straight to `BlockExecutor.Execute` and it is refused, because the predeploys are not in it. `AddRequestPredeploys` is a test-project extension, so a consumer either seeds those accounts in the witness or supplies them from real state.

## Entry points

**Start with `BlockExecutor`.** It is the only type most consumers need; everything below it is reachable through the `HardforkRegistry` you hand it.

| I want to… | Reach for |
|---|---|
| **Execute a whole block from a witness** | **`BlockExecutor.Execute(block, encodingProvider, hardforkRegistry, …)`** |
| Execute one transaction | `new TransactionExecutor(config).Execute(ctx)` |
| Run raw bytecode, one frame | `new EVMSimulator(config).ExecuteWithCallStack(program)` |
| Get the rules for a fork | `MainnetHardforkRegistry.Build(backends)` then `registry.Get(HardforkName.Prague)` |
| Work out which fork a block is on | `MainnetChainActivations.Instance.ResolveAt(blockNumber, timestamp)` |
| Supply state to the engine | implement `IStateReader`, or use `InMemoryStateReader` over a witness |
| Record a minimal witness while executing | wrap your reader in `WitnessRecordingStateReader`, then `GetWitnessAccounts()` |

`HardforkConfig` is the bundle of rules for one fork; `HardforkRegistry` maps a `HardforkName` to it. You build the registry once at startup and the executor is a dumb lookup.

## Installation

```bash
dotnet add package Nethereum.EVM.Core
```

The engine carries no crypto backends. `DefaultPrecompileRegistries`, used in the `WithPrecompiles` example below, and the ready-made `DefaultMainnetHardforkRegistry.Instance` both live in `Nethereum.EVM.Precompiles`, which references this package:

```bash
dotnet add package Nethereum.EVM.Precompiles
```

## The two arms: one source, two engines

Every async API in the engine ships a synchronous twin behind `#if EVM_SYNC`. The two arms are written side by side in the same file:

| Concern | `EVM_SYNC` (guest, this package) | `!EVM_SYNC` (host, `Nethereum.EVM`) |
|---|---|---|
| Frame execution | `EVMSimulator.ExecuteWithCallStack(Program program, int vmExecutionCounter = 0, int depth = 0, bool traceEnabled = true)` | `ExecuteWithCallStackAsync(...)` — same parameters, returns `Task<Program>` |
| Transaction | `TransactionExecutor.Execute(TransactionExecutionContext ctx)` | `ExecuteAsync(TransactionExecutionContext ctx)` |
| Block | `BlockExecutor.Execute(...)` | `BlockExecutor.ExecuteAsync(...)` |
| State reads | `IStateReader.GetBalance` / `GetCode` / `GetStorageAt` / `GetTransactionCount` / `AccountExists` / `GetBlockHash` | the same six, `…Async`, returning `Task<T>` |
| Log type | `ProgramResult.Logs` is `List<EvmLog>` | `List<FilterLog>` (`Nethereum.RPC.Eth.DTOs`) |
| Call input | `ProgramContext(EvmCallContext callInput, …)` only | that, plus a `ProgramContext(CallInput callInput, …)` overload that converts an RPC `CallInput` |
| Signature recovery | authorities are pre-recovered into the witness (`BlockWitnessTransaction.AuthorisationAuthorities`) | `Nethereum.Signer` recovers them (`Eip7702AuthorizationApplication`) |

Written out, the two arms sit side by side in the same file:

```csharp
#if EVM_SYNC
    public Program ExecuteWithCallStack(Program program, int vmExecutionCounter = 0, int depth = 0, bool traceEnabled = true)
#else
    public async Task<Program> ExecuteWithCallStackAsync(Program program, int vmExecutionCounter = 0, int depth = 0, bool traceEnabled = true)
#endif
    {
        // one shared body
    }
```

The reason for the split is proving cost: the .NET `Task` state machine, `System.Linq`, and reflection are all expensive or unavailable inside a zkVM guest, but the *consensus rules* must be identical or a proof would attest to a different chain than the node executed.

### The arms are held identical by a test, not by discipline

`tests/Nethereum.EVM.UnitTests/Execution/BlockExecutorArmsAgreeTests.cs` reads `src/Nethereum.EVM.Core/Execution/BlockExecutor.cs`, splits it at its `#if` / `#endif` directives, normalises both arms, and compares them **statement by statement, in order**:

```csharp
var arms = ReadArms();
var difference = Diff(arms.Sync, arms.Async, naiveSemicolonJoin: false);

var only = Assert.Single(difference.SyncOnly);
Assert.Equal(DeclaredBlockAccessListCheckUnit, only);
Assert.Empty(difference.AsyncOnly);
Assert.True(IsTheOneExpectedAsymmetry(difference));
```

*(tagged `[NethereumDocExample(DocSection.EvmSimulator, "sync-and-async-arms", …)]`, passing.)*

Exactly **one** statement is allowed to be sync-only — the `DeclaredBlockAccessListCheck` assignment — and that allowance is itself pinned: a companion test deletes it and asserts the now-identical arms are *rejected*, so the exception cannot silently widen. Four further tests in the same file prove the instrument would catch a stray statement, a duplicated member, a naive-join blind spot, and a statement merely **moved** within one arm.

If you change one arm of `BlockExecutor`, change the other in the same edit. The build will not stop you; this test will.

## Hardforks: a registry lookup, not a static global

`HardforkName` is a chronological enum:

```csharp
public enum HardforkName
{
    Unspecified = 0,
    Frontier,
    FrontierThawing,
    Homestead,
    DaoFork,
    TangerineWhistle,
    SpuriousDragon,
    Byzantium,
    Constantinople,
    Petersburg,
    Istanbul,
    MuirGlacier,
    Berlin,
    London,
    ArrowGlacier,
    GrayGlacier,
    Paris,
    Shanghai,
    Cancun,
    Prague,
    Osaka,
    OsakaBpo1,
    OsakaBpo2,
    Amsterdam
}
```

The consensus-only forks (`FrontierThawing`, `DaoFork`, `MuirGlacier`, `ArrowGlacier`, `GrayGlacier`) exist for naming completeness and are registered as aliases of the parent fork's EVM rules by `MainnetHardforkRegistry.Build`. `HardforkNames.Parse(string name)` accepts the enum names case-insensitively plus the test-fixture aliases `EIP150`, `EIP155`, `EIP158`, `ConstantinopleFix`, `Merge`, `MergeNetSplitFork`.

**`HardforkConfig`** is the executable bundle for one fork. Its properties are the rule sets the executor consults:

| Property | Type |
|---|---|
| `IntrinsicGasRules` | `IntrinsicGasRules` |
| `OpcodeHandlers` | `OpcodeHandlerTable` |
| `Precompiles` | `PrecompileRegistry` |
| `CallFrameInitRules` | `CallFrameInitRules` |
| `TransactionValidationRules` / `TransactionSetupRules` | rule sets |
| `CodeDepositRule`, `ContractCreationMaterialiseRule`, `BlockHashRule`, `SstoreRefundRule`, `EthTransferLogRule`, `TouchedEmptyCleanupRule`, `ReceiptConstruction` | one-rule seams |
| `GasForwarding` | `IGasForwardingCalculator` (default `Eip150GasForwarding.Instance`) |
| `MaxCodeSize`, `MaxInitcodeSize`, `MaxBlobsPerBlock`, `RefundQuotient`, `SstoreClearsSchedule`, `SstoreSetRefund`, `SstoreResetRefund`, `ContractInitialNonce`, `SystemCallGas` | scalars |
| `RejectEfPrefix`, `CleanEmptyAccounts`, `BaseFeeApplies`, `EnforceSstoreGasStipend`, `WarmCoinbase` | flags |
| `ReceiptCodec`, `HeaderCodec`, `TransactionDecoder` | `Nethereum.Model.Codecs` seams |

`HardforkConfig` also exposes a lazily-built static preset per fork (`HardforkConfig.Frontier` … `HardforkConfig.Amsterdam`) and `Clone()`.

**`HardforkRegistry`** maps `HardforkName` → `HardforkConfig` (`Register`, `Remove`, `Contains`, `TryGet`, `Get`, `RegisteredNames`). `Get` **throws** on `HardforkName.Unspecified` with a message naming the producer bug, and throws for an unregistered fork — it never falls back.

Build one per chain with `MainnetHardforkRegistry.Build(PrecompileBackends backends)`. The crypto backends come from `Nethereum.EVM.Precompiles`, `Nethereum.EVM.Zisk`, or your own implementation.

### Chain-ID dispatch

`IChainActivations` is one method: `HardforkName ResolveAt(long blockNumber, ulong timestamp)`.

`MainnetChainActivations.Instance` carries the mainnet table as public constants — block heights `FrontierThawingBlock` 200_000 through `ParisBlock` 15_537_394, then timestamps `ShanghaiTimestamp` 1_681_338_455, `CancunTimestamp` 1_710_338_135, `PragueTimestamp` 1_746_612_311, and the nullable `OsakaTimestamp` 1_764_798_551, `OsakaBpo1Timestamp` 1_765_290_071, `OsakaBpo2Timestamp` 1_767_747_671. (`MainnetHardforkActivations.ResolveAt` is a static shorthand for the same instance.)

`ChainActivationsRegistry` maps chain id → activations. Mainnet (chain 1) is pre-registered by the constructor; L2s, testnets and AppChains register their own. An unknown chain id **throws** — a silent fallback would replay transactions under the wrong fork's rules.

*From `EvmSimulatorDocExampleTests.AnUnregisteredChainIdIsRefused` — tagged, passing.*

```csharp
var registry = new ChainActivationsRegistry();

Assert.Equal(
    HardforkName.Cancun,
    registry.ResolveAt(chainId: 1, blockNumber: 19_426_587, timestamp: MainnetChainActivations.CancunTimestamp));

Assert.Throws<System.InvalidOperationException>(
    () => registry.ResolveAt(chainId: 424242, blockNumber: 1, timestamp: 1));
```

## The precompile registry

Precompiles are split three ways so a guest can supply its own crypto:

| Piece | Role |
|---|---|
| `IPrecompileHandler` | `int AddressNumeric { get; }`, `byte[] Execute(byte[] input)` — one precompile |
| `IPrecompileGasCalculator` / `PrecompileGasCalculators` | the fork's gas schedule, independent of the handler |
| `PrecompileRegistry : IPrecompileRegistry` | `CanHandle(int address)`, `Get(int address)`, `GetGasCost(int address, byte[] input)`, `Execute(int address, byte[] input)`, `GetAddresses()`, `GetHandlers()`, `GasCalculators`, plus the copy-on-write `WithHandlers(params IPrecompileHandler[] additional)` and `WithGasCalculators(PrecompileGasCalculators gasCalculators)` |
| `PrecompileBackends` | the crypto seam: `EcRecover`, `Sha256`, `Ripemd160`, `ModExp`, `Bn128`, `Blake2f`, and the optional `P256Verify` |

`PrecompileRegistries` builds the fork-shaped registries (`FrontierHandlers`, `CoreHandlers`, `FromSpec`, `WithGas`, `CancunBase`, `PragueBase`, `OsakaBase`); `MainnetPrecompileExecutorFactory` wires a `PrecompileBackends` into them for `MainnetHardforkRegistry.Build`.

A `HardforkConfig` static preset carries **no** registry — `HardforkConfig.Cancun.Precompiles` is `null`. Attach one with `HardforkConfigExtensions.WithPrecompiles(this HardforkConfig config, PrecompileRegistry registry)`, which clones rather than mutating the shared preset. `MainnetHardforkRegistry.Build` does this for you.

*From `EvmSimulatorDocExampleTests.AttachAPrecompileRegistryToAHardforkConfig` — tagged, passing.*

```csharp
Assert.Null(HardforkConfig.Cancun.Precompiles);

var config = HardforkConfig.Cancun.WithPrecompiles(DefaultPrecompileRegistries.CancunBase());

Assert.True(config.Precompiles.CanHandle(0x01));
Assert.True(config.Precompiles.CanHandle(0x09));
Assert.False(config.Precompiles.CanHandle(0x0b));
```

BLS (0x0b–0x11) and KZG point evaluation (0x0a) are **not** in the base registries: they are installed by `WithBlsBackend` / `WithKzgBackend` from `Nethereum.EVM.Precompiles.Bls` and `Nethereum.EVM.Precompiles.Kzg`. An address that a fork registers but whose backend was not supplied is filled with a `PlaceholderPrecompile`, which throws `UnwiredPrecompileException` (carrying its `AddressNumeric`) rather than returning a wrong answer.

## State reading

`IStateReader` (`Nethereum.EVM.BlockchainState`) is the whole state seam, and it is the clearest illustration of the two arms — one interface, written twice:

```csharp
public interface IStateReader
{
#if EVM_SYNC
    EvmUInt256 GetBalance(byte[] address);
    EvmUInt256 GetBalance(string address);
    byte[] GetCode(byte[] address);
    byte[] GetCode(string address);
    byte[] GetStorageAt(byte[] address, EvmUInt256 position);
    byte[] GetStorageAt(string address, EvmUInt256 position);
    EvmUInt256 GetTransactionCount(byte[] address);
    EvmUInt256 GetTransactionCount(string address);
    bool AccountExists(string address);
    byte[] GetBlockHash(long blockNumber);
#else
    Task<EvmUInt256> GetBalanceAsync(byte[] address);
    Task<EvmUInt256> GetBalanceAsync(string address);
    Task<byte[]> GetCodeAsync(byte[] address);
    Task<byte[]> GetCodeAsync(string address);
    Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position);
    Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position);
    Task<EvmUInt256> GetTransactionCountAsync(byte[] address);
    Task<EvmUInt256> GetTransactionCountAsync(string address);
    Task<bool> AccountExistsAsync(string address);
    Task<byte[]> GetBlockHashAsync(long blockNumber);
#endif
}
```

That is the whole trick: implement ten methods and the engine runs, in either arm.

| Type | Role |
|---|---|
| `InMemoryStateReader` | witness-backed: constructed from `Dictionary<string, AccountState>`, exposes `Accounts`, `GetAccountState`, `AccountHasStorage`, `CommitChanges(ExecutionStateService executionState)` |
| `AccountState` | `Balance`, `Nonce`, `Code`, `Storage` |
| `ExecutionStateService` | wraps a reader and tracks every read and write for one transaction — warm/cold access, balance credits/debits (`AccountExecutionBalance`), storage modifications, code deployment |
| `IStateSnapshot` / `StateSnapshot` | revert points for nested frames |
| `MissingWitnessDataException` | thrown by a strict reader on a read the witness does not cover |

`InMemoryStateReader.Strict` turns a missing-data read from a silent zero into `MissingWitnessDataException`. For a zkVM guest that is essential: a missed read means the recorder is buggy, not that the slot is legitimately zero.

## EIP-7685 execution requests

`ExecutionRequests` implements the request commitment. Request-type bytes, verbatim:

```csharp
public static class ExecutionRequests
{
    public const byte DepositRequestType = 0x00;
    public const byte WithdrawalRequestType = 0x01;
    public const byte ConsolidationRequestType = 0x02;
    public const byte BuilderDepositRequestType = 0x03;
    public const byte BuilderExitRequestType = 0x04;

    public static byte RequestTypeFor(string predeployAddress);
    public static byte[] Compose(byte requestType, byte[] requestData);
    public static bool CarriesData(byte[] request);
    public static bool IsValidEngineRequestsList(IReadOnlyList<byte[]> requests);
    public static bool IsActive(HardforkName fork);
    public static byte[] CommitmentFor(HardforkName fork, IEnumerable<byte[]> blockRequests);
    public static byte[] ComputeRequestsHash(IEnumerable<byte[]> blockRequests);
}
```

Each type byte belongs to one predeploy: deposits come from logs of `DepositRequests.DepositContractAddress` (`0x00000000219ab540356cbb839cbe05303d7705fa`); the other four are `SystemCallContracts.WithdrawalRequests`, `ConsolidationRequests`, `BuilderDeposit` and `BuilderExit`, mapped by `RequestTypeFor`.

| Method | |
|---|---|
| `byte RequestTypeFor(string predeployAddress)` | maps a predeploy to its type byte; throws `ArgumentOutOfRangeException` for anything else |
| `byte[] Compose(byte requestType, byte[] requestData)` | `request_type ++ request_data` |
| `bool CarriesData(byte[] request)` | `request.Length > 1` — an item that is only its type byte is *empty* |
| `bool IsValidEngineRequestsList(IReadOnlyList<byte[]> requests)` | `false` for a `null` list, for any item without data, or when the type bytes are not strictly ascending — the shape `engine_newPayloadV4`/`V5` require of `executionRequests` |
| `bool IsActive(HardforkName fork)` | `fork >= HardforkName.Prague` |
| `byte[] CommitmentFor(HardforkName fork, IEnumerable<byte[]> blockRequests)` | the commitment, or `null` before Prague |
| `byte[] ComputeRequestsHash(IEnumerable<byte[]> blockRequests)` | `sha256(concat(sha256(request) for each non-empty request))` |

Empty items are excluded from the intermediate list, so a block with an empty deposit list commits to the same hash as one that omits it entirely. Before Prague the header carries **no** `requests_hash` at all — which is a different thing from carrying the hash of an empty list.

*From `EvmSimulatorDocExampleTests.BeforePragueThereIsNoRequestsCommitment` — tagged, passing.*

```csharp
Assert.False(ExecutionRequests.IsActive(HardforkName.Cancun));
Assert.Null(ExecutionRequests.CommitmentFor(HardforkName.Cancun, new List<byte[]>()));

Assert.True(ExecutionRequests.IsActive(HardforkName.Prague));
Assert.NotNull(ExecutionRequests.CommitmentFor(HardforkName.Prague, new List<byte[]>()));
```

`BlockExecutionRequests` enforces the ordering rule — EIP-7685 requires ascending `request_type`, and EIP-6110 requires deposits in log order — by making deposits the only way to open the list:

```csharp
static BlockExecutionRequests OpenedWithDeposits(HardforkName fork, IEnumerable<Log> logsInBlockOrder)
void   AddFrom(string predeployAddress, byte[] requestData)
byte[] Commitment()
```

`DepositRequests` reads the deposit log itself: `DepositEventSignatureHash`, `IsDepositLog(Log log)`, `CollectRequestData(IEnumerable<Log> logsInBlockOrder)`, `ExtractDepositData(byte[] data)`. A log that claims to be a deposit but is malformed raises `MalformedDepositLogException` rather than being skipped.

## System calls

`SystemCallContracts` names the predeploys and answers which of them are active at a fork:

```csharp
public static class SystemCallContracts
{
    public const string BeaconRoots = "0x000f3df6d732807ef1319fb7b8bb8522d0beac02";
    public const string HistoryStorage = "0x0000F90827F1C53a10cb7A02335B175320002935";
    public const string WithdrawalRequests = "0x00000961Ef480Eb55e80D19ad83579A64c007002";
    public const string ConsolidationRequests = "0x0000BBdDc7CE488642fb579F8B00f3a590007251";
    public const string BuilderDeposit = "0x0000BFF46984E3725691FA540A8C7589300D8282";
    public const string BuilderExit = "0x000064D678505AD48F8CCB093BC65613800E8282";

    public const string SystemCaller = Nethereum.Util.AddressUtil.SYSTEM_ADDRESS;

    public static IReadOnlyList<string> RequestContractsFor(HardforkName fork);

    public static readonly IReadOnlyList<string> AllRequestContracts;
}
```

`RequestContractsFor` returns empty before Prague, the two Prague predeploys through Osaka, and all four from Amsterdam.

`SystemCallFailurePolicy` encodes the split the EIPs actually specify — the request predeploys are consensus-critical, the read-only ones are not:

| | `FailureInvalidatesBlock` | `AbsenceInvalidatesBlock` |
|---|---|---|
| `WithdrawalRequests`, `ConsolidationRequests`, `BuilderDeposit`, `BuilderExit` | `true` (EIP-7002 / EIP-7251 / EIP-8282) | `true` |
| `BeaconRoots`, `HistoryStorage` | `false` (EIP-4788 / EIP-2935: "the call must fail silently") | `false` |

*From `EvmSimulatorDocExampleTests.OnlyRequestPredeployFailuresInvalidateTheBlock` — tagged, passing.*

```csharp
Assert.True(SystemCallFailurePolicy.FailureInvalidatesBlock(SystemCallContracts.WithdrawalRequests));
Assert.True(SystemCallFailurePolicy.AbsenceInvalidatesBlock(SystemCallContracts.ConsolidationRequests));
Assert.True(SystemCallFailurePolicy.AbsenceInvalidatesBlock(SystemCallContracts.BuilderDeposit));
Assert.True(SystemCallFailurePolicy.AbsenceInvalidatesBlock(SystemCallContracts.BuilderExit));

Assert.False(SystemCallFailurePolicy.FailureInvalidatesBlock(SystemCallContracts.BeaconRoots));
Assert.False(SystemCallFailurePolicy.AbsenceInvalidatesBlock(SystemCallContracts.HistoryStorage));
```

`SystemCallExecution` runs them — `Prepare`, `IsAbsent(byte[] contractCode)`, `TakeCallSnapshot`, `SettleCallState`, `Commit`, plus the twinned `BuildProgram` / `BuildProgramAsync` — and turns the policy into `SystemCallPredeployMissingException` / `SystemCallFailedException` through `RefuseBlockOnAbsentRequestPredeploy(string contractAddress)` and `RefuseBlockOnFatalCallFailure(string contractAddress, Program program)`.

## EIP-8037: the state-gas dimension

From Amsterdam, gas has a second dimension. A transaction is granted an execution-gas allowance **and** a state-gas reservoir; operations that grow the state are charged against the reservoir, and only spill into execution gas when it runs dry.

Constants (`GasConstants`), verbatim:

| Constant | Value |
|---|---|
| `EIP8037_COST_PER_STATE_BYTE` | `1530` |
| `EIP8037_STORAGE_SET_STATE_GAS` | `64 * EIP8037_COST_PER_STATE_BYTE` |
| `EIP8037_NEW_ACCOUNT_STATE_GAS` | `120 * EIP8037_COST_PER_STATE_BYTE` |
| `EIP8037_AUTH_BASE_STATE_GAS` | `23 * EIP8037_COST_PER_STATE_BYTE` |
| `EIP8037_TX_MAX_GAS_LIMIT` | `16_777_216` |
| `EIP8037_SYSTEM_MAX_SSTORES_PER_CALL` | `16` |
| `EIP8037_CODE_HASH_PER_WORD` | `KECCAK256_PER_WORD` |

The per-transaction totals live in a `StateGasAccount`:

```csharp
public sealed class StateGasAccount
{
    public long ReservoirRemaining { get; set; }
    public long FromReservoir { get; set; }
    public long SpilledIntoExecution { get; set; }
}
```

`StateGasMeter` is the accounting: `TryChargeStateGas(Program program, long amount, out long spilled)`, `ChargeStateGas`, `ChargeNewAccountStateGas`, `CreditStateGasRefund`, `RestoreStateGas`, `DrainReservoir`, `AbsorbChild(Program parent, Program child)`, `ReturnOutstandingStateGasToGasLeft`. It reads and writes `Program.StateGasLeft`, `Program.StateGasBaseline`, `Program.StateGasSpilled` and is gated by `ProgramContext.StateGasActive`; the per-transaction totals live in `TransactionExecutionContext.StateGas` (`StateGasAccount { ReservoirRemaining, FromReservoir, SpilledIntoExecution }`) and surface as `TransactionExecutionResult.StateGasUsed` alongside `ExecutionGasUsed`.

Both examples below come from `Eip8037SyncStateGasTests` in `tests/Nethereum.EVM.Core.Tests` — tagged, passing, and synchronous, so they compile against this package. They share this helper from that file:

```csharp
private static Program NewProgram(long gasRemaining, long stateGasLeft)
{
    var program = new Program(new byte[0])
    {
        GasRemaining = gasRemaining,
        StateGasLeft = stateGasLeft,
        StateGasBaseline = stateGasLeft
    };
    return program;
}
```

*From `Given_LegitimateStateGasSpill_WhenFrameLaterHitsUnrelatedExceptionalHalt_Then_RestoreDoesNotResurrectGasRemaining` — a charge larger than the reservoir spills the excess onto execution gas, and a later halt must not hand it back.*

```csharp
var program = NewProgram(gasRemaining: 5000, stateGasLeft: 200);
StateGasMeter.ChargeStateGas(program, 1000);
Assert.False(program.HasExecutionError);
Assert.Equal(800, program.StateGasSpilled);

program.GasRemaining = 0;
program.MarkExceptionalHalt();

StateGasMeter.RestoreStateGas(program);

Assert.Equal(0, program.GasRemaining);
```

*From `Given_InsufficientCombinedGas_WhenChargeStateGasFails_AtEvmSync_Then_NoSpillRecorded` — when neither the reservoir nor the remaining gas can cover the charge, the frame errors and nothing is recorded as spilled.*

```csharp
var program = NewProgram(gasRemaining: 500, stateGasLeft: 0);

StateGasMeter.ChargeStateGas(program, 1000);

Assert.True(program.HasExecutionError);
Assert.Equal(0, program.GasRemaining);
Assert.Equal(0, program.StateGasSpilled);
```

At the transaction level, creating an account charges `EIP8037_NEW_ACCOUNT_STATE_GAS` and an `SSTORE` that grows the state charges `EIP8037_STORAGE_SET_STATE_GAS`, while overwriting a slot that was already set, or transferring to an account that already exists, charges **zero** — only growth is charged. The same transactions at Prague report `StateGasUsed == 0` because the dimension does not exist there (`HardforkConfig.IntrinsicGasRules.StateGasActive`). Those four cases are proved end to end by `Eip8037StateGasConstantsTests` in `tests/Nethereum.EVM.UnitTests`; they are not shown here because they run `TransactionExecutor.ExecuteAsync`, which this package does not have.

## BLOCKHASH via EIP-2935

From Prague, `BlockHashExecutor` delegates to `Eip2935BlockHashRule`, which reads slot `blockNumber % HISTORY_SERVE_WINDOW` (`8191`) from the storage of the history contract at `HISTORY_STORAGE_ADDRESS` = `0x0000F90827F1C53a10cb7A02335B175320002935` — the same address as `SystemCallContracts.HistoryStorage` — and returns 32 zero bytes when the slot is unset. There is no separate block-hash witness field — the ancestor-hash ring is normal contract storage, so a witness recorder captures it automatically. `HistoryContractHelpers.PopulateFromBlockHashes(List<WitnessAccount> accounts, IDictionary<long, byte[]> blockHashes, long blockNumber)` seeds that storage from a `(blockNumber → hash)` map for test vectors that carry hashes rather than slots. Which rule applies is a `HardforkConfig.BlockHashRule` seam: `LegacyBlockHashRule` or `Eip2935BlockHashRule`.

## Witnesses and block execution

`BlockWitnessData` is the in-memory witness: block context (`BlockNumber`, `Timestamp`, `BaseFee`, `BlockGasLimit`, `ChainId`, `Coinbase`, `Difficulty`, `PreStateRoot`, `ParentHash`, `ExtraData`, `MixHash`, `Nonce`, `SlotNumber`), the Cancun/Shanghai/Prague extras (`Withdrawals`, `BlobGasUsed`, `ExcessBlobGas`, `ParentBeaconBlockRoot`, `RequestsHash`), `Transactions` (`BlockWitnessTransaction { From, RlpEncoded, AuthorisationAuthorities }`), the pre-state `Accounts`, an optional `DeclaredBlockAccessList`, the `Features` (`BlockFeatureConfig { Fork, StateTree, HashFunction }`), and the flags `VerifyWitnessProofs`, `ComputePostStateRoot`, `ProduceBlockCommitments`.

`BlockFeatureConfig` has ready-made presets: `Cancun`, `Prague`, `Osaka`, and `BinaryBlake3(HardforkName fork = HardforkName.Osaka)` / `BinaryPoseidon(HardforkName fork = HardforkName.Osaka)` for the EIP-7864 binary state tree (`WitnessStateTreeType`, `WitnessHashFunction`).

`BinaryBlockWitness` is the wire format; `WitnessRecordingStateReader` wraps any `IStateReader`, captures every read, and `GetWitnessAccounts()` returns the minimal pre-state needed to re-execute; `WitnessStateBuilder.BuildAccountState` turns witness accounts back into the executor's `Dictionary<string, AccountState>`.

`BlockExecutor` is the entry point, in both arms:

```
BlockExecutionResult Execute(       BlockWitnessData block,
                                    IBlockEncodingProvider encodingProvider,
                                    HardforkRegistry hardforkRegistry,
                                    IStateRootCalculator stateRootCalculator = null,
                                    IBlockRootCalculator blockRootCalculator = null)

Task<BlockExecutionResult> ExecuteAsync( … same five parameters … )
```

`BlockExecutionResult` returns `TxResults`, `Receipts`, `CombinedBloom`, `CumulativeGasUsed`, `HeaderGasUsed`, `BlockExecutionGasUsed`, `BlockStateGasUsed`, `StateRoot`, `TransactionsRoot`, `ReceiptsRoot`, `BlockHash`, `ProducedHeader`, `BlockAccessList`, `BlockAccessListGasLimitExceeded`, `DeclaredBlockAccessListCheck`, `BlockAccessListMalformed`, `FinalExecutionState`, `StateReader`.

`IStateRootCalculator`, `IBlockRootCalculator` and `IMerkleTreeBuilder` are declared here but **implemented elsewhere** — `PatriciaStateRootCalculator` and `PatriciaBlockRootCalculator` live in `Nethereum.CoreChain`, which is why this package can stay free of a trie dependency.

## Transaction execution

`TransactionExecutionContext` carries the whole transaction: sender/recipient/data/value, the fee-market fields (`IsEip1559`, `MaxFeePerGas`, `MaxPriorityFeePerGas`, `EffectiveGasPrice`), blob fields (`IsType3Transaction`, `BlobVersionedHashes`, `MaxFeePerBlobGas`, `BlobGasCost`), `AccessList`, `AuthorisationList` + `AuthorisationAuthorities`, the block environment, the gas split (`IntrinsicExecutionGas`, `FloorGas`, `ExecutionGasGrant`, `StateGasReservoir`, `StateGas`, `StateGasUsed`), snapshots, and `ExecutionState`. `ExecutionMode` is `Transaction`, `Call`, `SystemCall`.

`TransactionContextFactory` builds one from a signed transaction (`From`), an RLP blob (`FromRlpEncoded`) or a witness transaction (`FromBlockWitnessTransaction`).

```csharp
public enum ExecutionMode
{
    Transaction,
    Call,
    SystemCall
}
```

`TransactionExecutor` runs whichever mode the context names:

```csharp
public TransactionExecutionResult Execute(TransactionExecutionContext ctx)
```

`TransactionExecutionResult` reports `Success`, `GasUsed`, `GasRefund`, `EffectiveGasUsed`, `ExecutionGasUsed`, `StateGasUsed`, `ReturnData`, `RevertReason`, `Logs`, `StateRoot`, `ContractAddress`, `Error`, `ErrorCode`, `IsValidationError`, `Traces`, `Program`, `CreatedAccounts`, `DeletedAccounts`, `InnerCalls`, `InnerContractCodeCalls`, `ProgramResult`.

`ErrorCode` is a `TransactionError`, and it reports **pre-execution validation** failures only:

```csharp
public enum TransactionError
{
    None,
    InsufficientMaxFeePerGas,
    PriorityGreaterThanMaxFee,
    InsufficientBalance,
    GasAllowanceExceeded,
    IntrinsicGasTooLow,
    NonceIsMax,
    SenderNotEOA,
    InitcodeSizeExceeded,
    Type3TxContractCreation,
    Type3TxZeroBlobs,
    Type3TxBlobCountExceeded,
    Type3TxInvalidBlobVersionedHash,
    AddressCollision,
    InvalidEFPrefix,
    MaxCodeSizeExceeded,
    OutOfGas,
    Reverted,
    InsufficientMaxFeePerBlobGas,
    GasLimitExceedsMaximum,
    Type4TxContractCreation,
    Type4EmptyAuthorizationList,
    TransactionTypeNotSupported,
    InvalidChainId,
    NonceMismatch,
}
```

An in-EVM `REVERT` is *not* one of them: it leaves `ErrorCode` at `None` and shows up as `Success == false` with `ProgramResult.IsRevert == true`. (`Reverted` is declared but never assigned by the executor — check `ProgramResult.IsRevert`, not the code.) A validation rejection additionally raises `TransactionValidationException` carrying its `Reason`.

*From `EvmSimulatorDocExampleTests.AnInEvmRevertFailsTheTransactionWithoutSettingAnErrorCode` — tagged, passing.*

```csharp
Assert.False(result.Success);
Assert.True(result.ProgramResult.IsRevert);
Assert.Equal(TransactionError.None, result.ErrorCode);
Assert.False(result.IsValidationError);
```

## Project layout

| Area | Key types |
|---|---|
| Forks & registries | `HardforkName`, `HardforkNames`, `HardforkConfig`, `HardforkRegistry`, `MainnetHardforkRegistry`, `IChainActivations`, `MainnetChainActivations`, `ChainActivationsRegistry`, `Hardforks/*Spec.cs`, `HardforkSpecRegistry` |
| State | `IStateReader`, `InMemoryStateReader`, `ExecutionStateService`, `AccountState`, `AccountExecutionState`, `AccountExecutionBalance`, `IStateSnapshot`, `MissingWitnessDataException` |
| Executor | `EVMSimulator`, `TransactionExecutor`, `BlockExecutor`, `CallFrame`, `ProgramContext`, `Program`, `ProgramResult`, `ProgramTrace` |
| Opcodes | `Execution/Opcodes/Executors/*Executor.cs`, `OpcodeHandlerTable`, `OpcodeHandlerSets`, `Instruction`, `InstructionLookup` |
| Gas | `Gas/GasConstants`, `Gas/IntrinsicGasRuleSets`, `Gas/Opcodes/Costs/*`, `Gas/Opcodes/Rules/*`, `Gas/StateGasMeter`, `Gas/BlockGasCapacity` |
| Precompiles | `Execution/Precompiles/*` — handlers, gas calculators, `PrecompileRegistry`, `PrecompileBackends` |
| Rule seams | `Execution/CallFrame/Rules/*`, `Execution/SelfDestruct/Rules/*`, `Execution/TransactionSetup/Rules/*`, `Execution/TransactionValidation/Rules/*`, `Execution/Storage/*`, `Execution/TransferLogs/Rules/*`, `Execution/TxFinalisation/*` |
| Requests & system calls | `Execution/ExecutionRequests`, `BlockExecutionRequests`, `DepositRequests`, `SystemCallContracts`, `SystemCallExecution`, `SystemCallFailurePolicy` |
| Block access lists | `Execution/BlockAccessListBuilder`, `BlockAccessListCollector`, `BlockAccessListSizeRule`, `BlockAccessListStructureRule` |
| Witness | `Witness/BlockWitnessData`, `BinaryBlockWitness`, `WitnessRecordingStateReader`, `WitnessStateBuilder`, `HistoryContractHelpers`, `WitnessStateTreeType`, `WitnessHashFunction` |
| Trie seams | `IStateRootCalculator`, `IBlockRootCalculator`, `IMerkleTreeBuilder` (implemented in `Nethereum.CoreChain`) |

## See also

- [`Nethereum.EVM`](../Nethereum.EVM/README.md) — the async host build of this same source, plus RPC state, decoding, state-change extraction and debugging.
- [`Nethereum.EVM.Precompiles`](../Nethereum.EVM.Precompiles/README.md) — the default crypto backends and `DefaultMainnetHardforkRegistry`.
- `Nethereum.EVM.Precompiles.Bls` / `Nethereum.EVM.Precompiles.Kzg` — the BLS and KZG backends.
- `Nethereum.EVM.Zisk` — the zkVM guest wiring that consumes the `EVM_SYNC` build.
