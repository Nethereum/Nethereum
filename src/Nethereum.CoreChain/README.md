# Nethereum.CoreChain

Run an Ethereum node's brain in .NET. Give it a block and it tells you whether that block is valid and what state it produces; give it transactions and it seals a block from them; point it at a block source and it follows a chain to the tip, checkpointing and rewinding on its own. It is the same engine behind the mainnet follower (Nethereum.MainnetChain), AppChains and DevChain.

## What you can do with it

| Task | Reach for |
|---|---|
| Execute a block — the canonical state transition π(σ,B): receipts, logs, gas, post-state root | `BlockExecutor.ExecuteAsync` |
| Judge a block and find out *why* it was rejected, check by check | `BlockImporter.ImportAsync` → `BlockImporterResult` |
| Seal a new block from a set of transactions | `BlockProducer.ProduceBlockAsync` |
| Follow a chain to its tip, checkpointing and rewinding on divergence | `FollowerChainNode.RunAsync` / `FollowerService.RunAsync` |
| Compute a state root without walking the whole state each block | `IncrementalStateRootCalculator` |
| Serve EIP-1186 account/storage proofs, at the tip or as-of a recent block | `ProofService` / `ProofServiceAsOf` |
| Undo blocks — flat-state journal, node history, or nearest checkpoint | `RewindCoordinator.RewindToAsync` |
| Serve `eth_*` / `net_*` / `web3_*` JSON-RPC over all of the above | `RpcHandlerRegistry` + `RpcDispatcher` |
| Build an AppChain node on the same engine mainnet runs | the seams in [Reusing the core for an AppChain](#reusing-the-core-for-an-appchain) |

CoreChain brings no database, no networking and no consensus client of its own — those bind to its interfaces from outside: persistent storage from **Nethereum.CoreChain.RocksDB**, devp2p sync and peer serving from **Nethereum.DevP2P.Sync**, the trie and proofs from **Nethereum.Merkle.Patricia**, the fork schedule and EVM from **Nethereum.EVM**, and the mainnet consensus gate and light client from **Nethereum.MainnetChain**. For tests, `InMemoryChainStoreBundle.Open()` supplies the whole storage side in one call.

## Install

```bash
dotnet add package Nethereum.CoreChain
```

## Quick Start — import a block and read the verdict

Extracted from `QuickStartDocExampleTests` (`tests/Nethereum.CoreChain.UnitTests/QuickStartDocExampleTests.cs`), which seals a block on one node stack and imports it into a second.

```csharp
var stores = InMemoryChainStoreBundle.Open();
await SystemContractPredeploys.ApplyGenesisAllocationAsync(stores.State, HardforkName.Prague);

var config = new ChainConfig { ChainId = 1337, BlockGasLimit = 30_000_000, BaseFee = 0 };
var roots = new IncrementalStateRootCalculator(stores.State, stores.TrieNodes);
var engine = new BlockExecutor(
    stores.State, stores.Blocks,
    new FixedChainActivations(HardforkName.Prague),
    chainConfigFactory: _ => config,
    hardforkConfigFactory: _ => config.GetHardforkConfig(),
    stateRootCalculator: roots,
    rewardPolicy: NoRewardPolicy.Instance,
    trieNodeStore: stores.TrieNodes);

var verdict = await new BlockImporter(engine, stores.Blocks, stores.State)
    .ImportAsync(header, transactions, uncles: null, withdrawals: null);

Console.WriteLine(verdict.RootMatches
    ? "the block passed every validity check"
    : verdict.DescribeRejection());
```

A block that passes leaves `verdict.RootMatches` true, `verdict.FailedChecks` empty and `verdict.BlockHash` set, and `DescribeRejection()` returns exactly `"the block passed every validity check"`. Give the same importer a header whose `GasUsed` exceeds its own `GasLimit` and the verdict flips: `RootMatches` false, `BlockHash` null, `FailedChecks` containing `"gasCapacity"`, and `DescribeRejection()` starting `"failed checks: gasCapacity"`. That is the whole contract — a rejection always names itself.

Swap `InMemoryChainStoreBundle.Open()` for `RocksDbChainStoreBundle.Open(dataDir)` and the same three objects run against a persistent database.

## Entry points

**Reach for `BlockExecutor` first.** It is the state transition itself, and everything else in this package is a thin layer that drives it: `BlockImporter` runs it with `Role = BlockExecutionRole.Validating` and turns the result into a verdict, `BlockProducer` runs it in the default `Building` role and seals a header, and `FollowerChainNode` drives an `IBlockExecutor` — `BlockImporter` in the shipped wiring — in a loop. Once you have constructed one `BlockExecutor`, the rest is composition.

| I want to... | Use |
|---|---|
| execute a block against some state, persisting nothing | `BlockExecutor.ExecuteAsync(header, txs, uncles, withdrawals, options, ct)` |
| import a block and get a pass/fail verdict with named checks | `BlockImporter.ImportAsync(header, transactions, uncles, withdrawals, ct)` |
| know *why* a block was rejected | `BlockImporterResult.DescribeRejection()`, `.FailedChecks`, `.FailedValidityChecks` |
| seal a block from transactions I chose | `BlockProducer.ProduceBlockAsync(transactions, options)` |
| follow a chain from a block source and keep following | `FollowerChainNode.RunAsync(ct)` |
| plug in storage (any backend) | `IChainStoreBundle` — `InMemoryChainStoreBundle.Open()` or `RocksDbChainStoreBundle.Open(dataDir)` |
| answer an `eth_getProof` | `ProofService` (tip) / `ProofServiceAsOf` (as-of a retained block) |
| decide what happens on divergence | `IValidationPolicy` — `StrictValidationPolicy` is the shipped answer |

## Node & execution

The execution stack composes in three layers:

- **`BlockExecutor`** — the canonical state-transition engine `π(σ,B)` (Yellow Paper §11). It executes a block (system calls → transactions → withdrawals → rewards → post-state root) and never persists chain bodies. Fork behaviour (EIP-4788 beacon root, EIP-2935 parent-hash history, DAO drain, EIP-7685 end-of-block requests, EIP-4895 withdrawals) is gated by the injected `IChainActivations`, so the same engine runs mainnet and AppChains. Its constructor takes `IStateStore`, `IBlockStore`, `IChainActivations`, a `Func<HardforkName, ChainConfig>`, a `Func<HardforkName, HardforkConfig>`, an `IIncrementalStateRootCalculator`, an `IRewardPolicy`, an `ITrieNodeStore`, and optionally `ILogger<BlockExecutor>`, `IBlockRootsProvider` and `Func<BlockHeader,string>? authorResolver = null` (resolves a header to its block author, e.g. for Clique).
- **`BlockImporter`** — wraps `BlockExecutor` for the follower: one execution per block, plus persistence (header, uncles, tx/receipt/log bodies), reverse-diff journal bracketing, and the node-history block bracket. Implements `Nethereum.CoreChain.Sync.IBlockExecutor`; its `RootMatches` result drives the follower.
- **`FollowerChainNode`** — the follower-only `IChainNode`. Its constructor binds an `IChainStoreBundle`, an `IBlockSource`, an `executorFactory`, an `IValidationPolicy`, `FollowerOptions`, a `ChainConfig`, a `HardforkConfig`, a `TransactionProcessor` and an `ITransactionVerificationAndRecovery`, plus optional `IFollowerService`, `ICanonicalStateRootSource`, `IFilterStore`, `IStateReader`, `IBlobStore`, `IChainActivations`, hardfork-config factory and logger. `RunAsync(ct)` drives the loop; it serves latest and as-of proofs (`ProofService`, `ProofServiceAsOf`, `CanServeProofAsOf`). It is read-only — no mempool.

**`FollowerService`** (`IFollowerService`) runs the loop in either of two modes: a **legacy forward stream** (consume `IBlockSource.StreamAsync`, execute, commit the cursor per matched block, checkpoint periodically, and on divergence consult the validation policy), or a **tip-event-driven** mode (poll `ICanonicalStateRootSource.GetLatestAsync`, skeleton-fill headers/bodies backward via an injected walker delegate, then execute forward over local storage). On a completed snap sync (`SnapPhase.Complete`) it fast-starts at pivot + 1.

```csharp
Task<FollowerRunResult> RunAsync(
    IBlockSource source,
    Func<IChainStoreBundle> bundleFactory,
    Func<IChainStoreBundle, IBlockExecutor> executorFactory,
    IValidationPolicy policy,
    ICanonicalStateRootSource canonical,
    FollowerOptions options,
    CancellationToken ct,
    ILogger logger = null);

public sealed record FollowerOptions(
    ulong StartBlock,
    ulong CheckpointEvery,
    ulong AnchorEvery,
    int MaxConsecutiveDivergences = 3,
    int MaxRewindCycles = 3,
    ulong? EndBlock = null,
    int MaxConsecutiveSourceFailures = 120,
    int? KeepLatestCheckpoints = null,
    TimeSpan? TipPollInterval = null,
    ulong WalkerInvocationThreshold = 1UL,
    ulong MaxReorgDepth = 1024UL,
    bool ExternalHeaderFollow = false);

public sealed record FollowerRunResult(
    FollowerExitReason ExitReason,
    ulong LastExecutedBlock,
    ulong BlocksExecuted,
    ulong RootMismatches,
    ulong RewindCyclesUsed,
    ChainCheckpoint? SnapshotRestoreTarget,
    string Detail);

public enum FollowerExitReason
{
    SourceCompleted,
    Cancelled,
    FatalVerdict,
    RewindUnavailable,
    SnapshotRestoreRequested,
    SourceUnavailable,
}
```

## Block production

`BlockProducer` implements `IBlockProducer`, and drives the same `BlockExecutor` engine forward: it orders the transactions with an `ITransactionOrderingPolicy` (default `MempoolNonceOrderingPolicy.Instance`), executes them, computes the roots with an `IBlockRootsProvider` (default `PatriciaBlockRootsProvider`), encodes the block with an `IBlockEncodingProvider` (default `RlpBlockEncodingProvider.Instance`), hashes the header with an `IBlockHashProvider` (default `RlpKeccakBlockHashProvider.Instance`), and persists the block, transactions, receipts, logs and withdrawals. Production is serialised behind a lock, so one producer instance seals one block at a time.

```csharp
Task<BlockProductionResult> ProduceBlockAsync(
    IReadOnlyList<ISignedTransaction> transactions,
    BlockProductionOptions options);

public class BlockProductionOptions
{
    public Action<BlockHeader>? ApplyConsensusSeal { get; set; }
    public long Timestamp { get; set; }
    public string Coinbase { get; set; }
    public BigInteger BaseFee { get; set; }
    public BigInteger BlockGasLimit { get; set; }
    public BigInteger Difficulty { get; set; }
    public byte[] PrevRandao { get; set; }
    public byte[] ExtraData { get; set; }
    public BigInteger ChainId { get; set; }
    public byte[] ParentBeaconBlockRoot { get; set; }
    public byte[] Nonce { get; set; }
    public ulong? SlotNumber { get; set; }
    public bool CaptureWitness { get; set; }
    public HardforkName HardforkName { get; set; } = HardforkName.Prague;
    public IList<Withdrawal> Withdrawals { get; set; }
}

public class BlockProductionResult
{
    public BlockHeader Header { get; set; }
    public byte[] BlockHash { get; set; }
    public List<TransactionResult> TransactionResults { get; set; } = new();
    public int SuccessfulTransactions { get; set; }
    public int FailedTransactions { get; set; }
    public byte[] WitnessBytes { get; set; }
    public byte[] PreStateRoot { get; set; }
    public IReadOnlyList<byte[]> ExecutionRequests { get; set; }
    public byte[]? BlockAccessListRlp { get; set; }
    public object? MessageBatchResult { get; set; }
    public List<ISignedTransaction> IncludedTransactions { get; set; } = new();
}
```

The `BlockProducer` constructor takes the engine, the `IBlockStore` / `ITransactionStore` / `IReceiptStore` / `ILogStore` / `IStateStore` / `ITrieNodeStore` / `IIncrementalStateRootCalculator`, and then, in this order, the optional `ITransactionOrderingPolicy`, `IBlockHashProvider`, `IBlockEncodingProvider` and `IBlockRootsProvider` seams, followed by an `IWithdrawalStore` and a `NodeCommitBlockContext`. Those optional parameters are positional with a `null` default, so passing them by name is the safe way to reach the later ones. A second overload adds the `IBlockAccessListStore` and requires every one of those parameters to be passed.

**Compose the stack and produce a block** — extracted from `BlockProducerTests.ProduceBlock_WithValidTransfer_Succeeds` (`tests/Nethereum.CoreChain.UnitTests/BlockProducerTests.cs`):

```csharp
var trieNodeStore = new InMemoryContentNodeStore();
var stateRootCalculator = new IncrementalStateRootCalculator(_stateStore, trieNodeStore);
var engine = new BlockExecutor(
    _stateStore,
    _blockStore,
    new FixedChainActivations(HardforkName.Prague),
    chainConfigFactory: _ => config,
    hardforkConfigFactory: _ => config.GetHardforkConfig(),
    stateRootCalculator: stateRootCalculator,
    rewardPolicy: NoRewardPolicy.Instance,
    trieNodeStore: trieNodeStore);

var blockProducer = new BlockProducer(
    engine,
    _blockStore, _transactionStore, _receiptStore, _logStore, _stateStore,
    trieNodeStore, stateRootCalculator);

var result = await blockProducer.ProduceBlockAsync(
    new List<ISignedTransaction> { tx }, DefaultOptions());
// result.SuccessfulTransactions == 1, result.TransactionResults[0].Success
```

## Executing a block

```csharp
Task<BlockExecutionResult> ExecuteAsync(
    BlockHeader header,
    IReadOnlyList<TxEntry> txs,
    IList<BlockHeader>? uncles,
    IList<WithdrawalEntry>? withdrawals,
    BlockExecutionOptions options,
    CancellationToken ct = default);

public enum BlockExecutionRole
{
    Building,
    Validating,
    Simulating
}

public sealed class SimulateCallOptions
{
    public bool Validation { get; init; }
    public bool TraceTransfers { get; init; }
    public Dictionary<string, EvmUInt256> ExpectedNonces { get; init; } = new Dictionary<string, EvmUInt256>();
}

public sealed class BlockExecutionOptions
{
    public bool ReadOnly { get; init; }
    public bool CaptureWitness { get; init; }
    public byte[]? ParentBeaconBlockRoot { get; init; }
    public int? TraceTxIndex { get; init; }
    public BlockExecutionRole Role { get; init; } = BlockExecutionRole.Building;
    public SimulateCallOptions? SimulateCallOptions { get; init; }
    public BigInteger SimulateGlobalGasUsedBefore { get; init; }
    public List<AccountChanges>? DeclaredBlockAccessList { get; init; }
}
```

`Role` is the difference between sealing a block and judging one: `Validating` compares the executed result against what the header *declares*, and `Simulating` runs the block for `eth_simulateV1`-style speculative execution, with `SimulateCallOptions` (`Validation`, `TraceTransfers`, per-account `ExpectedNonces`) tuning that mode. `SimulateGlobalGasUsedBefore` is `eth_simulateV1`-only: gas already spent by every prior simulated block in the same request, since geth funds a no-gas call from a single RPC gas-cap budget spanning the whole call rather than resetting per block — a later block's synthetic transactions carry `min(blockGasLimit - blockGasUsedSoFar, RpcGasCap - globalGasUsedSoFar)`. It is zero for the first block and for every non-simulating role. `DeclaredBlockAccessList` carries the block access list as the block declares it (an Engine API payload, an eth/71 body) — EIP-7928's Engine API returns `INVALID` both when the list "is malformed" and when it "doesn't match", and recomputing hashes can only answer the second; only the declared list itself can answer the first.

## Block validity checks

`BlockExecutionResult` implements `IBlockValidityChecks`. Every check is a flag on the result, and the `BlockValidityChecks` extensions turn those flags into an enum set (`Failed()`), a wire-identifier set (`FailedIdentifiers()`) or a single boolean (`AnyFailed()`).

```csharp
public enum BlockValidityCheck
{
    StateRoot,
    ReceiptsRoot,
    LogsBloom,
    GasUsed,
    GasCapacity,
    BlobGasCapacity,
    BlobGasUsed,
    ExcessBlobGas,
    BlobFieldFormat,
    BaseFee,
    BlockAccessListHash,
    BlockAccessListGasLimit,
    BlockAccessListMalformed,
    RequestsHash,
    InvalidTransaction,
    GasLimitBound,
    WithdrawalsRoot
}

public interface IBlockValidityChecks
{
    bool StateRootMismatch { get; }
    bool ReceiptsRootMismatch { get; }
    bool LogsBloomMismatch { get; }
    bool GasUsedMismatch { get; }
    bool GasCapacityExceeded { get; }
    bool BlobGasCapacityExceeded { get; }
    bool BlobGasUsedMismatch { get; }
    bool ExcessBlobGasMismatch { get; }
    bool BlobFieldFormatMismatch { get; }
    bool BaseFeeMismatch { get; }
    bool BlockAccessListHashMismatch { get; }
    bool BlockAccessListGasLimitExceeded { get; }
    bool BlockAccessListMalformed { get; }
    bool RequestsHashMismatch { get; }
    bool ContainsInvalidTransaction { get; }
    bool GasLimitBoundViolated { get; }
    bool WithdrawalsRootMismatch { get; }
}
```

Each check has a stable wire identifier, returned by `check.Identifier()` and surfaced in `FailedIdentifiers()` / `BlockImporterResult.FailedChecks`:

| Check | Identifier | Fails when |
|---|---|---|
| `StateRoot` | `stateRoot` | the post-state root differs from the header's |
| `ReceiptsRoot` | `receiptsRoot` | the receipts trie root differs from the header's |
| `LogsBloom` | `logsBloom` | the combined bloom differs from the header's |
| `GasUsed` | `gasUsed` | the executed gas total differs from the header's `gasUsed` |
| `GasCapacity` | `gasCapacity` | the block's gas exceeds its own gas limit |
| `BlobGasCapacity` | `blobGasCapacity` | the block's blob gas exceeds the fork's blob capacity |
| `BlobGasUsed` | `blobGasUsed` | the executed blob gas differs from the header's `blobGasUsed` |
| `ExcessBlobGas` | `excessBlobGas` | the header's `excessBlobGas` differs from the value derived from the parent |
| `BaseFee` | `baseFee` | the header's `baseFeePerGas` differs from the EIP-1559 value derived from the parent |
| `BlockAccessListHash` | `blockAccessListHash` | the recomputed EIP-7928 BAL hash differs from the header's |
| `BlockAccessListGasLimit` | `blockAccessListGasLimit` | the BAL's own gas accounting exceeds its limit |
| `BlockAccessListMalformed` | `blockAccessListMalformed` | the *declared* BAL is not well-formed (EIP-7928 §Engine API's first verdict) |
| `RequestsHash` | `requestsHash` | the header's EIP-7685 `requests_hash` is not the hash of the requests this block produced |
| `InvalidTransaction` | `invalidTransaction` | a transaction in the block is not admissible at all |
| `GasLimitBound` | `gasLimitBound` | the header's `gasLimit` moves outside the per-block adjustment bound from the parent |
| `WithdrawalsRoot` | `withdrawalsRoot` | the withdrawals trie root differs from the header's |

`Identifier()` throws `ArgumentOutOfRangeException` for a check with no registered identifier, so adding an enum member without a wire name fails loudly rather than silently.

**A failing check names itself on the importer result** — extracted from `BlockValidityCheckParityTests` (`tests/Nethereum.CoreChain.UnitTests/BlockValidityCheckParityTests.cs`):

```csharp
var tampered = BlockPipelineHarness.CloneHeader(produced.Header);
tampered.GasUsed = tampered.GasLimit + 1;

var follower = await BlockPipelineHarness.CreateAsync();
var result = await follower.ImportAsync(tampered, txs);

// result.RootMatches == false, result.BlockHash == null,
// result.FailedChecks contains "gasCapacity"
```

## System calls & predeploys

Post-Merge forks introduce contracts the protocol itself calls at block boundaries. `SystemContractPredeploys` holds their **runtime code** and the fork each one activates at, so a fresh genesis (AppChain, DevChain, a test fixture) can be allocated with exactly the accounts mainnet has.

```csharp
public sealed class SystemContractPredeploy
{
    public SystemContractPredeploy(string address, string runtimeCodeHex, HardforkName activationFork, string name = null);
    public string Address { get; }
    public byte[] RuntimeCode { get; }
    public HardforkName ActivationFork { get; }
    public string Name { get; } // EIP-7910 eth_config canonical name, or null when eth_config names no such contract
    public const int Nonce = 1;
}

public static class SystemContractPredeploys
{
    public const string HistoryStorageRuntimeCode = "0x33...";
    public const string BeaconRootsRuntimeCode = "0x33...";
    public const string Create2FactoryAddress = "0x4e59b44847b379578588920cA78FbF26c0B4956C";
    public const string Create2FactoryRuntimeCode = "0x7f...";
    public const string WithdrawalRequestsRuntimeCode = "0x33...";
    public const string ConsolidationRequestsRuntimeCode = "0x33...";
    public const string BuilderDepositRuntimeCode = "0x33...";
    public const string BuilderExitRuntimeCode = "0x33...";

    public static readonly SystemContractPredeploy Create2Factory;
    public static readonly IReadOnlyList<SystemContractPredeploy> All;

    public static IEnumerable<SystemContractPredeploy> For(HardforkName fork);
    public static Task ApplyGenesisAllocationAsync(IStateStore stateStore, HardforkName fork);
    public static Task ApplyGenesisAllocationAsync(
        IStateStore stateStore, HardforkName fork, IEnumerable<SystemContractPredeploy> predeploys);
    public static Task AllocateIfAbsentAsync(IStateStore stateStore, SystemContractPredeploy predeploy);
}
```

`All` is, in order:

| Predeploy | Address | Activates at |
|---|---|---|
| Beacon block roots (EIP-4788) | `0x000F3df6D732807Ef1319fB7B8bB8522d0Beac02` | Cancun |
| Block-hash history (EIP-2935) | `0x0000F90827F1C53a10cb7A02335B175320002935` | Prague |
| Withdrawal requests (EIP-7002) | `0x00000961Ef480Eb55e80D19ad83579A64c007002` | Prague |
| Consolidation requests (EIP-7251) | `0x0000BBdDc7CE488642fb579F8B00f3a590007251` | Prague |
| Builder deposit | `0x0000BFF46984E3725691FA540A8C7589300D8282` | Amsterdam |
| Builder exit | `0x000064D678505AD48F8CCB093BC65613800E8282` | Amsterdam |
| Keyless CREATE2 factory (EIP-7997) | `0x4e59b44847b379578588920cA78FbF26c0B4956C` | Amsterdam |

`ApplyGenesisAllocationAsync(stateStore, fork)` writes every predeploy whose `ActivationFork` is at or below `fork`; `AllocateIfAbsentAsync` is the per-account step and is a **no-op when the account already has code**, keeping any balance already there and setting `Nonce = 1` (EIP-2935 exempts `HISTORY_STORAGE_ADDRESS` from EIP-161 cleanup by giving it code and a nonce of 1; EIP-7997 likewise requires the factory account to be in genesis "with a nonce equal to 1"). The addresses themselves are `Eip4788Constants.BeaconRootsAddress`, `Eip2935Constants.HistoryStorageAddress` and `SystemCallContracts.*` (the latter in Nethereum.EVM.Core).

`CoreChain`'s `Forks/` folder holds the matching system-call helpers — `Eip2935Helpers`, `Eip4788Helpers`, `Eip7685Constants`, `DaoForkConstants`, `GenesisHeaderFields` — and `FixedChainActivations`, which pins one `HardforkName` for all blocks (AppChains, DevChain, tests). Mainnet instead uses the schedule-driven `MainnetChainActivations` from Nethereum.EVM.Core.

## Storage abstractions

Persistent backends implement `IChainStoreBundle`, the single composition root:

```csharp
public interface IChainStoreBundle : IAsyncDisposable, IDisposable
{
    IStateStore State { get; }
    ITrieNodeStore TrieNodes { get; }
    ITrieNodeStore StateTrieNodes { get; }
    NodeCommitBlockContext NodeCommitBlockSource { get; }
    IBlockStore Blocks { get; }
    ITransactionStore Transactions { get; }
    IUncleStore Uncles { get; }
    IWithdrawalStore Withdrawals { get; }
    IBlockAccessListStore BlockAccessLists { get; }
    IReceiptStore Receipts { get; }
    ILogStore Logs { get; }
    IChainMetadataStore Metadata { get; }
    IStateDiffStore Diffs { get; }
    bool JournalEnabled { get; }

    Task<ChainCheckpoint> SaveCheckpointAsync(
        ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default);
    Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default);
    Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default);
    Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default);
    Task ResetStateOnlyAsync(CancellationToken ct = default);
    Task ResetSnapBootstrapStateAsync(CancellationToken ct = default);
    string ResolveCheckpointSnapshotPath(ulong blockNumber);
    Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default);

    long FreezerHead { get; }
    long ByHashIndexedHead { get; }
    long LogIndexRenderedHead { get; }
    long LogRenderProgressBlock { get; }

    IBundleBatch BeginBatch();
}
```

- `State` (`IStateStore`) — accounts, storage (by slot and by `keccak(slot)`), code, snapshots, and dirty tracking (dirty accounts/slots/cleared-addresses) that drives incremental state-root computation.
- `Blocks`, `Transactions`, `Uncles`, `Withdrawals`, `BlockAccessLists`, `Receipts`, `Logs` — chain bodies.
- `Metadata` (`IChainMetadataStore`) — sync cursors (last block / last fetched header / body / receipt), checkpoints, snap-sync state, and header-skeleton state.
- `Diffs` (`IStateDiffStore`) — the flat-state value reverse-diff journal (rewind); `JournalEnabled` says whether it is on.
- `TrieNodes` / `StateTrieNodes` (`ITrieNodeStore`) — the trie node store; the same object in hash mode, a distinct path-keyed store otherwise.
- `NodeCommitBlockSource` (`NodeCommitBlockContext`) — the block-bracket arming site for node-history journaling.
- `BeginBatch()` (`IBundleBatch`) commits block data first and the metadata cursor last, so the cursor never runs ahead of the data.

An in-memory implementation (`InMemoryChainStoreBundle`) is provided for tests; the RocksDB implementation lives in Nethereum.CoreChain.RocksDB. (The `ITrieNodeStore` interface and in-memory trie stores themselves live in Nethereum.Merkle.Patricia.)

## State roots & proofs

```csharp
public interface IIncrementalStateRootCalculator
{
    Task<byte[]> ComputeStateRootAsync();
    Task<byte[]> ComputeStateRootAsync(byte[] previousStateRoot);
    Task<byte[]> ComputeStateRootWithoutPersistAsync(byte[] previousStateRoot);
    Task PersistPendingStateAsync();
    void DiscardPendingState();
    Task<byte[]> ComputeFullStateRootAsync();
}
```

- **`IncrementalStateRootCalculator`** computes the state root without walking the full flat state each block. `ComputeStateRootAsync(previousStateRoot)` warm-starts from the previous root (lazy-loading nodes through the trie store) and applies only the dirty accounts and storage slots, warm-starting each contract's storage trie from its persisted root and handling SELFDESTRUCT wipes; `ComputeFullStateRootAsync()` is the full-walk fallback. `ComputeStateRootWithoutPersistAsync` plus `PersistPendingStateAsync` / `DiscardPendingState` let a caller compute a candidate root and only then commit the nodes. `BinaryIncrementalStateRootCalculator` / `BinaryStateRootCalculator` are the EIP-7864 binary-trie variants; `PatriciaBlockRootCalculator` is the block-level (tx/receipt/withdrawal) root calculator.
- **`ProofService`** (`IProofService`) generates EIP-1186 account + storage proofs. When backed by a trie node store it gates on `ContainsKey(stateRoot)` (throwing `StateNotAvailableException` → JSON-RPC −32000 when the root is gone), loads the trie at that root, and derives the account fields from the walked root — so an as-of-N proof reports the as-of-N account. Storage proofs key on `owner = keccak(address)` for the path-keyed keyspace. As-of-block proofs are served through `IHistoricalProofCapable` (`ProofServiceAsOf`).

## Validation, anchoring & rewind

```csharp
public interface IValidationPolicy
{
    bool ShouldAnchorAt(ulong blockNumber);
    ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber);
}

public enum ValidationAction
{
    Continue,
    RewindAndRetry,
    Fatal,
}
```

- **`StrictValidationPolicy`** is the shipped policy: an EVM bug is `Fatal`; a peer lie triggers `RewindAndRetry`.
- **`ICanonicalStateRootSource`** / `CanonicalTip` — the trusted-tip abstraction that anchors following and pivot selection. CoreChain ships `FixedTipCanonicalSource`, `CompositeCanonicalStateRootSource`, `RpcCanonicalSource` and `MainnetKnownCheckpoints`; the beacon `LightClientCanonicalSource` and the AppChain anchor source live in their own packages.
- **Rewind** is `RewindCoordinator` (`IRewindCoordinator`):

```csharp
Task<RewindResult> RewindToAsync(
    ulong targetBlock,
    RewindPolicy policy,
    CancellationToken ct = default);

public enum RewindPolicy
{
    JournalFirstThenSnapshot,
    JournalOnly,
    SnapshotOnly,
}

public enum RewindOutcome
{
    NoOp,
    JournalUsed,
    NodeHistoryUsed,
    SnapshotUsed,
    NoPathAvailable,
}
```

  With `JournalFirstThenSnapshot` it first replays the flat-state value diffs (`StateRewindService`). When the state is path-keyed and the bundle implements `INodeHistoryRecoverable`, it then recovers the trie through `RecoverToAsync(targetBlock, FlatRecoverySource.ReplayJournal, …)`, falling back to `FlatRecoverySource.ReconcileFromTrie`; if neither path covers the target it restores the nearest checkpoint. The `RewindResult` record reports `Outcome`, `NewHead`, `UndoneCount`, `RestoredCheckpoint` and a human-readable `Detail`.

> The consensus-admission seam — `IConsensusBlockGate`, the `ConsensusGatedBlockExecutor` decorator that wraps an `IBlockExecutor` with it, and the pass-through `AlwaysAcceptConsensusBlockGate` — lives here in CoreChain (`Sync/`). Only the *policy* that answers it for mainnet, `LightClientConsensusBlockGate`, lives in Nethereum.MainnetChain. Gating is an extension point, not baked into execution.

## Snap-sync state model

CoreChain defines the *persisted* snap-sync state consumed by the follower's fast-start: `SnapSyncState` (`Phase`, pivot, heal target, task cursors, counters), `SnapSyncAccountTask` and `SnapSyncCounters`.

```csharp
public enum SnapPhase : byte
{
    NotStarted    = 0,
    Phase2Running = 1,
    Phase3Running = 2,
    Complete      = 3,
    Generating    = 4,
}
```

The snap-sync *orchestrator* (the client, phases, peer serving) lives in Nethereum.DevP2P.Sync.

## JSON-RPC

`RpcHandlerRegistry` + `RpcDispatcher` host a full `eth_*` / `net_*` / `web3_*` handler set (`AddStandardHandlers()`), including filters, `eth_blobBaseFee` and `debug_traceTransaction` / `debug_traceCall`.

`eth_subscribe` / `eth_unsubscribe` are **not** in that set and are not `IRpcHandler`s. Subscriptions are served by `WebSocketRpcHandler` (`Rpc/Subscriptions/WebSocketRpcHandler.cs`), which owns a `SubscriptionManager`, answers those two methods itself inside `HandleConnectionAsync(WebSocket, CancellationToken)` and delegates everything else to the dispatcher; call `BroadcastBlockAsync(BlockHeader, byte[] blockHash, List<FilteredLog>)` on it after each block to push `newHeads` and `logs`. A node wired from `AddStandardHandlers()` alone answers method-not-found to `eth_subscribe`. `EthGetProofHandler` selects the historical vs latest proof service per request and maps `StateNotAvailableException` to −32000.

## Architecture notes

- **Chain-agnostic core**: one `BlockExecutor` runs mainnet and AppChains, differing only by `IChainActivations` + config factories.
- **Executor → importer → follower → gate**: each layer is a seam. Consensus gating wraps the same `IBlockExecutor` interface without the core depending on it.
- **Storage-mode by lifecycle**: hash-keyed (content-addressed, ephemeral, `TrieNodes == StateTrieNodes`) vs path-keyed (location-addressed, persistent node history). Hash mode is byte-identical to legacy behaviour.
- **Pluggable, incremental state root**: injected into `BlockExecutor`; warm-start + dirty-account updates avoid full-state walks on mainnet.
- **Symmetry contract**: the block-execution steps mirror the Zisk guest executor in Nethereum.EVM.Core so AppChain state transitions are provable.

## Reusing the core for an AppChain

`Nethereum.CoreChain` + `Nethereum.CoreChain.RocksDB` + `Nethereum.DevP2P` + `Nethereum.DevP2P.Sync` are the
**reusable node core**. `Nethereum.MainnetChain` is **configuration and policy only** — it supplies mainnet's
concrete answers to the core's seams and wires them together; it holds no reusable mechanism. An AppChain builds
a node by implementing the same seams with its own answers:

| Seam (in the core) | Mainnet fills it with | An AppChain fills it with |
|---|---|---|
| `IChainActivations` (fork schedule) | `MainnetChainActivations` | its own fork/genesis activations |
| `ICanonicalStateRootSource` (what's canonical at block N / the tip) | `LightClientCanonicalSource` (beacon LC) | an L1-anchor / sequencer-attestation source |
| `IConsensusBlockGate` (admit a block as canonical) | `LightClientConsensusBlockGate` | its own admission rule (or `AlwaysAcceptConsensusBlockGate`) |
| `IFinalityCursorProvider` (finalized/safe labels for RPC) | `LightClientFinalityCursorProvider` | an anchor/sequencer cursor, or `LatestOnlyFinalityCursorProvider` |
| `IPeerHandshakeWorker` (dial with a chain identity) | `MainnetPeerHandshakeWorker` (mainnet genesis + chainId) | `ChainPeerHandshakeWorker(genesisHash, networkId, …)` |
| `IBlockExecutor` factory + `IIncrementalStateRootCalculator` | mainnet config factories | the AppChain's config factories |

Everything downstream of those seams — the executor, importer, producer, follower/gate pipeline, snap bootstrap
(`SnapSyncOrchestrator`), peer pool (`PeerPoolManager`, including the trusted-peer keeper), fetch scheduler,
backward header walker, RocksDB storage and recovery (`BootRecoveryGate`) — is the same production code for
every chain. The rule of thumb: a generic **mechanism** lives in the core and takes an interface; the concrete
**policy** (beacon light client, mainnet bootnodes/genesis constants) stays in `MainnetChain`. If a plausible
AppChain would supply its own implementation of something, that something is a core seam, not mainnet code.
