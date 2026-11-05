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
    bool ExternalHeaderFollow = false)
{
    public AsyncGate ChainWriteGate { get; init; }
    public Func<CancellationToken, Task> AfterRewindAsync { get; init; }
}

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

## Execution stack

`ExecutionStackFactory.Build` composes the executor, the state-root calculator and the importer for one node role from a single options object, so a follower, an AppChain producer and DevChain run the same wiring and differ only in what they pass. The returned `ExecutionStack` exposes `Executor`, `Calculator`, `Importer`, `Prefetcher`, `Commit` (the `BlockCommit` sequence that the importer and the producer share) and `SideChain`, and `NewImporter()` builds another importer over the same executor with the same role options. `RewardPolicy` is required: building without one throws `ArgumentException`.

```csharp
public static ExecutionStack Build(
    IChainStoreBundle bundle,
    IChainActivations activations,
    Func<HardforkName, ChainConfig> chainConfigFactory,
    Func<HardforkName, HardforkConfig> hardforkConfigFactory,
    ExecutionStackOptions options);

public sealed class ExecutionStackOptions
{
    public const int DefaultStateRootWorkers = 4;

    public IRewardPolicy? RewardPolicy { get; init; }
    public Func<BlockHeader, string>? AuthorResolver { get; init; }
    public bool SealedByClique { get; init; }
    public bool FeeFreeBaseFee { get; init; }
    public SideChainOptions? SideChain { get; init; }
    public IFlushCadence? FlushCadence { get; init; }
    public int PrefetchWorkers { get; init; }
    public int StateRootWorkers { get; init; } = DefaultStateRootWorkers;
    public StatePrefetcher? Prefetcher { get; init; }
    public ITransactionVerificationAndRecovery? TransactionVerifier { get; init; }
    public ILoggerFactory? LoggerFactory { get; init; }
    public IStateStore? StateOverride { get; init; }
    public Func<IStateStore, ITrieNodeStore, IIncrementalStateRootCalculator>? StateRootCalculatorFactory { get; init; }
}

public sealed class SideChainOptions
{
    public bool AdoptByDifficulty { get; init; }
    public bool AcknowledgeKnownCanonicalBlocks { get; init; } = true;
    public bool PersistMetadata { get; init; } = true;
}

public static IFlushCadence? ForBlocks(int blocks, string paramName = "blocks");
```

`PrefetchWorkers` is the number of state-prefetch threads (0 leaves prefetch off; a negative value is refused) and `StateRootWorkers` the number of threads that update storage tries in parallel when the root is computed. `FlushCadences.ForBlocks(blocks)` turns a configured interval into an `IFlushCadence`: `null` for 1 (flush every block), a `FixedIntervalFlushCadence` for more, and an `ArgumentException` for less than 1. The secp256k1 recovery and signing contexts are initialised when the executor is composed, so the first block does not pay for them.

## State prefetch and the parallel state root

Block execution spends most of its time waiting on state reads, so a block can be warmed before the serial executor reaches it. `StatePrefetcher` runs a block's transactions on dedicated worker threads against a throwaway layer, which loads the accounts, slots and code they touch into the store's caches without writing anything to the store; `Begin(header, transactions)` returns a `PrefetchSession`, or `null` when the prefetcher is disposed or cannot plan the block, and the importer imports the block either way. When the stack has a prefetcher the importer starts a session for each block and carries it to the executor in `BlockExecutionOptions.Prefetch`; `ExecutionStackOptions.Prefetcher` shares one prefetcher between stacks.

```csharp
public sealed class StatePrefetcher : IDisposable
{
    public const string WorkerThreadNamePrefix = "state-prefetch-";
    public static readonly TimeSpan DefaultShutdownBound = TimeSpan.FromSeconds(5);

    public StatePrefetcher(
        IStateStore state,
        IBlockStore blocks,
        IChainActivations activations,
        Func<HardforkName, ChainConfig> chainConfigFactory,
        Func<HardforkName, HardforkConfig> hardforkConfigFactory,
        Func<BlockHeader, string>? authorResolver,
        int workers,
        TimeSpan? shutdownBound = null);

    public int Workers { get; }
    public int LiveWorkerThreads { get; }
    public int Wakeups { get; }
    public PrefetchSession? Begin(BlockHeader header, IList<ISignedTransaction> transactions);
    public void Dispose();
}
```

