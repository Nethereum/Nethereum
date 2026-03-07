# Nethereum 6.0.0

Nethereum 6.0.0 is a major release introducing the CoreChain (full in-process Ethereum node), DevChain (development chain with SQLite persistence), AppChain (multi-node application chain stack), Account Abstraction (ERC-4337 bundler + ERC-7579 modular accounts), a Blazor Server blockchain Explorer, multi-provider blockchain storage (Postgres, SQL Server, SQLite), .NET Aspire orchestration, and significant EVM simulator improvements bringing it closer to full Ethereum specification compliance.

[Full Changelog](https://github.com/Nethereum/Nethereum/compare/5.8.0...6.0.0)

## Nethereum.CoreChain — In-Process Ethereum Node

New project providing a full in-process Ethereum execution layer node with JSON-RPC support, state management, block production, and transaction processing.

* Full JSON-RPC handler suite: `eth_call`, `eth_estimateGas`, `eth_getBalance`, `eth_getCode`, `eth_getStorageAt`, `eth_sendRawTransaction`, `eth_getTransactionByHash`, `eth_getTransactionReceipt`, `eth_getBlockByHash`, `eth_getBlockByNumber`, `eth_getBlockReceipts`, `eth_getLogs`, `eth_newFilter`, `eth_getFilterChanges`, `eth_getFilterLogs`, `eth_feeHistory`, `eth_gasPrice`, `eth_createAccessList`, `eth_coinbase`, `eth_mining`, `eth_syncing`, `net_listening`, `net_peerCount`, `web3_sha3`
* `debug_traceTransaction` and `debug_traceCall` with opcode and call tracers
* Historical state support with state diff tracking and pruning
* In-memory and persistent storage backends (via `IBlockStore`, `ITransactionStore`, `IReceiptStore`, `ILogStore`, `IStateStore`, `IStateDiffStore`, `IFilterStore`)
* Block producer with configurable block production options
* Transaction pool (`TxPool`) with gas price ordering
* Metrics instrumentation via `System.Diagnostics.Metrics`
* P2P interfaces for consensus and synchronisation
* WebSocket subscription support (`eth_subscribe`/`eth_unsubscribe`)
* State root calculation with Patricia Merkle Trie integration
* Consensus abstraction interfaces (pluggable PoA, Clique, etc.)

Commits: https://github.com/Nethereum/Nethereum/commit/652b4b77341d7b9baa35be1cb660cf53eaf64fe4, https://github.com/Nethereum/Nethereum/commit/f6c07772038c70f1c22bff25814c9b1ed4635011, https://github.com/Nethereum/Nethereum/commit/328672f9157503e3059a07b1022e9d8ffdb8f79b, https://github.com/Nethereum/Nethereum/commit/b040698658747aaf814836dc11daf2c774800253

## Nethereum.CoreChain.RocksDB — Persistent Storage

RocksDB-backed persistent storage for CoreChain including block, transaction, receipt, log, and state stores. Includes state diff store for historical state reconstruction, message result caching, and bloom filter-based log querying.

Commits: https://github.com/Nethereum/Nethereum/commit/2dcceade71399e0f06ee9830ec32470c5f066459, https://github.com/Nethereum/Nethereum/commit/2371cba5ca4f477775ec571084338c15040e1092

## Nethereum.DevChain — Development Chain

Full-featured Ethereum development chain built on CoreChain, designed for local development and testing.

* SQLite persistent storage (default) — chain state survives restarts; in-memory mode also available
* Pre-funded dev accounts (10,000 ETH each)
* Auto-mine mode (block per transaction)
* EIP-1559 transaction support
* Thread-safe account impersonation
* `evm_increaseTime` and `evm_setNextBlockTimestamp` dev RPC methods
* Hosted service pattern for ASP.NET Core integration

Commits: https://github.com/Nethereum/Nethereum/commit/652b4b77341d7b9baa35be1cb660cf53eaf64fe4, https://github.com/Nethereum/Nethereum/commit/3bc09399349eb8f73ed132c52ec699761eb9fbc2, https://github.com/Nethereum/Nethereum/commit/90bce97bdaf18163f4d8cbbd950cf128e6406e02

## Nethereum.DevChain.Server — HTTP Server

ASP.NET Core HTTP server wrapper for DevChain with CORS, health checks, configurable chain ID, and dev account management. Provides the JSON-RPC POST endpoint compatible with MetaMask, Foundry, Hardhat, and any Ethereum tooling.

Commits: https://github.com/Nethereum/Nethereum/commit/90bce97bdaf18163f4d8cbbd950cf128e6406e02

## Nethereum.AppChain — Application Chain Stack (Preview)

Nethereum.AppChain extends the CoreChain and DevChain to provide an application-specific chain layer. The idea is that applications can run their own chain as an extension of Ethereum, handling domain-specific data and business rules at this layer while users retain the ability to exit with their data at any time. Financial assets and high-value state remain on L1s and L2s where they benefit from full Ethereum security, while application data — game state, social graphs, content, governance — lives on the AppChain where it can be processed cheaply and with custom rules. This separation lets developers build fully decentralised applications without forcing all data onto expensive shared infrastructure.

**This project is currently in Preview.**

* Clique PoA consensus integration
* P2P networking with DotNetty transport and security fixes
* Sequencer for transaction ordering
* L1 anchoring with Postgres persistence for data availability and exit proofs
* Policy engine for chain governance and custom transaction validation rules
* Sync protocol for multi-node state synchronisation
* Account Abstraction integration for AA-native chains (gasless UX, session keys)
* Key vault integration via `IWeb3` constructor overloads
* Template support for scaffolding new AppChain projects

Commits: https://github.com/Nethereum/Nethereum/commit/b1c5806d2291efbc9adfbc99cd93c8cbe5e56fde, https://github.com/Nethereum/Nethereum/commit/0677068137ba2ebe2db549556047d411e3ec7398

## Nethereum.AccountAbstraction — ERC-4337 + ERC-7579

Major upgrade to the Account Abstraction stack with full ERC-4337 bundler implementation and ERC-7579 modular smart account support.

* **Bundler**: Full ERC-4337 bundler with user operation validation, mempool management, gas estimation, BLS aggregator support, and reputation tracking
* **Bundler RPC Server**: Standalone JSON-RPC server for the bundler (`eth_sendUserOperation`, `eth_estimateUserOperationGas`, `eth_getUserOperationByHash`, `eth_getUserOperationReceipt`, `eth_supportedEntryPoints`)
* **Bundler RocksDB Storage**: Persistent mempool and reputation storage using RocksDB
* **Gas Estimation**: Improved gas estimation for user operations including verification gas, call gas, and pre-verification gas
* **Validation Helper**: `ValidationDataHelper` for parsing validation data timestamps and aggregator addresses
* **ERC-7579 Modular Accounts**: `NethereumAccount` smart account with modular architecture — validators, executors, hooks, and fallback handlers
* **Smart Contract Factory**: `NethereumAccountFactory` with governance controls
* **Modules**: ECDSAValidator, Rhinestone modules (OwnableValidator, SocialRecovery, DeadmanSwitch, MultiFactor, HookMultiPlexer, OwnableExecutor, RegistryHook), SmartSessions with policies (SudoPolicy, ERC20SpendingLimitPolicy, UniActionPolicy)
* **Paymaster Contracts**: VerifyingPaymaster, DepositPaymaster, TokenPaymaster, BasePaymaster
* **Contract Handlers**: `IContractHandlerService` enabling standard contract services to be switched to AA mode
* **Batch Call Refactoring**: Improved batch operation support for user operations

Commits: https://github.com/Nethereum/Nethereum/commit/ce0e537cd2e3dea7f53916240608098e3f8d2f40, https://github.com/Nethereum/Nethereum/commit/9aaafa21c594a309b2bf750e9da96caf4e19f0bd, https://github.com/Nethereum/Nethereum/commit/b844e03a59df54cced1f747552430f1e20cc358a, https://github.com/Nethereum/Nethereum/commit/8ee6c33c5b10a1d49c495c750b321ceb37a38258, https://github.com/Nethereum/Nethereum/commit/e692fc1c26069942274930005d413139b0114ea1, https://github.com/Nethereum/Nethereum/commit/bcccaf5e7e433025ec6bedff1d7b13cb1b851761, https://github.com/Nethereum/Nethereum/commit/b356d6b6900f176d11d9ef98858161e5386deafa, https://github.com/Nethereum/Nethereum/commit/bcae91c8e404e5489f881561ea76d25ba4305dee, https://github.com/Nethereum/Nethereum/commit/934d48fcdb066b9a5ffae23ffc30007905586ed4, https://github.com/Nethereum/Nethereum/commit/81f7870792c5b0604838f6cb112ccc3cf0dcf6f8, https://github.com/Nethereum/Nethereum/commit/53c76a246d8ee7796aaa308063e80dbc3f0ec573, https://github.com/Nethereum/Nethereum/commit/b8087e8659a4b3bf5b4e603ef89ca4ee809663fa

## Nethereum.AccountAbstraction Smart Contracts (Solidity)

New Solidity smart contracts for the Account Abstraction system, compiled with Foundry (Solc 0.8.28, Cancun EVM).

* `NethereumAccount.sol` — ERC-7579 modular smart account with sentinel-list module management
* `NethereumAccountFactory.sol` — CREATE2 factory with governance controls
* `BasePaymaster.sol`, `VerifyingPaymaster.sol`, `DepositPaymaster.sol`, `TokenPaymaster.sol` — Paymaster contracts
* `ECDSAValidator.sol` — ECDSA signature validation module
* Rhinestone module ports: `OwnableValidator`, `SocialRecovery`, `DeadmanSwitch`, `MultiFactor`, `HookMultiPlexer`, `OwnableExecutor`, `RegistryHook`
* SmartSessions: `SmartSession.sol` with `SudoPolicy`, `ERC20SpendingLimitPolicy`, `UniActionPolicy`

Commits: https://github.com/Nethereum/Nethereum/commit/b356d6b6900f176d11d9ef98858161e5386deafa

## Nethereum.Explorer — Blazor Server Blockchain Explorer

New Blazor Server component library providing a full blockchain explorer UI.

* Block list and detail pages
* Transaction list, detail, and input data decoding with ABI resolution
* Log list with event decoding
* Account page (balance, transactions, code)
* Contract interaction: read/write functions via EIP-6963 wallet integration
* Token pages: ERC-20/721/1155 transfers, balances, and metadata
* MUD table browser (World addresses, table IDs, records)
* ABI resolution (Sourcify, local storage)
* Security: CSV injection protection, SQL injection prevention in MUD queries, query bounds validation

Commits: https://github.com/Nethereum/Nethereum/commit/4468c1b977b554bc57b921837f0288f7b870bfa6, https://github.com/Nethereum/Nethereum/commit/f1e8b001cee6366044ec47b684be9f0e2e488a32

## Nethereum.Blazor — Dynamic Contract Interaction Components

* New dynamic contract interaction components: `DynamicQueryFunction`, `DynamicTransactionFunction`, `DynamicContractDeployment` — interact with any smart contract using just its ABI, no code generation required
* `DynamicStructInput`, `DynamicArrayInput` — recursive input rendering for complex ABI types
* `DynamicResultOutput`, `DynamicErrorDisplay`, `DynamicReceiptDisplay`, `DynamicGasSettings`
* Typed base classes: `QueryFunctionComponentBase`, `TransactionFunctionComponentBase`, `ContractDeploymentComponentBase`, `ResultOutputBase`
* EIP-6963 wallet discovery improvements

Commits: https://github.com/Nethereum/Nethereum/commit/15a0dd5350ab925cb4835dd84a010d05c359c71d

## Nethereum.Blazor.Solidity — EVM Debugger Components

New Blazor components for EVM-level debugging and Solidity source code viewing.

Commits: https://github.com/Nethereum/Nethereum/commit/d28b638560d6572eb3c87f1f0fc5f3262c427b96

## Nethereum.EVM — Major EVM Simulator Upgrade

Significant improvements to the EVM simulator bringing it closer to full Ethereum specification compliance (Prague/Cancun).

* **Prague support**: EIP-7623 gas changes, new precompiles (BLS12-381, KZG Point Evaluation), BN128 curve completed port
* **Precompiles**: Native implementations for ecRecover, MODEXP, BN128 (add, mul, pairing), BLAKE2f, BLS12-381, KZG Point Evaluation
* **Call frame tracking**: Proper call stack with `InnerCallResult` tracking for internal calls, delegate calls, and static calls
* **Access list tracker**: EIP-2929 warm/cold access tracking for accurate gas calculation
* **Gas calculation fixes**: EXTCODECOPY stack order, EXP gas calculation, maxinitcode 32000 gas, static call enforcement, revert handling
* **Debugger**: Async debugger sessions, function dispatch mapping (`_functionMaps`), safe hex/chainId parsing
* **State changes extractor**: Extract token balance changes from EVM traces for transaction simulation
* **Performance**: Cached int types, `BinaryPrimitives` for direct integer decoding

Commits: https://github.com/Nethereum/Nethereum/commit/17972bdcf0a4d1769505ba943b1786a27ded320d, https://github.com/Nethereum/Nethereum/commit/04d442bc4f892b180058ee5e05971fece59178c7, https://github.com/Nethereum/Nethereum/commit/f3f5be3c93e1d3c0de56e733cbf3a83d9f6b7576, https://github.com/Nethereum/Nethereum/commit/384ca19f9d3691d54f2c64f7bddd28f76f527168, https://github.com/Nethereum/Nethereum/commit/43f682a9c10ab0c89cdef47234873cf09e23cd1b, https://github.com/Nethereum/Nethereum/commit/1ad60603d125550bb533c95d60f39c31896a4cf4, https://github.com/Nethereum/Nethereum/commit/291ab5a6941729a597dadfc39cdd4eaf52bad182, https://github.com/Nethereum/Nethereum/commit/77a315a01d16bece8279700bbf3dc65be2a355f0, https://github.com/Nethereum/Nethereum/commit/879125b9709303e02daf610f211cf89c9f14100a, https://github.com/Nethereum/Nethereum/commit/20dc6749f98b62c92ac8768020240c08a37279ef, https://github.com/Nethereum/Nethereum/commit/214b1e3f09a282c0b2b98156bd295f89406f918d, https://github.com/Nethereum/Nethereum/commit/54302866c1af6f90eb79d35cc92f350988d6fef7, https://github.com/Nethereum/Nethereum/commit/b2e2206b4207f2b007af125f5298321ccb86e51f, https://github.com/Nethereum/Nethereum/commit/8d43b6b0b4c3e00f36ccafa916a134110766da23, https://github.com/Nethereum/Nethereum/commit/81418a5b21c9566f1e27ac0373ef539f5d13738b, https://github.com/Nethereum/Nethereum/commit/2f853e5bd2eec3fef91dbb325b1b63266a6fcb4d, https://github.com/Nethereum/Nethereum/commit/5a7f7438c1de90d420066498d9dd8178b9f21798, https://github.com/Nethereum/Nethereum/commit/6e827045a851d8cf38fcc738c4785b3c0adeabd4

## BlockchainProcessing — Reorg Detection, Token Indexing, Internal Transactions

Major upgrade to the blockchain processing pipeline.

* **Reorg detection**: `ReorgDetectedException` with automatic block rewind and reprocessing
* **Token transfer indexing**: ERC-20/721/1155 `Transfer` event processors with dedicated repositories (`ITokenTransferLogRepository`, `ITokenBalanceRepository`, `ITokenMetadataRepository`, `INFTInventoryRepository`)
* **Token balance aggregation**: Computes running balances from transfer events
* **Internal transaction support**: `IInternalTransactionRepository` with storage step handler for tracing-derived internal calls
* **Metrics**: Processing rate, block lag, and error counters via `System.Diagnostics.Metrics`
* **String-to-long migration**: Block numbers and transaction indices changed from `string` to `long` for proper sorting and querying in the storage entities
* **Log storage options**: Configurable log storage with bloom filter-based filtering
* **Retry runner**: Configurable retry logic for transient failures

Commits: https://github.com/Nethereum/Nethereum/commit/68f541f63dc32502ca25ed2c83b1c4e484c5b623

## BlockchainStore — Multi-Provider Database Storage

Complete rewrite of the blockchain storage layer with a shared EF Core base and provider-specific implementations.

### BlockchainStore.EFCore (Shared Base)
* `IBlockchainDbContextFactory` factory pattern for scoped DbContext creation
* Full entity model: blocks, transactions, transaction logs, contracts, token transfers, token balances, token metadata, NFT inventory, internal transactions
* Block progress tracking for resumable indexing

### BlockchainStore.Postgres
* PostgreSQL provider using Npgsql with snake_case naming conventions
* Optimised for high-throughput indexing with batch operations

### BlockchainStore.SqlServer
* SQL Server provider with configurable schema support
* Full indexed blockchain storage

### BlockchainStore.Sqlite
* SQLite provider for lightweight/embedded scenarios
* Full indexed blockchain storage

### BlockchainStorage.Processors (Shared Base)
* `BlockchainProcessingService` — orchestrates block/transaction/log processing into the database
* `InternalTransactionProcessingService` — processes debug traces into internal transaction records
* `BlockchainProcessingHostedService` / `InternalTransactionProcessingHostedService` — ASP.NET Core hosted service wrappers
* `BlockchainProcessingOptions` — configuration binding (connection strings, batch size, confirmations)

### BlockchainStorage.Processors.Postgres / SqlServer / Sqlite
* Thin DI wiring projects: `AddPostgresBlockchainProcessor()`, `AddSqlServerBlockchainProcessor()`, `AddSqliteBlockchainProcessor()`

### BlockchainStorage.Token.Postgres
* Token processing pipeline: ERC-20/721/1155 transfer log processor, balance aggregator, metadata resolver
* Token balance denormaliser for fast query access
* Safe tokenId parsing for large ERC-721/1155 IDs

Commits: https://github.com/Nethereum/Nethereum/commit/4f74d19706378d89d0992ef5b02f006bb2b476f0, https://github.com/Nethereum/Nethereum/commit/7515761a813f25d945e0c58d9d6f4b731ab94744, https://github.com/Nethereum/Nethereum/commit/286ebbca55b97a5e60af70d6266cf8a2b2e916c6, https://github.com/Nethereum/Nethereum/commit/2417b18c7657ca4f5acc54c6e6a52e13de291c80, https://github.com/Nethereum/Nethereum/commit/d075e36f07c0f92f231f3478fd4c93df1385e05b, https://github.com/Nethereum/Nethereum/commit/db97ee04c700cb4de81aa0e14b0f6a6d617e4c77, https://github.com/Nethereum/Nethereum/commit/f496d9e3d418ef653338ebbc2a7899266a52a1e3, https://github.com/Nethereum/Nethereum/commit/7769e13c7af1cf70139ceba9352441f1acb3a533

## MUD — Processing Services and Reorg Support

* Hosted processing services for MUD World record indexing
* Reorg support: detect and rewind MUD records on chain reorganisation
* Normaliser: denormalise MUD table records into queryable Postgres tables
* `DynamicTablePredicateBuilder` for type-safe MUD table queries
* `INormalisedTableQueryService` for querying normalised MUD data
* Blazor component refactoring

Commits: https://github.com/Nethereum/Nethereum/commit/741eb1372e410a3002ff546c58f55b801fb0ab4d

## Nethereum.ABI — Int128/UInt128 Support and StateMutability

* **Int128/UInt128 decoding and encoding**: Full support for 128-bit integer types in ABI encoding and decoding
* **Direct integer decoding**: Use `BinaryPrimitives` for primitive integer types instead of decoding via `BigInteger` — significant performance improvement
* **StateMutability support**: `FunctionABI` and `ConstructorABI` now include `StateMutability` field from ABI JSON
* **String decoder boundary fix**: Prevent out-of-range exceptions on malformed string data
* **Null-safe JSON parameter conversion**: `JsonParameterObjectConvertor` handles null values gracefully
* **ABIJsonDeserialiserSTJ**: System.Text.Json ABI deserialiser improvements

Commits: https://github.com/Nethereum/Nethereum/commit/c940936efec5160eb3d5f511e1e9af924a4189c2, https://github.com/Nethereum/Nethereum/commit/a425b5d7dc3aeea3cb5cad49dc34343ac2b5a83a, https://github.com/Nethereum/Nethereum/commit/6e476ac7a8b909b61b067ebbdaf2117c2ab02711, https://github.com/Nethereum/Nethereum/commit/b6efed56047a031396cab6d68e5963d58ec21b74, https://github.com/Nethereum/Nethereum/commit/6918a5f3d3cb38e0bc6e833a891867cc88f57ee3

## Nethereum.Model — EIP-4844/Prague Block Headers and Transaction7702

* **EIP-4844 block header fields**: `BlobGasUsed`, `ExcessBlobGas`, `ParentBeaconBlockRoot` added to `BlockHeader`
* **Prague fields**: `RequestsHash` for the Prague hardfork
* **Transaction7702**: Full EIP-7702 transaction type support in `TransactionFactory`
* **Original RLP caching**: Cache the original RLP bytes for hash verification without re-encoding
* **Defensive fixes**: Clone RLP cache on copy, deduplicate encode paths, reorder type checks for correctness
* **Log encoding**: Receipt and `LogBloomFilter` RLP encoding/decoding updates
* **SignedTransaction extensions**: Improved signed transaction handling

Commits: https://github.com/Nethereum/Nethereum/commit/96093c58a136efd2e42538a5e46af22b3f9b18c4, https://github.com/Nethereum/Nethereum/commit/69f0ed9699c970db087ddb84040962b4e7d1d5da, https://github.com/Nethereum/Nethereum/commit/961cd78abf9439426c1aeba12620f56444486ed8, https://github.com/Nethereum/Nethereum/commit/94651016

## Nethereum.RPC — Shared Debug Tracing and EIP-4844

* **Debug tracing moved to shared RPC**: `DebugTraceTransaction`, `DebugTraceCall`, and all tracer types (CallTracer, OpcodeTracer, PrestateTracer, NoopTracer) moved from `Nethereum.Geth` to `Nethereum.RPC.DebugNode` so any node implementation can use them
* **Tracer DTOs**: `TraceConfigDto`, `TraceCallConfigDto`, `TracerConfigDto`, `BlockOverridesDto`, `StateOverrideDto`, `TracerLogDto`
* **EIP-4844 blob fields**: `BlobVersionedHashes`, `MaxFeePerBlobGas` added to transaction DTOs
* **RpcMessage serialisation**: System.Text.Json server-side serialisation support

Commits: https://github.com/Nethereum/Nethereum/commit/838319162c5b477070fae30fa63bf2ee61da62d4, https://github.com/Nethereum/Nethereum/commit/69d36000774c86f2b60ee2bafc8856ba6b0b3469, https://github.com/Nethereum/Nethereum/commit/b3c3015d805cb5510d800fc5ec4d08d8e63f5d51

## Nethereum.Geth — Tracer Refactoring

* Geth-specific tracing types (`DebugTraceTransaction`, `DebugTraceCall`, `CallTracer`, `OpcodeTracer`, `PrestateTracer`, `NoopTracer`, and all DTOs) moved to shared `Nethereum.RPC.DebugNode` namespace
* Remaining Geth-specific tracers updated: `BigramTracer`, `TrigramTracer`, `UnigramTracer`, `EvmdisTracer`, `FourByteTracer`, `OpcountTracer`, `CustomTracer`

Commits: https://github.com/Nethereum/Nethereum/commit/69d36000774c86f2b60ee2bafc8856ba6b0b3469

## Nethereum.Merkle — FrontierMerkleTree, Poseidon, and Fixes

* **FrontierMerkleTree**: New append-only Merkle tree optimised for blockchain use (stores only the frontier nodes)
* **LeanIncrementalMerkleTree**: Optimised with cached layers and dirty-node persistence
* **Poseidon hash provider**: ZK-friendly hash function for sparse Merkle trees
* **Patricia Merkle Trie**: Dirty node tracking, nibble extension fixes
* **Fixes**: `VerifyProof` direction correction, empty root handling, capacity overflow prevention

Commits: https://github.com/Nethereum/Nethereum/commit/530f723688762a7d8e658ff7eebf09ff1e1e1012, https://github.com/Nethereum/Nethereum/commit/9e06cc23091954e160fd55896e15caad745ff77d, https://github.com/Nethereum/Nethereum/commit/adeea4569502cf9af321580ebd544281851c3803, https://github.com/Nethereum/Nethereum/commit/0d3fd5524d30918934740a1c94bcea6280c98ed5, https://github.com/Nethereum/Nethereum/commit/6848840a5bd1f080b751ffaf5576eec054b9b5f5, https://github.com/Nethereum/Nethereum/commit/e5ee4d6eb39c546721e6b77b7b915a6ba9be379a, https://github.com/Nethereum/Nethereum/commit/2d3bae3cd06722adaaab06f4d596890d04884a92

## Nethereum.Wallet — StateChanges Preview and Transaction Executor

* **StateChangesPreviewService**: Simulate transactions via EVM and display predicted state changes (ETH balance changes, token transfers) before signing
* **TransactionExecutor**: Unified transaction execution with `CallMode` option for EVM simulation vs live execution

Commits: https://github.com/Nethereum/Nethereum/commit/20a1140e89e83a0c2882c1e7ccb48a7b69e5f9e8, https://github.com/Nethereum/Nethereum/commit/81418a5b21c9566f1e27ac0373ef539f5d13738b, https://github.com/Nethereum/Nethereum/commit/6f2f8032a2510a6bf196ae678a63190b420be2ef

## Nethereum.ABI DataServices — Sourcify V2

* Sourcify upgrade to V2 APIs
* Parquet download support for bulk ABI data
* Postgres EF database for ABI storage
* `ABIInfoStorageFactory` with composite pattern: cache, Sourcify, Etherscan, 4Byte

Commits: https://github.com/Nethereum/Nethereum/commit/312c258b7a2a73209834767d02722d4aabd13530

## Code Generator

* Support for `referencedTypesNamespaces` — when a struct type is shared across contracts (e.g. `PackedUserOperation`), the code generator skips regenerating it and adds the namespace import instead
* JavaScript transpile update

Commits: https://github.com/Nethereum/Nethereum/commit/3afba9ddbcafbcc78c7a6468ee6bc8e9d97bc70e, https://github.com/Nethereum/Nethereum/commit/3132ac1a6fabb32b2471012f244f43580525b7f2, https://github.com/Nethereum/Nethereum/commit/f27dc10995802b3d9da06401c030f3f11f5be95f

## .NET Aspire Integration

New Aspire orchestration for spinning up a complete Ethereum development environment with a single `dotnet run`.

* **AppHost**: Orchestrates DevChain + PostgreSQL + Indexer + Explorer with service discovery, health checks, and OpenTelemetry
* **Indexer**: Background worker crawling blocks, transactions, logs, token transfers (ERC-20/721/1155), token balances, and MUD World records into PostgreSQL
* **Explorer**: Full blockchain explorer UI connected via Aspire service discovery
* **`dotnet new nethereum-devchain` template**: Generates a standalone Aspire solution using only NuGet packages with configurable `--NethereumVersion`, `--ChainId`, and `--AspireVersion` parameters
* **ServiceDefaults**: Shared OpenTelemetry, health checks, and resilient HTTP client configuration

Commits: https://github.com/Nethereum/Nethereum/commit/0b6e91ef5939b76fe4837b236af2cd10518c941f

## .NET 10 Target Framework Support

* `Nethereum.Signer` and `Nethereum.KeyStore`: Added `net10.0` to BouncyCastle conditional framework lists
* `Nethereum.Maui.AndroidUsb`: Updated target framework from `net9.0` to `net10.0`
* Wallet UI components: Upgraded target frameworks and packages to .NET 10
* `Microsoft.Extensions.Logging.Abstractions`: Version constraint relaxed to support any version above minimum

Commits: https://github.com/Nethereum/Nethereum/commit/e9459bc93baeb1cbcf21deac59438d7b5a0de279, https://github.com/Nethereum/Nethereum/commit/9ab1acab3a3ce4bbf353e469f251fc3abb773023, https://github.com/Nethereum/Nethereum/commit/ee468461b45441087faa6ac1537c1d7d4eb5e197, https://github.com/Nethereum/Nethereum/commit/741a4012fe02656bb2f56f8e9c4cd5db854c153b

## EIP-7702 Support

Full EIP-7702 (Set EOA Account Code) support across the stack:

* `Transaction7702` model and encoder
* `TransactionFactory` update for 7702 transaction creation
* Signer support for 7702 authorisation lists
* CoreChain integration tests and spec tests
* Bundler validation support for delegated accounts

Commits: https://github.com/Nethereum/Nethereum/commit/48a9884c596a5840f8303aa6db358d6c13ce714e, https://github.com/Nethereum/Nethereum/commit/1aaae6f28a36d4283e9a515a8f24f782718c6590, https://github.com/Nethereum/Nethereum/commit/b844e03a59df54cced1f747552430f1e20cc358a

## Test Coverage

* **EVM specification tests**: stCallCodes, stCreate2, stRefundTest, stBadOpcode, stExtCodeHash, stStaticCall, precompiles test vectors — validated against Ethereum test suite
* **RLP tests**: Using Geth test vectors for encoding/decoding validation
* **Signer tests**: EIP-2930 transaction signing with Geth test vectors
* **Account Abstraction**: Integration tests for ERC-4337 bundler, ERC-7579 modules, gas estimation, and end-to-end user operation flows
* **CoreChain**: Integration tests for RPC handlers, access list creation, debug tracing, blockchain test vectors
* **EVM benchmarks**: Performance benchmarking project

Commits: https://github.com/Nethereum/Nethereum/commit/5a67625e4dfdb1f97568a5cdc1bb1b969ddb743a, https://github.com/Nethereum/Nethereum/commit/779349bd8e9edb36c4664e896c496ad90a4752fa, https://github.com/Nethereum/Nethereum/commit/35920bd65e6fad91bdab927ad0114563d4c797f2, https://github.com/Nethereum/Nethereum/commit/9b998b1e9566bca7aa1c356dfb7fbd0462c63266, https://github.com/Nethereum/Nethereum/commit/e818b7922d2b9d79610562a3014891bc12324ff0, https://github.com/Nethereum/Nethereum/commit/6e827045a851d8cf38fcc738c4785b3c0adeabd4

## Other Changes

* **Hex extensions**: Small optimisations for hex string operations
  Commit: https://github.com/Nethereum/Nethereum/commit/e9c5ddb96ca07cc357777c1ce28c7d1eae4e86bb
* **BigDecimal**: Support parsing e-notation (scientific notation)
  Commit: https://github.com/Nethereum/Nethereum/commit/0ee4b8d054d5449abb37bf2879395639c4341401
* **ENS**: Update ADRaffy.ENSNormalize
  Commit: https://github.com/Nethereum/Nethereum/commit/66e7a773257aa5c17d825330546711ec0778d7c1
* **Poseidon hasher**: `Nethereum.Util.PoseidonHasher` for ZK-friendly hashing
  Commit: https://github.com/Nethereum/Nethereum/commit/6848840a5bd1f080b751ffaf5576eec054b9b5f5
