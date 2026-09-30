# Indexing & Storage — Nethereum 7.0

Nethereum 7.0 brings the SQL-backed indexing stack — `Nethereum.BlockchainProcessing`, the `Nethereum.BlockchainStore.*` EF Core providers (Postgres / SqlServer / Sqlite), the `Nethereum.BlockchainStorage.Processors.*` hosted services, and the `Nethereum.TokenServices` / `Nethereum.DataServices` / `Nethereum.Sourcify.Database` companions — up to the post-Prague / Amsterdam data model. These packages all pre-date 6.1.0, so this note is the 7.0 delta only, not a re-description of the crawler/orchestrator framework. New this release: an EIP-7928 block-access-list index (crawl step, storage entity, repository interface, EF Core + in-memory backends), an EIP-7708 native-transfer discrimination path so a system-emitted `Transfer` log is not double-counted as a token balance, a second internal-transaction source that replays the transaction through `Nethereum.EVM` for nodes without `debug_traceTransaction`, EIP-7702 authorization-list persistence and Amsterdam header fields (`blockAccessListHash`, `slotNumber`) carried through to storage, and Amsterdam block data in `Nethereum.Explorer`. `Nethereum.DataServices` and `Nethereum.Sourcify.Database` carry documentation-only changes.

## Nethereum.BlockchainProcessing — EIP-7928 block access list index

The block access list (EIP-7928, Amsterdam) is now a first-class indexable stream: a crawl step, a storage entity with a round-tripping mapping, a repository interface, and an in-memory backend, so an indexer can persist and re-serve a block's declared per-account access list.

* `BlockCrawlOrchestrator.IndexBlockAccessLists` — the opt-in switch (off by default) that enables the access-list crawl step; on a reorg the orchestrator marks stored access lists non-canonical through its `NonCanonicalBlockAccessListRepository`
* `BlockAccessListCrawlerStep` (`CrawlerStep<BlockAccessListVO, BlockAccessListVO>`, `Enabled = false` by default) and `BlockAccessListStorageStepHandler` (`ProcessorBaseHandler<BlockAccessListVO>`) — the pipeline stage that carries the access list into storage
* `BlockAccessListAccount` (a `TableRow` storage entity) with `BlockAccessListAccountMapping.MapToStorageEntityForUpsert(blockNumber, blockHash)` / `.ToAccountAccess()` — flattens an RPC `AccountAccess` (storage reads/changes, balance/nonce/code changes) to stored columns and rehydrates it byte-for-byte
* `IBlockAccessListRepository` (`UpsertAsync`, `GetForBlockAsync`, `MarkNonCanonicalAsync`) with `InMemoryBlockAccessListRepository`, wired onto `IBlockchainStoreRepositoryFactory.CreateBlockAccessListRepository()`
* `Block.BlockAccessListHash` and `Block.SlotNumber` (EIP-7928) added to the storage `Block` entity, `IBlockView`, `BlockMapping`, `BlockEntityBuilder` and the Postgres migration, so an Amsterdam block's new header fields reach the database

## Nethereum.BlockchainProcessing — EIP-7708 native-transfer discrimination

EIP-7708 (Amsterdam) makes an ETH movement emit the same `Transfer(address,address,uint256)` log an ERC-20 token does, distinguished only by the emitting system address. The token indexer would otherwise treat every ETH send as a token balance change, so the aggregation and denormalization paths now recognise and separate the native emitter.

* `TokenTransferLogViewExtensions.IsNativeTransfer(ITokenTransferLogView)` — true when the emitter is `AddressUtil.SYSTEM_ADDRESS`; `TokenBalanceAggregationService.ProcessTransferAsync` skips native rows so they never create a `TokenBalance`
* `TransferLogFilter` (`Everything`, `NativeTransfers`, `.Negated()`, `.Matches(log)`, `.AsCriteria()`) — a composable predicate over `FilterLog` (native = `IsFromEthTransferEmitter()`), passed through `TokenDenormalizerProcessingService.ProcessBatchAsync` so the live write path and a rebuild apply the same rule and never re-introduce ether; `TokenDenormalizerOptions.LogFilter` selects it (default `TransferLogFilter.Everything`)
* `Nethereum.Util.AddressExtensions.IsNativeTransferLogEmitter(string)` — the shared address check the token stack and explorer consume

## Nethereum.BlockchainProcessing — internal-transaction sources

The trace-based internal-transaction indexer no longer depends on a node exposing `debug_traceTransaction`: `IInternalTransactionSource` abstracts how call-tree entries are produced, with two interchangeable implementations that emit the same `InternalTransaction` shape.