- `PrefetchSession.Outcome` reports how many transactions were prefetched, skipped or failed, and `TriePathOutcome` how many trie paths were enqueued, warmed, dropped, failed or skipped. A prefetch worker that throws is counted as failed and the block still imports with the same roots and receipts as with prefetch off.
- The session also warms the trie: `StartTriePaths` attaches an observer to the state layer, and each account or slot the real execution dirties has its trie path read on the prefetch threads, so the state root finds those nodes already loaded. The queue is bounded at `PrefetchSession.TriePathQueueCapacity` (4096); further keys are dropped and counted, never blocked on.
- Senders are recovered once per transaction on `sender-recovery-` threads, at the start of the session, and the executor reuses the recovered sender when it sees the same transaction instance.
- `Dispose()` joins the worker threads within `shutdownBound`; rebuilding an execution stack repeatedly does not accumulate threads.
- `IncrementalStateRootCalculator` takes `stateRootWorkers` (default 1 on the constructor, `ExecutionStackOptions.DefaultStateRootWorkers` = 4 through the stack). With more than one, the dirty storage tries of a block are updated in parallel on dedicated worker threads that start on the first incremental block and are not recreated; with 1 no worker thread starts. The root is identical for any worker count, and a storage-trie update that throws fails the block instead of returning a partial root.

## Execution statistics

`BlockExecutionResult.Stats` and `BlockImporterResult.Stats` carry a `BlockExecutionStats` when the state store chain supports read metering (`IStateReadMeterHost`): account, storage and code read times and counts (`AccountReads`, `StorageReads`, `CodeReads`, `AccountLoaded`, `StorageLoaded`, `CodeLoaded`, `CodeLoadBytes`), the root phases (`AccountUpdates`, `AccountHashes`, `AccountCommits`, `StorageUpdates`, `StorageCommits`), the updated and deleted counts, `PrecompileCalls` (calls per precompile address), and `Execution` (which excludes state-read time), `Validation`, `DatabaseCommit`, `BlockWrite`, `TotalTime`, `MgasPerSecond`, `TransactionCount` and `GasUsed`. `BlockImporter` writes one `block.exec.stats` log line per block from it, listing only the precompiles that were called.

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
    public bool FeeFreeBaseFee { get; set; }
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
    public List<ISignedTransaction> DeferredTransactions { get; set; } = new();
    public List<DroppedTransaction> DroppedTransactions { get; set; } = new();
}
```

The `BlockProducer` constructor takes the engine, the `IBlockStore` / `ITransactionStore` / `IReceiptStore` / `ILogStore` / `IStateStore` / `ITrieNodeStore` / `IIncrementalStateRootCalculator`, and then, in this order, the optional `ITransactionOrderingPolicy`, `IBlockHashProvider`, `IBlockEncodingProvider` and `IBlockRootsProvider` seams, followed by an `IWithdrawalStore` and a `NodeCommitBlockContext`. Those optional parameters are positional with a `null` default, so passing them by name is the safe way to reach the later ones. A second overload adds the `IBlockAccessListStore`, the `hardforkConfigFactory` and an optional `BlockCommit`, and requires every parameter before them to be passed. `ITransactionOrderingPolicy.Order(pool, blockContext, ct)` no longer takes a gas limit: the executor's own fit check against the gas left after actual use decides what goes in.

**Faults, deferral and drops.** When a transaction faults the host rather than reverting in the EVM, `BlockProducer` isolates it: the transaction is reported in `DroppedTransactions` with its reason, the same sender's later transactions are returned in `DeferredTransactions`, and the block is produced again from the rest. After four drops in one block the fifth cuts the block to the transactions before it and defers the rest, so a block costs at most six executions. The retry runs only when the executed prefix can be reverted (an atomic commit, or the `HistoricalStateStore` journal); over a bare store the block fails as it did before. A transaction that did not fit the gas or blob budget left in the block is deferred only if it would fit an empty block; otherwise it is dropped, and its sender's later nonces stay deferred. `FeeFreeBaseFee` keeps the base fee at zero in production, header validation, `eth_baseFee` and `eth_feeHistory` on a chain that sets `ChainConfig.FeeFreeBaseFee`. From Osaka the producer stops adding transactions before the encoded block would reach the 8,388,608-byte limit of EIP-7934, and the cut transactions stay pending. `BuildBlockOnParentAsync(transactions, orderLikeMempool, options, parentHash, parentHeader)` builds a block on a given parent instead of the head.

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

## Transaction admission

`TransactionAdmissionRules` is the one admission rule that JSON-RPC submission and peer relay share, so a transaction is judged the same way wherever it enters. `FirstRefusal(tx)` returns the first reason a transaction is refused, or `null`.

```csharp
public sealed class TransactionAdmissionRules
{
    public const int DefaultMaxTransactionSizeBytes = 128 * 1024;
    public const int DefaultMaxBlobTransactionSizeBytes = 1024 * 1024;

