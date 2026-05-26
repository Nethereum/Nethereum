# Nethereum Aspire Unified Plan

> **PERSISTENT PLAN FILE** - Do NOT delete. Update with progress, new backlog items, and status changes.
> Last updated: 2026-02-23 (session 3 - token pipeline)

---

## Vision

Three-phase approach to build a fully integrated, testable Nethereum infrastructure:

1. **Phase 1 (Current): Aspire DevChain Unified Solution** - Single-chain DevChain with all services (indexing, explorer, MUD, bundler, load testing). Validates reorgs, crawling, MUD indexing, 4337 integration.
2. **Phase 2: Aspire AppChain Template** - Multi-node AppChain with messaging, sync, sequencer, anchoring, P2P — plus all Phase 1 services. Full integrated solution for testing and iteration.
3. **Phase 3: NetDapps Backend Template** - Use Phase 2 as the template/backbone for the NetDapps application backend.

---

## Phase 1: Aspire DevChain Unified Solution

### Architecture
```
AppHost (Orchestrator)
├── Postgres (database)
├── DevChain (Nethereum DevChain JSON-RPC node)
├── Indexer (block/tx/log crawler → Postgres + MUD indexer → Postgres)
├── Bundler (ERC-4337 Account Abstraction bundler)
├── Explorer (Blazor Server - blocks, txs, logs, MUD records, load test dashboard)
├── LoadGenerator (configurable tx scenarios with OpenTelemetry metrics)
└── IntegrationTests (Aspire Testing framework - end-to-end validation)
```

### Project Status

#### DevChain Service [DONE]
- [x] ASP.NET Core host with JSON-RPC POST endpoint
- [x] Single/batch RPC dispatch via `RpcDispatcher`
- [x] CORS, health check GET endpoint
- [x] Aspire service defaults integration
- [x] Configurable ChainId and storage (memory/rocksdb)
- [x] `DevChainHostedService` for auto-start and account funding
- [x] EIP-1559 support: `eth_feeHistory` handler returns `FeeHistoryResult` with sensible defaults
- [x] JSON-RPC compliant error responses (HTTP 200 with error body, not HTTP 500)

#### Bundler Service [DONE - BASIC]
- [x] ASP.NET Core host with JSON-RPC endpoint
- [x] Resolves DevChain URL via Aspire service discovery
- [x] `BundlerRpcServer` integration with debug methods enabled
- [ ] **BACKLOG**: Integration tests for UserOp submission
- [ ] **BACKLOG**: Integration tests for 4337 end-to-end flow (SimpleAccount deploy, send UserOp, validate on-chain)
- [ ] **BACKLOG**: Gas estimation validation tests
- [ ] **BACKLOG**: Paymaster integration

#### Indexer Service [DONE]
- [x] Worker service with `PostgresBlockchainProcessingService` (blocks, txs, logs → Postgres)
- [x] `MudPostgresProcessing` for MUD store records (conditional - only when address configured)
- [x] Resolves DevChain URL + Postgres connection from Aspire
- [x] Reorg support via `ReorgBuffer` and `NonCanonicalBlockRepository`
- [x] Raw DDL schema creation with `IF NOT EXISTS` (EnsureCreatedAsync is no-op when Aspire pre-creates DB)
- [x] Blockchain tables use quoted PascalCase (`"Blocks"`, `"ChainStates"` etc.) matching `.ToTable()` entity builders
- [x] Token transfer log processing (`TokenTransferLogProcessingHostedService`) — ERC20/721/1155 Transfer events
- [x] Token balance aggregation (`TokenBalanceAggregationHostedService`) — computes balances from transfer logs
- [x] Token tables use lowercase convention (`tokentransferlogs`, `tokenbalances`, `nftinventory`, `tokenmetadata`)
- [x] All three hosted services running concurrently: blockchain processor, token log processor, balance aggregator
- [ ] **BACKLOG**: Switch from raw DDL to EF Core migrations (generate up-to-date migration from current model)
- [ ] **BACKLOG**: Verify reorg detection actually works end-to-end (need DevChain reorg simulation)
- [ ] **BACKLOG**: ChainState validation (chain ID mismatch detection)
- [ ] **BACKLOG**: Processing metrics/observability (blocks/sec, lag behind chain head)
- [ ] **BACKLOG**: Graceful shutdown and resume from last processed block
- [ ] **BACKLOG**: Configure `MinimumBlockConfirmations` for reorg safety