* `IInternalTransactionSource.ProduceAsync(transactionHash)` → `List<InternalTransaction>`
* `DebugTraceInternalTransactionSource(IClient)` — calls `debug_traceTransaction` (Geth call-tracer) and flattens the nested call tree via `InternalTransactionMapping.FlattenCallTrace`
* `EvmReplayInternalTransactionSource(IEthApiService, HardforkConfig)` / `(IEthApiService, ChainForkResolver, chainId)` — replays the transaction locally against `Nethereum.EVM`'s `TransactionExecutor` over pre-transaction state read by RPC (`RpcNodeDataService`), for nodes without the `debug_` namespace; fork rules come from a fixed `HardforkConfig` or per-block `ChainForkResolver`
* `TransactionToTrace` and `IInternalTransactionRepository.GetContractTransactionsInRangeAsync(fromBlock, toBlock)` / `ITransactionRepository.UpdateRevertReasonAsync(txHash, revertReason)` — persistence-method additions so the orchestrator drives a repository interface, not a concrete type; in-memory implementations keep no-DB scenarios working
* Adds a `Nethereum.EVM` project reference to `Nethereum.BlockchainProcessing`

## Nethereum.BlockchainProcessing — EIP-7702 authorization list persistence

An EIP-7702 transaction's authorization list is now carried through to storage as JSON, so an indexed transaction preserves its delegations.

* `TransactionBase.AuthorizationList` (string) and `TransactionMapping.Map` serialise a source transaction's `AuthorisationList` to JSON, leaving the column null when there is none

## Nethereum.BlockchainProcessing — log block timestamp

* `ITransactionLogView.BlockTimestamp` / `TransactionLog.BlockTimestamp` (`long`) persist each log's block timestamp. This is a new interface member, so custom `ITransactionLogView` implementations must add it

## Nethereum.BlockchainStore.EFCore — block-access-list backend and schema

The EF Core layer gains the concrete block-access-list persistence.

* `BlockAccessListRepository` (`RepositoryBase`, `IBlockAccessListRepository`) and `BlockAccessListAccountEntityBuilder` (`IEntityTypeConfiguration<BlockAccessListAccount>`) — the on-disk block-access-list store and its schema; `BlockchainStoreRepositoryFactory` exposes it, `BlockchainDbContextBase` adds the `BlockAccessListAccounts` DbSet, and `EfCoreReorgHandler` marks it non-canonical on reorg
* `TransactionEntityBuilder` carries the new `AuthorizationList` column, `TransactionLogEntityBuilder` the `BlockTimestamp` column, and `BlockEntityBuilder` the `BlockAccessListHash` / `SlotNumber` columns. The Postgres provider ships migration `AddBlockAccessListAndAuthorizationList`, which adds `blocks.blockaccesslisthash`, `blocks.slotnumber`, `transactions.authorizationlist`, `transactionlogs.blocktimestamp` and the `BlockAccessListAccounts` table with its indexes, so a database created by 6.1.0 upgrades with `Database.Migrate()`

## Nethereum.BlockchainStorage.Processors — local-EVM-replay option

The internal-transaction hosted service can select the EVM-replay source instead of `debug_traceTransaction`.

* `BlockchainProcessingOptions.UseLocalEvmReplayForInternalTransactions` and `Hardfork` — when the flag is set, `InternalTransactionProcessingService.ExecuteAsync` builds an `EvmReplayInternalTransactionSource` (per-block `DefaultChainForkResolver.Default`, or a named fork via `DefaultMainnetHardforkRegistry`) rather than a `DebugTraceInternalTransactionSource`; `BlockchainProcessingOptions.Load(IConfiguration)` reads both keys and the DI registration copies both options through

## Nethereum.TokenServices & Nethereum.BlockchainStorage.Token.Postgres — native emitter exclusion

The token-discovery and denormalization companions skip the EIP-7708 system emitter so it is never surfaced as a token.

* `Erc20EventScanner` excludes any `IsNativeTransferLogEmitter()` address from `AffectedTokenAddresses`, and `TokenBalanceRpcAggregationService` skips it when aggregating balances

## Nethereum.Explorer — Amsterdam data in the UI

* Block detail shows the Amsterdam header fields (`slotNumber`, `blockAccessListHash`) alongside the other header fields, and a block-access-list card fed by the new `IBlockAccessListQueryService.GetForBlockAsync`
* EIP-7708 ether movements are shown as the native currency rather than as an unnamed token
* `IExplorerNavContributor` / `ExplorerNavItem` let host packages add navigation entries
* New `tests/Nethereum.Explorer.UnitTests`

## Nethereum.DataServices & Nethereum.Sourcify.Database — documentation only

`Nethereum.DataServices` and `Nethereum.Sourcify.Database` have no shipped code change since 6.1.0; their READMEs were corrected against source (`ABIInfo.ContractABI.Functions`, the Four-Byte `Signatures` result member, `Chain.Rpc[].Url`, the Sourcify Parquet `FilesProcessed` progress field, and the `signature` column name / `Nethereum.DataServices.Sourcify.Database` namespace).