    public string? FirstRefusal(ISignedTransaction tx);
    public string? BlobCountReason(ISignedTransaction tx);
    public string? OversizeReason(ISignedTransaction tx);
    public string? InvalidSignatureReason(ISignedTransaction tx);
    public string? WrongChainIdReason(ISignedTransaction tx);
    public string? GasLimitTooHighReason(ISignedTransaction tx);
    public string? FeeCapBelowPriorityFeeReason(ISignedTransaction tx);
}
```

A blob transaction must carry between one and `MAX_BLOBS_PER_TX` blobs and is measured with its sidecar against 1 MiB; every other type is capped at 128 KiB. The constructor takes the chain id, the gas limit cap and, optionally, `maxTransactionSizeBytes` and `maxBlobTransactionSizeBytes`.

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
    public PrefetchSession? Prefetch { get; init; }
    public int? TraceTxIndex { get; init; }
    public BlockExecutionRole Role { get; init; } = BlockExecutionRole.Building;
    public SimulateCallOptions? SimulateCallOptions { get; init; }
    public BigInteger SimulateGlobalGasUsedBefore { get; init; }
    public List<AccountChanges>? DeclaredBlockAccessList { get; init; }
}
```

`Role` is the difference between sealing a block and judging one: `Validating` compares the executed result against what the header *declares*, and `Simulating` runs the block for `eth_simulateV1`-style speculative execution, with `SimulateCallOptions` (`Validation`, `TraceTransfers`, per-account `ExpectedNonces`) tuning that mode. `SimulateGlobalGasUsedBefore` is `eth_simulateV1`-only: gas already spent by every prior simulated block in the same request, since a no-gas call is funded from a single RPC gas-cap budget spanning the whole call rather than one that resets per block — a later block's synthetic transactions carry `min(blockGasLimit - blockGasUsedSoFar, RpcGasCap - globalGasUsedSoFar)`. It is zero for the first block and for every non-simulating role. `DeclaredBlockAccessList` carries the block access list as the block declares it (an Engine API payload, an eth/71 body) — EIP-7928's Engine API returns `INVALID` both when the list "is malformed" and when it "doesn't match", and recomputing hashes can only answer the second; only the declared list itself can answer the first.

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
    WithdrawalsRoot,
    BlockRlpSize,
    UnknownParent,
    BlockNumber,
    Timestamp,
    FutureBlock,
    ExtraDataSize,
    DaoExtraData,
    GasLimitTooBig,
    Difficulty,
    Nonce,
    UnclesHash,
    Uncles,
    TransactionsRoot,
    WithdrawalsRootPresence,
    ParentBeaconRootPresence,
    BlockAccessListHashPresence,
    SlotNumberPresence,
    ParentNotHead
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
    bool BlockRlpSizeExceeded => false;
    bool UnknownParent => false;
    bool BlockNumberMismatch => false;
    bool TimestampNotAfterParent => false;
    bool FutureBlock => false;
    bool ExtraDataTooLong => false;
    bool DaoExtraDataMismatch => false;
    bool GasLimitTooBig => false;
    bool DifficultyMismatch => false;
    bool NonceInvalid => false;
    bool UnclesHashMismatch => false;
    bool UnclesInvalid => false;
    bool TransactionsRootMismatch => false;
    bool WithdrawalsRootPresenceMismatch => false;
    bool ParentBeaconRootPresenceMismatch => false;
    bool BlockAccessListHashPresenceMismatch => false;
    bool SlotNumberPresenceMismatch => false;
    bool ParentNotHead => false;
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
| `BlockRlpSize` | `blockRlpSize` | from Osaka, the RLP-encoded block exceeds EIP-7934's 8,388,608 bytes |
| `UnknownParent` | `unknownParent` | the header's parent hash is not a block this node holds |
| `BlockNumber` | `blockNumber` | the header's number is not the parent's plus one |
| `Timestamp` | `timestamp` | the header's timestamp is not after the parent's |
| `FutureBlock` | `futureBlock` | before the merge, with a clock configured, the timestamp is more than 15 seconds ahead of it |
| `ExtraDataSize` | `extraDataSize` | the header's extra data exceeds 32 bytes (not enforced on Clique chains) |
| `DaoExtraData` | `daoExtraData` | a block in the ten after the DAO fork block does not carry `dao-hard-fork` |
| `GasLimitTooBig` | `gasLimitTooBig` | the header's `gasLimit` exceeds 2^63-1 |
| `Difficulty` | `difficulty` | after the merge the difficulty is not zero; before it, the difficulty is not the one `EthashDifficulty` calculates |
| `Nonce` | `nonce` | after the merge the header's nonce is not zero |
| `UnclesHash` | `unclesHash` | after the merge the uncle hash is not the empty-list hash, or the body's uncles do not hash to the header's |
| `Uncles` | `uncles` | before the merge, the body's uncles break the ommer rules (at most two, each recent, unique, not an ancestor, validly headed) |
| `TransactionsRoot` | `transactionsRoot` | the body's transactions do not hash to the header's `transactionsRoot` |
| `WithdrawalsRootPresence` | `withdrawalsRootPresence` | the header carries a `withdrawalsRoot` before Shanghai, or lacks one from it |
| `ParentBeaconRootPresence` | `parentBeaconRootPresence` | the header carries a `parentBeaconBlockRoot` before Cancun, or lacks one from it |
| `BlockAccessListHashPresence` | `blockAccessListHashPresence` | the header carries a block access list hash before Amsterdam, or lacks one from it |
| `SlotNumberPresence` | `slotNumberPresence` | the header carries a slot number before Amsterdam, or lacks one from it |
| `ParentNotHead` | `parentNotHead` | the block does not extend the head and the importer holds no side chain support, so it cannot be executed against the live state |

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