#### Explorer Service [IN PROGRESS - ~60%]
- [x] Blazor Server app with Razor components
- [x] Postgres connection via Aspire
- [x] `ExplorerService` - full block/tx/log queries with paging, filtering, canonical-only
- [x] `MudExplorerService` - world addresses, table IDs, records browsing
- [x] `LoadTestMetricsService` - proxies load generator stats
- [x] Pages: Home, Blocks, BlockDetail, Transactions, TransactionDetail, Logs, Account, MudTables, MudRecords, LoadTestDashboard, NotFound
- [x] Shared components: SearchBar, StatsCard, LoadingSpinner, HexDisplay, Pagination
- [x] Layout: MainLayout with navigation
- [ ] **BACKLOG**: Verify all pages actually render correctly with real indexed data
- [ ] **BACKLOG**: Account page - show balance, code, storage (needs RPC calls, not just DB)
- [ ] **BACKLOG**: Real-time updates (SignalR or polling for new blocks/txs)
- [ ] **BACKLOG**: Transaction decode - show decoded input data using ABI
- [ ] **BACKLOG**: Log decode - show decoded event data
- [ ] **BACKLOG**: MUD table decode - decode encoded keys/values to human-readable form
- [ ] **BACKLOG**: Search functionality (block number, tx hash, address)
- [ ] **BACKLOG**: Error states and empty states for all pages
- [ ] **BACKLOG**: CSS/styling polish

#### LoadGenerator Service [DONE]
- [x] Background service with configurable warmup, concurrency, TPS targeting, duration
- [x] Scenarios: ETH transfer, ERC20, Contract Deploy, Mixed (weighted)
- [x] `AccountManager` for parallel worker accounts (funded from master)
- [x] `LoadGeneratorMetrics` with OpenTelemetry (success/fail counts, TPS, latency percentiles)
- [x] `/metrics/stats` and `/metrics/history` REST endpoints
- [x] Worker error handling with backoff
- [x] EIP-1559 transactions (`UseLegacyAsDefault = false`)
- [x] Gas estimation before contract deployments (Erc20Scenario, ContractDeployScenario)

#### Integration Tests [IN PROGRESS - ~50%]
- [x] `AspireFixture` - bootstraps full distributed app, waits for all resources
- [x] `TestContractDeployer` - helper for sending ETH, waiting for indexer catch-up
- [x] `ERC20TestHelper` - deploy/mint/transfer ERC20 for log indexing tests
- [x] `BlockIndexingTests` - chain ID check, send tx → verify in Postgres, blocks indexed
- [x] `LogIndexingTests` - ERC20 transfer/mint events indexed in Postgres
- [x] `LoadGeneratorTests` - metrics endpoint works, has successful txs, traffic is indexed
- [x] `IndexerResilienceTests` - burst of 10 txs indexed, block progress converges to chain head
- [ ] **BACKLOG**: MUD indexing tests (deploy MUD world, set records, verify in Postgres)
- [ ] **BACKLOG**: Reorg simulation tests (need DevChain reorg API - evm_mine with fork)
- [ ] **BACKLOG**: 4337 Bundler tests (deploy SimpleAccount, send UserOp, verify execution)
- [ ] **BACKLOG**: Explorer HTTP tests (hit explorer pages, verify they return 200)
- [ ] **BACKLOG**: Load generator scenario tests (ERC20, deploy, mixed scenarios)
- [ ] **BACKLOG**: Long-running stability test (run for N minutes, verify no drift/leaks)
- [ ] **BACKLOG**: Indexer restart/resume tests

#### Service Defaults [DONE]
- [x] Standard Aspire service defaults (health checks, OpenTelemetry, resilience)

#### Solution Infrastructure [DONE]
- [x] `Directory.Build.props` with `UseProjectReferences=true` and Aspire version
- [x] `nuget.config` with local + nuget.org sources
- [x] Solution file with all projects
- [x] Multi-target fix for `Mud.Repositories.Postgres` and `Mud.Repositories.EntityFramework` (net8.0;net10.0)

### Phase 1 Priority Backlog (Ordered)

| # | Task | Area | Status |
|---|------|------|--------|
| 1 | Verify Explorer pages render with real indexed data | Explorer | TODO |
| 2 | MUD indexing integration tests | Tests | TODO |
| 3 | Reorg simulation + detection tests | Tests/DevChain | TODO |
| 4 | 4337 Bundler end-to-end tests (SimpleAccount, UserOp) | Tests/Bundler | TODO |
| 5 | Explorer search functionality (block/tx/address) | Explorer | TODO |
| 6 | Explorer account page with balance/code from RPC | Explorer | TODO |
| 7 | Explorer transaction/log decoding | Explorer | TODO |
| 8 | Explorer MUD record decoding | Explorer | TODO |
| 9 | Indexer processing metrics/observability | Indexer | TODO |
| 10 | Explorer real-time updates | Explorer | TODO |
| 11 | Explorer HTTP endpoint tests | Tests | TODO |
| 12 | Bundler paymaster integration | Bundler | TODO |
| 13 | Long-running stability test | Tests | TODO |
| 14 | Indexer restart/resume tests | Tests | TODO |
| 15 | CSS/styling polish for Explorer | Explorer | TODO |
| 16 | **MUD E2E: Deploy → Index → Normalise → Query → Browse** | MUD/Explorer | TODO — see `docs/MUD_E2E_PLAN.md` |

---

## Phase 2: Aspire AppChain Template (FUTURE)