## Header and body rules

`HeaderValidator.Validate` is the single place a header is judged against its parent: extra-data size, nonce, uncle hash, timestamp, difficulty, gas limit and gas used, the gas-limit step bound, block number, base fee, withdrawals root, blob fields, block access list hash and slot number each have a fork-aware rule and a named verdict from the table above. Pass the seal the chain runs under with `SealRules`; a Clique chain is not judged on extra-data size and may share its parent's timestamp.

```csharp
public enum SealRules
{
    ProofOfStake,
    Ethash,
    Clique
}

public static BlockValidityCheck? Validate(
    BlockHeader header,
    BlockHeader parent,
    HardforkName fork,
    HardforkConfig config,
    SealRules seal,
    long? unixNow = null,
    bool daoExtraDataRequired = false,
    bool uncle = false,
    bool feeFreeBaseFee = false);
```

`BlockBodyValidator.ValidateAsync(header, transactions, uncles, withdrawals, fork, seal, unixNow)` judges the body: the transactions must hash to the header's `transactionsRoot`, the withdrawals list must be present exactly when the header carries a withdrawals root, and before the merge the uncles must follow the ommer rules (`MaximumUncles` 2, within the `AncestorWindow` of 7 ancestors, unique, not an ancestor, validly headed). `EthashDifficulty.Calculate(fork, timestamp, parent)` is the pre-merge difficulty the `Difficulty` check compares against. `BlockExecutor` takes a `BlockValidationOptions` (`SealedByClique`, `FeeFreeBaseFee`, `UnixClock`, `UncleStore`, `TransactionVerifier`) that selects the rules for its chain, and `BlockImporter` runs them before it executes anything, so a refused block names the failing rule in `FailedChecks` and persists nothing.

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
    ITrieNodeStore NodeStore => null;
    BlockTrieSet BeginBlockTries(byte[] parentRoot) => null;
}
```

- **`IncrementalStateRootCalculator`** computes the state root without walking the full flat state each block. `ComputeStateRootAsync(previousStateRoot)` warm-starts from the previous root (lazy-loading nodes through the trie store) and applies only the dirty accounts and storage slots, warm-starting each contract's storage trie from its persisted root and handling SELFDESTRUCT wipes; `ComputeFullStateRootAsync()` is the full-walk fallback. `ComputeStateRootWithoutPersistAsync` plus `PersistPendingStateAsync` / `DiscardPendingState` let a caller compute a candidate root and only then commit the nodes. `BinaryIncrementalStateRootCalculator` / `BinaryStateRootCalculator` are the EIP-7864 binary-trie variants; `PatriciaBlockRootCalculator` is the block-level (tx/receipt/withdrawal) root calculator.
- **`ProofService`** (`IProofService`) generates EIP-1186 account + storage proofs. When backed by a trie node store it gates on `ContainsKey(stateRoot)` (throwing `StateNotAvailableException` → JSON-RPC −32000 when the root is gone), loads the trie at that root, and derives the account fields from the walked root — so an as-of-N proof reports the as-of-N account. Storage proofs key on `owner = keccak(address)` for the path-keyed keyspace. As-of-block proofs are served through `IHistoricalProofCapable` (`ProofServiceAsOf`).
- **`BlockHashesTree`** (`Nethereum.CoreChain.DataAvailability`) commits a list of block hashes to a Merkle root; `VerifyProof(root, blockHash, index, proof, totalLeaves)` checks one hash against it. The root does not commit to the leaf count, so `totalLeaves` must come from the trusted anchor, never from the prover; a prover-chosen `totalLeaves` can make a proof verify at a different position.

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
- **`ICanonicalStateRootSource`** / `CanonicalTip` — the trusted-tip abstraction that anchors following and pivot selection. CoreChain ships `FixedTipCanonicalSource`, `CompositeCanonicalStateRootSource`, `RpcCanonicalSource` and `MainnetKnownCheckpoints`; `TrustedHeaderCanonicalSource` (over any `ITrustedHeaderProvider`, such as the beacon light client) and the AppChain anchor source live in their own packages.
- **Rewind** is `RewindCoordinator` (`IRewindCoordinator`):

```csharp
Task<RewindResult> RewindToAsync(
    ulong targetBlock,
    RewindPolicy policy,
    CancellationToken ct = default);

Task<RewindResult> RewindToAsync(
    ulong targetBlock,
    RewindPolicy policy,
    RewindScope scope,
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

## Side chains and reorgs

A `BlockImporter` built with `SideChainSupport` (through `ExecutionStackOptions.SideChain`) stores a block that does not extend the head as a side block instead of refusing it, and can later make that branch canonical. `BlockImporterResult.Disposition` says where the block landed; a stack without side-chain support refuses a non-extending block with the `ParentNotHead` check.

```csharp
public enum BlockImportDisposition
{
    ExtendedCanonical,
    StoredAsSide,
    AlreadyCanonical,
    AlreadySide,
    Refused,
    UnknownParent
}

public enum SetCanonicalStatus
{
    NoOp,
    Reorganised,
    BranchInvalid,
    AncestryUnavailable,
    RewindUnavailable,
    TooDeepReorg
}

public enum RewindScope
{
    ExecutionAndChain,
    ExecutionOnly
}
```

- `BlockImporter.SetCanonicalAsync(blockHash, ct)` rewinds to the common ancestor and replays the branch through the same import path, returning a `SetCanonicalResult` with the `ChainReorganisation` (`CommonAncestor`, `DroppedBlockHashes`, `AddedBlockHashes`, `FailedBlockHash`) and the transactions the dropped blocks carried. A branch whose block fails is `BranchInvalid`, `BlockImporterResult.LatestValidHash` (from `ChainReorganisation.FailedBlockParentHash`) names its last good parent, and the hash is remembered (`TryGetInvalidBlock(blockHash, out latestValidHash)`). A branch whose ancestry cannot be walked back within `SideChainSupport.MaxReorgDepth` (default `DefaultMaxReorgDepth` 64) is `AncestryUnavailable`, one that would drop more canonical blocks than that is `TooDeepReorg`, and one the rewind journal cannot reach is `RewindUnavailable`; in each case nothing rewinds.
- `ISideBlockStore` (`Put`, `TryGet`, `Contains`, `Remove`, `PruneBelow`) holds the side blocks; `InMemorySideBlockStore` is the shipped store, and `CanonicalOrSideBlockStore` reads the canonical store first and the side store second. `PruneSideBlocksBelow(blockNumber)` drops side blocks below a finalized height.
- `SideChainOptions.AdoptByDifficulty` lets `DifficultyForkChoice` adopt a heavier branch; a zero-difficulty (post-merge) branch is adopted when it arrives.
- `IRewindCoordinator.RewindToAsync(targetBlock, policy, scope, ct)` takes a `RewindScope`: `ExecutionOnly` rewinds execution state and leaves the chain data to the writers that own it, which is what an Engine-driven follower needs when the consensus layer re-lays the top of the chain. Every executed block records the hash it executed (`IChainMetadataStore.SaveExecutedHash` / `GetExecutedHash`, kept for the last 1,024 heights), so the tip path finds the common ancestor from that record, including after a restart, and `BlockImporter` and `ConsensusGatedBlockExecutor` rewind through one shared primitive.
- A follower that is handed a competing block gives it to the importer and backs off, and a gap (a block whose parent is unknown) is a gap, not an invalid block. `FollowerOptions.ChainWriteGate` (an `AsyncGate`) is the per-block write gate shared with the Engine API: one writer executes at a time, the gate carries the executed head every writer publishes, and a follower that reaches a block another writer already executed skips it. `FollowerOptions.AfterRewindAsync` runs after a rewind so the host can drop warm state.

## Engine API follower

`FollowerEngineApiService` is the follower's `IEngineApiService`: it takes `newPayload` and `forkchoiceUpdated` from a consensus client and drives the same importer the sync loop uses, under the same write gate.

```csharp
public sealed class EnginePayloadStashOptions
{
    public const int DefaultMaxBlocks = 128;
    public const long DefaultMaxBytes = 256L * 1024 * 1024;

    public int MaxBlocks { get; set; } = DefaultMaxBlocks;
    public long MaxBytes { get; set; } = DefaultMaxBytes;
}
```

- A payload that extends the executed head is imported once and answered `VALID`, even before the first `forkchoiceUpdated`. A payload whose parent is known but is not the head is answered `ACCEPTED` and stashed without execution; one with an unknown parent is `SYNCING` and the devp2p sync is pointed at the forkchoice head.
- A wrong state root, a timestamp that is not after the parent's, or a declared hash that is not the block's hash is `INVALID` (`INVALID_BLOCK_HASH` for the hash) with the parent as `latestValidHash`, and its descendants stay `INVALID`. A transaction that does not decode is `INVALID`, not an internal error. `newPayloadV3` to `V5` compare `expectedBlobVersionedHashes` with the payload's blob transactions in order: a mismatch is `INVALID` (`EnginePayloadDecoder.CheckBlobVersionedHashes`).
- `forkchoiceUpdated` to a sibling head runs the reorg path, to a head whose reorg would drop more than `MaxReorgDepth` blocks answers `-38006` and leaves the head, to the current head is a no-op, and to a head that no store, stash or peer holds is `SYNCING`. A head or finalized hash fetched from peers is used only when the fetched header hashes to the requested hash (`EngineHeaderResolver`), so a peer cannot publish a forged header as the sync target. `forkchoiceUpdated` drops the side blocks below the finalized block.
- `EnginePayloadStash` keeps at most `MaxBlocks` blocks and `MaxBytes` of retained transaction data, evicting the oldest first and always keeping the newest; `RetainedBytes` reports the current figure.
- `SetHeadAsync(target)` rewinds the chain to a block number through the same reorg path.

## JSON-RPC

`RpcHandlerRegistry` + `RpcDispatcher` host a full `eth_*` / `net_*` / `web3_*` handler set (`AddStandardHandlers()`), including filters, `eth_blobBaseFee`, `eth_baseFee` (the base fee the next block must carry), `eth_getStorageValues` (up to 1,024 slots in one call, each answered as a 32-byte word) and `debug_traceTransaction` / `debug_traceCall`. `AddStandardHandlers(RpcModuleSet)` registers only the namespaces a node lists: `RpcModuleSet.Parse("eth,net,web3")` (`RpcModuleSet.DefaultModules`), with `txpool`, `debug` and `admin` as opt-in names; an unknown name throws `ArgumentException`, and a method of an omitted namespace is not served.

```csharp
public interface IRpcDispatcher
{
    Task<RpcResponseMessage> DispatchAsync(RpcRequestMessage request);
    Task<List<RpcResponseMessage>> DispatchBatchAsync(RpcRequestMessage[] requests);
    int MaxBatchSize { get; set; }
}
```

`RpcDispatcher` and `InstrumentedRpcDispatcher` implement `IRpcDispatcher`, and `JsonRpcEnvelope` is the HTTP-shaped front end over it: it answers a batch above `MaxBatchSize` (`RpcDispatcher.DefaultMaxBatchSize`, 1,000) with one `BatchTooLarge` error carrying the id of the first call, before it parses the members (`RpcBatchGuard.RejectOversizedBatch`), sends no response to a notification, answers a batch member that is not a request object with `-32600` and the other members normally, caps a batch response at `JsonRpcEnvelope.DefaultMaxResponseSize` (25,000,000 bytes) with `ResponseTooLargeCode` for the members that no longer fit.

- Invalid input is `-32602` and never `-32603`: parameters are read through `RpcJsonElementExtensions` (`RequireRpcString`, `GetOptionalRpcString`, `RequireRpcObject`, `GetRpcString`), a lone UTF-16 surrogate is refused, and `-32603` is always the plain text `Internal error` with no exception text. `HistoricalStateNotAvailableException` answers `-32000` with its message. An unknown method is metered under the single key `InstrumentedRpcDispatcher.UnknownMethodMetric` (`unknown`) and its name is truncated in the error text, and the log lines a caller can trigger without credentials (unknown method, client-input errors) are written at Debug.
- `eth_call`, `eth_estimateGas`, `eth_createAccessList` and `eth_simulateV1` run under `ChainConfig.RpcEvmTimeout` (5 seconds), and the `debug_trace*` methods under `RpcTraceTimeout` (5 seconds), answering `-32000` `execution aborted` when it passes; the whole `eth_estimateGas` search shares one deadline. `RpcGasCap` bounds the gas of a call.
- `eth_getLogs`, `eth_newFilter` and `eth_subscribe("logs")` refuse a filter with more than 4 topic positions, or more than `ChainConfig.RpcLogQueryLimit` (1,000) addresses or values in a position, with `-32602`; a log filter installed through `eth_newFilter` starts at the head, and a poll returns only logs of blocks since the last poll under the `getLogs` caps. Filter ids are random 128-bit values.
- `eth_feeHistory` takes at most 100 reward percentiles, strictly ascending within [0, 100].

`eth_subscribe` / `eth_unsubscribe` are **not** in that set and are not `IRpcHandler`s. Subscriptions are served by `WebSocketRpcHandler` (`Rpc/Subscriptions/WebSocketRpcHandler.cs`), which owns a `SubscriptionManager`, answers those two methods itself inside `HandleConnectionAsync(WebSocket, CancellationToken)` and delegates everything else to the dispatcher it is given (`IRpcDispatcher`); call `BroadcastBlockAsync(BlockHeader, byte[] blockHash, List<FilteredLog>)` on it after each block to push `newHeads` and `logs`. A node wired from `AddStandardHandlers()` alone answers method-not-found to `eth_subscribe`; `SubscriptionsEnabled` follows the `eth` module. A message is read to its end under `WebSocketRpcHandler.MaxMessageSize` (32 MiB), each connection has an outbox with a `WriteTimeout` of 10 seconds so one slow subscriber cannot stall a broadcast, and `SubscriptionManager.MaxSubscriptionsPerConnection` (200) caps the subscriptions on a connection. `SubscriptionBroadcaster` hands blocks from the producer to the fan-out through a bounded queue of `QueuedBlocks` (128) that drops the oldest, so a stuck subscriber never holds up block production. `EthGetProofHandler` selects the historical vs latest proof service per request and maps `StateNotAvailableException` to −32000.

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
| `ICanonicalStateRootSource` (what's canonical at block N / the tip) | `TrustedHeaderCanonicalSource` (beacon LC) | an L1-anchor / sequencer-attestation source |
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