### Architecture
```
AppHost (Orchestrator)
├── All Phase 1 services
├── AppChain Sequencer (block production, ordering)
├── AppChain P2P Node(s) (gossip, sync)
├── AppChain Sync Service (state sync between nodes)
├── AppChain Messaging (cross-chain messaging)
├── AppChain Anchoring (L1 anchoring to DevChain/mainnet)
├── AppChain Policy (gas policy, access control)
├── Clique Consensus (multi-validator)
└── Extended IntegrationTests
```

### Backlog (High-Level)
- [ ] Multi-node AppChain cluster in Aspire
- [ ] Sequencer with configurable block production
- [ ] P2P node discovery and gossip protocol
- [ ] State sync between nodes (catch-up, snapshot)
- [ ] Cross-chain messaging (send/receive/relay)
- [ ] L1 anchoring (periodic state root submission)
- [ ] Clique consensus with multiple validators
- [ ] Gas policy service (free gas, sponsored gas, session keys)
- [ ] Full integration tests for multi-node scenarios
- [ ] Load testing across multiple nodes
- [ ] Reorg/fork resolution in multi-node setup
- [ ] Monitoring dashboard for all nodes
- [ ] **Testnet anchoring integration** — E2E testing with real networks: (1) Sepolia — blob DA strategy (BlobRef_SnarkOnChain), validate blob submission + KZG commitments via beacon API. (2) Arbitrum/Optimism testnets — calldata DA strategies, validate L2 cost savings vs L1. (3) Cross-L2 anchoring — anchor an AppChain on an L2 instead of L1. Requires: testnet faucet automation, real gas cost tracking, blob lifecycle testing (pruning window), multi-chain Aspire profiles.
- [ ] **Network-specific Aspire templates** — Publishable templates like the DevChain template but pre-configured for real networks: `Nethereum.AppChain.Sepolia.Template` (blob DA, beacon API, Sepolia RPC), `Nethereum.AppChain.Arbitrum.Template` (calldata DA, Arbitrum RPC, L2 gas oracle), `Nethereum.AppChain.Optimism.Template` (calldata DA, OP Stack RPC). Each template pre-selects the optimal anchoring strategy for that network, configures gas estimation, and includes the right explorer/indexer wiring. Users `dotnet new nethereum-appchain-sepolia` and get a working AppChain anchored to Sepolia out of the box.
- [ ] **Proof challenge & request UI** — Explorer admin page to challenge anchored blocks and request proofs from the proving pipeline. Submit challenges via AppChainAnchor contract, trigger proof generation from block-prover, display proof status (pending/proving/verified/failed), and verify on-chain. Requires: proof request REST API on block-prover service, challenge submission via contract interaction, proof status tracking in indexer.

---

## Phase 3: NetDapps Backend Template (FUTURE)

### Vision
Use Phase 2 AppChain Aspire solution as the production backend template for the NetDapps application.

### Backlog (High-Level)
- [ ] Template project generator from Aspire solution
- [ ] Configurable chain parameters (chain ID, gas limits, block time)
- [ ] Production-ready deployment configs (Docker, K8s, Azure)
- [ ] API gateway for DApp frontend integration
- [ ] Wallet integration endpoints
- [ ] DApp catalog service integration
- [ ] User account management
- [ ] Production monitoring and alerting

---

## Issues Log

| # | Issue | Status | Resolution |
|---|-------|--------|------------|
| A-1 | Indexer: stale DB schema detection and reset on startup | OPEN | Indexer should detect incompatible DB schema on startup (like a chain reorg) and reset gracefully instead of crashing. Currently requires manual `docker volume rm`. Indexer's `MigrateAsync` path should verify schema compatibility and drop/recreate if mismatched. |
| A-2 | MUD BackgroundServices crash host on transient RPC errors | FIXED | Wrapped `MudPostgresProcessingHostedService`, `MudPostgresNormaliserBackgroundService`, `MudWorldAddressDiscoveryService` with `RetryRunner.RunWithExponentialBackoffAsync` |

---

## Change Log

| Date | Change |
|------|--------|
| 2026-02-17 | Initial plan reconstruction. Phase 1 ~60% complete. Fixed build issues (Mud.Repositories multi-target, Npgsql version, Newtonsoft.Json). |
| 2026-02-17 | **All services running successfully.** Fixed: Indexer DB schema creation (EnsureCreatedAsync), MUD processing conditional, DevChain JSON-RPC HTTP 200 for errors, EthFeeHistoryHandler rewrite (uses FeeHistoryResult), LoadGenerator EIP-1559 transactions, gas estimation for contract deployments. DevChain producing 11k+ blocks with mixed load. |
| 2026-02-23 | **Token processing pipeline integrated into Aspire.** Added: TokenTransferLogProcessingHostedService, TokenBalanceAggregationHostedService, and all DI registrations. Fixed: raw DDL uses quoted PascalCase table names for blockchain tables (`.ToTable()` names not lowercased by EFCore.NamingConventions), correct columns matching current entity models (Block.LogsBloom/StateRoot/ReceiptsRoot/WithdrawalsRoot, TransactionBase.TransactionType, TransactionLog.BlockNumber/BlockHash/IsCanonical). All 3 hosted services (blockchain processor, token log processor, balance aggregator) running with zero errors. |
