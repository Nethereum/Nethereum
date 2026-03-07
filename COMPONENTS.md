# Nethereum Components

**Nethereum** is the comprehensive .NET integration platform for Ethereum and EVM-compatible blockchains. It provides a complete development stack: from low-level ABI encoding and transaction signing, through a full EVM simulator and in-process Ethereum node, to blockchain data indexing, an ERC-4337 account abstraction bundler, a Blazor blockchain explorer, MUD framework support, multi-platform wallet UIs (Blazor, Avalonia, MAUI, Unity), and .NET Aspire orchestration. Nethereum targets netstandard 2.0, .NET 6/8/9/10, .NET Framework 4.5.1+, and Unity, running on Windows, Linux, macOS, Android, iOS, WebAssembly, and game consoles.

## Quick Start by Use Case


| I want to... | Packages |
|---|---|
| **Basics** | |
| Send ETH and interact with contracts | `Nethereum.Web3` |
| Work with ERC-20, ERC-721, or ERC-1155 tokens | `Nethereum.Web3` (includes typed contract services for all major standards) |
| **Signing & Key Management** | |
| Sign transactions offline | `Nethereum.Web3` + `Nethereum.Accounts` |
| Use an HD wallet (BIP32/BIP39) | `Nethereum.HDWallet`  or `Nethereum.Wallet` (light hd wallet)|
| Sign with Trezor or Ledger | `Nethereum.Signer.Trezor` or `Nethereum.Signer.Ledger` |
| Sign with AWS KMS or Azure Key Vault | `Nethereum.Signer.AWSKeyManagement` or `Nethereum.Signer.AzureKeyVault` |
| Sign EIP-712 typed data | `Nethereum.Signer.EIP712` |
| **Local Development** | |
| Run a local dev chain (no external node) | `Nethereum.DevChain.Server` |
| Simulate EVM execution in-process | `Nethereum.EVM` |
| Preview transaction state changes before signing | `Nethereum.Wallet` (StateChangesPreviewService) + `Nethereum.EVM` |
| Spin up a full dev environment with Aspire | `dotnet new nethereum-devchain` template |
| **Code Generation** | |
| Generate C# contract services from Solidity ABI | `Nethereum.Generator.Console` (CLI) or VS Code Solidity extension |
| Generate UI components from contract definitions | `Nethereum.Generator.Console` (CLI) or VS Code Solidity extension|
| Generate MUD table services and queries | `Nethereum.Generator.Console` (CLI) or VS Code Solidity extension|
| **Data & Indexing** | |
| Index blockchain data to a database | `Nethereum.BlockchainProcessing` + a store provider (`Postgres` / `SqlServer` / `Sqlite`) |
| Index token transfers and compute balances | `Nethereum.BlockchainStorage.Token.Postgres` |
| Build a blockchain explorer | `Nethereum.Explorer` |
| Fetch ABI from Sourcify or Etherscan | `Nethereum.DataServices` |
| Query Ethereum data services and external APIs | `Nethereum.DataServices` |
| Get token prices, metadata, and logos | `Nethereum.TokenServices` (CoinGecko, token lists) |
| Discover and scan token balances across wallets | `Nethereum.TokenServices` (multicall batching, multi-account) |
| **DeFi & Protocols** | |
| Swap tokens on Uniswap (V2/V3/V4) | `Nethereum.Uniswap` |
| Use Permit2 for gasless token approvals | `Nethereum.Uniswap` (includes Permit2) |
| Accept crypto payments in my API (x402) | `Nethereum.X402` (ASP.NET Core middleware + facilitator) |
| Pay for x402-protected API endpoints | `Nethereum.X402` (client with EIP-3009 signed authorizations) |
| Resolve ENS names | `Nethereum.Contracts` (typed ENS services built-in) |
| Implement Sign-In with Ethereum | `Nethereum.Siwe` |
| Use Gnosis Safe multi-sig | `Nethereum.GnosisSafe` |
| Interact with Circles UBI protocol | `Nethereum.Circles` |
| **Account Abstraction** | |
| Use smart accounts (ERC-4337 UserOps) | `Nethereum.AccountAbstraction` |
| Build an ERC-4337 bundler | `Nethereum.AccountAbstraction.Bundler` |
| Run a bundler RPC server | `Nethereum.AccountAbstraction.Bundler.RpcServer` |
| Deploy ERC-7579 modular smart accounts | `Nethereum.AccountAbstraction` |
| **MUD (Autonomous Worlds)** | |
| Work with MUD World systems and tables | `Nethereum.Mud` + `Nethereum.Mud.Contracts` |
| Index and normalise MUD store records to Postgres | `Nethereum.Mud.Repositories.Postgres` |
| Query normalised MUD tables with predicates | `Nethereum.Mud` (DynamicTablePredicateBuilder) |
| Build MUD table UIs in Blazor | `Nethereum.MudBlazorComponents` |
| **Wallet & UI** | |
| Build a multi-platform wallet app | `Nethereum.Wallet` + `Nethereum.Wallet.UI.Components` + a renderer (`.Blazor` / `.Maui`) |
| Integrate browser wallets in Blazor | `Nethereum.Blazor` + `Nethereum.EIP6963WalletInterop` |
| Connect via WalletConnect / Reown | `Nethereum.WalletConnect` or `Nethereum.Reown.AppKit.Blazor` |
| Interact with any contract dynamically (no codegen) | `Nethereum.Blazor` (DynamicQueryFunction, DynamicTransactionFunction) |
| Build a Unity game with Ethereum | `Nethereum.Unity` |
| **Verification & Cryptography** | |
| Verify beacon chain state via light client | `Nethereum.Consensus.LightClient` + `Nethereum.Signer.Bls.Herumi` |
| Validate account balances and state against proofs | `Nethereum.Consensus.LightClient` + `Nethereum.Merkle.Patricia` |
| Calculate Merkle proofs and state roots | `Nethereum.Merkle` + `Nethereum.Merkle.Patricia` |
| **Infrastructure** | |
| Run a custom application chain | `Nethereum.AppChain` (Preview) |
| Use System.Text.Json / AOT-friendly RPC | `Nethereum.JsonRpc.SystemTextJsonRpcClient` |
| Stream real-time data via WebSocket subscriptions | `Nethereum.JsonRpc.WebSocketStreamingClient` |
| Use reactive extensions (Rx.NET) for RPC | `Nethereum.RPC.Reactive` |

---

## 1. Core Foundation

The foundation layer provides Ethereum primitives, ABI encoding, RPC communication, and the high-level Web3 entry point. Most users only need `Nethereum.Web3`, which pulls in all core dependencies.

| Package | Description |
|---|---|
| [Nethereum.Web3](src/Nethereum.Web3/) | High-level entry point aggregating RPC, contracts, accounts, and signing |
| [Nethereum.ABI](src/Nethereum.ABI/) | ABI encoding/decoding for functions, events, errors, and complex types (tuples, arrays, Int128/UInt128) |
| [Nethereum.Contracts](src/Nethereum.Contracts/) | Smart contract interaction: deployment, function calls, event filtering, multicall batching, and typed services for ERC-20, ERC-721, ERC-1155, ERC-165, ERC-1271, ERC-2535, ERC-6492, EIP-3009, and ENS |
| [Nethereum.Accounts](src/Nethereum.Accounts/) | Account types (Account, ManagedAccount, ExternalAccount), transaction managers, and nonce management |
| [Nethereum.Model](src/Nethereum.Model/) | Core domain models: block headers, transaction types (Legacy, EIP-1559, EIP-2930, EIP-4844, EIP-7702), receipts, RLP encoding |
| [Nethereum.Hex](src/Nethereum.Hex/) | Hex types (HexBigInteger, HexUTF8String) and conversion utilities |
| [Nethereum.RLP](src/Nethereum.RLP/) | Recursive Length Prefix encoding/decoding for Ethereum wire format |
| [Nethereum.Util](src/Nethereum.Util/) | Keccak-256 hashing, Wei/Gwei/Ether unit conversion, address checksumming, Poseidon hasher |
| [Nethereum.RPC](src/Nethereum.RPC/) | Typed wrappers for `eth_*`, `web3_*`, `net_*`, and `debug_*` RPC methods |
| [Nethereum.RPC.Reactive](src/Nethereum.RPC.Reactive/) | Rx.NET reactive wrappers for polling and streaming RPC data |

### JSON-RPC Transport

| Package | Description |
|---|---|
| [Nethereum.JsonRpc.Client](src/Nethereum.JsonRpc.Client/) | Base RPC client abstractions, interceptor pipeline, and request/response types |
| [Nethereum.JsonRpc.RpcClient](src/Nethereum.JsonRpc.RpcClient/) | HTTP JSON-RPC client (Newtonsoft.Json) |
| [Nethereum.JsonRpc.SystemTextJsonRpcClient](src/Nethereum.JsonRpc.SystemTextJsonRpcClient/) | HTTP JSON-RPC client (System.Text.Json, AOT-friendly) |
| [Nethereum.JsonRpc.IpcClient](src/Nethereum.JsonRpc.IpcClient/) | IPC client (Windows named pipes, Unix domain sockets) |
| [Nethereum.JsonRpc.WebSocketClient](src/Nethereum.JsonRpc.WebSocketClient/) | WebSocket JSON-RPC client |
| [Nethereum.JsonRpc.WebSocketStreamingClient](src/Nethereum.JsonRpc.WebSocketStreamingClient/) | Streaming WebSocket client for `eth_subscribe` / `eth_unsubscribe` |

---

## 2. Signing & Key Management

Signing libraries cover ECDSA transaction/message signing across all transaction types, EIP-712 typed data, BLS signatures, HD wallet derivation, hardware wallet integration, and cloud-based key management.

| Package | Description |
|---|---|
| [Nethereum.Signer](src/Nethereum.Signer/) | ECDSA signing and verification for Legacy, EIP-1559, EIP-2930, EIP-4844, and EIP-7702 transactions |
| [Nethereum.Signer.EIP712](src/Nethereum.Signer.EIP712/) | EIP-712 typed structured data signing and verification |
| [Nethereum.Signer.Bls](src/Nethereum.Signer.Bls/) | BLS signature abstractions |
| [Nethereum.Signer.Bls.Herumi](src/Nethereum.Signer.Bls.Herumi/) | BLS signature implementation using the Herumi native library |
| [Nethereum.KeyStore](src/Nethereum.KeyStore/) | Web3 Secret Storage (UTC/JSON keystore) encryption and decryption (Scrypt, PBKDF2) |
| [Nethereum.HDWallet](src/Nethereum.HDWallet/) | BIP32/BIP39/BIP44 hierarchical deterministic wallet derivation |
| [Nethereum.Signer.Ledger](src/Nethereum.Signer.Ledger/) | Ledger hardware wallet transaction signing |
| [Nethereum.Signer.Trezor](src/Nethereum.Signer.Trezor/) | Trezor hardware wallet transaction signing |
| [Nethereum.Signer.AWSKeyManagement](src/Nethereum.Signer.AWSKeyManagement/) | AWS KMS-based Ethereum transaction signing |
| [Nethereum.Signer.AzureKeyVault](src/Nethereum.Signer.AzureKeyVault/) | Azure Key Vault-based Ethereum transaction signing |

---

## 3. Smart Contracts & Standards

`Nethereum.Contracts` is the main package for smart contract interaction. It includes typed service classes for all major token and protocol standards. Additional standalone libraries provide deeper integration with specific protocols.

| Package | Description |
|---|---|
| [Nethereum.Contracts](src/Nethereum.Contracts/) | Core contract interaction: deployment, function calls, event filtering, multicall batching. **Includes typed services for:** ERC-20, ERC-721, ERC-1155, ERC-165, ERC-1271, ERC-2535 (Diamond Proxy), ERC-6492, EIP-3009 (transferWithAuthorization), and ENS |
| [Nethereum.ENS](src/Nethereum.ENS/) | Ethereum Name Service: name resolution, registration, management, and reverse lookup |
| [Nethereum.GnosisSafe](src/Nethereum.GnosisSafe/) | Safe (Gnosis Safe) multi-signature wallet interaction with Permit2 support |
| [Nethereum.Uniswap](src/Nethereum.Uniswap/) | Uniswap DEX contract interaction |
| [Nethereum.X402](src/Nethereum.X402/) | HTTP 402 Payment Required protocol for pay-per-request APIs with Ethereum payments |
| [Nethereum.Siwe](src/Nethereum.Siwe/) | Sign-In with Ethereum (EIP-4361): message building, signing, verification, and session management |
| [Nethereum.Circles](src/Nethereum.Circles/) | Circles UBI protocol integration |
| [Nethereum.GSN](src/Nethereum.GSN/) | Gas Station Network meta-transaction relay integration |

### Code Generation

Generate typed C# contract service classes, DTOs, and deployment messages from Solidity ABI/bytecode.

| Package | Description |
|---|---|
| Nethereum.Generator.Console | CLI tool: `dotnet tool install -g Nethereum.Generator.Console` |
| Nethereum.Generators | Core code generation engine (also available via VS Code Solidity extension) |
| Nethereum.Autogen.ContractApi | MSBuild task for automatic contract code generation on build |

---

## 4. EVM Simulator

A full in-process Ethereum Virtual Machine supporting all opcodes through Prague/Cancun, with native precompile implementations, call tracing, state change extraction, and step-by-step debugging.

| Package | Description |
|---|---|
| [Nethereum.EVM](src/Nethereum.EVM/) | Full EVM simulator: all opcodes (including PUSH0, MCOPY, TSTORE/TLOAD, BLOBHASH), call frame tracking, access list (EIP-2929), gas accounting, state change extraction, and async debugging sessions |
| [Nethereum.EVM.Contracts](src/Nethereum.EVM.Contracts/) | Precompiled contract implementations: ecRecover, SHA-256, RIPEMD-160, identity, modexp, alt_bn128, blake2f |
| Nethereum.EVM.Precompiles.Bls | BLS12-381 precompile (EIP-2537) |
| Nethereum.EVM.Precompiles.Kzg | KZG Point Evaluation precompile (EIP-4844) |

---

## 5. In-Process Ethereum Node

Nethereum includes a complete in-process Ethereum execution layer. Run a development chain for testing, a persistent node with RocksDB, or a custom application chain with sequencing, P2P networking, and L1 anchoring.

### Core Node

| Package | Description |
|---|---|
| [Nethereum.CoreChain](src/Nethereum.CoreChain/) | Full in-process Ethereum node: JSON-RPC handlers (`eth_*`, `net_*`, `web3_*`, `debug_traceTransaction`, `debug_traceCall`), state management, block production, transaction pool, filter management, WebSocket subscriptions, historical state with state diff tracking |
| [Nethereum.CoreChain.RocksDB](src/Nethereum.CoreChain.RocksDB/) | RocksDB persistent storage for blocks, transactions, receipts, logs, state, and state diffs |

### Development Chain

| Package | Description |
|---|---|
| [Nethereum.DevChain](src/Nethereum.DevChain/) | Development chain: pre-funded accounts (10,000 ETH each), auto-mine, configurable block time, SQLite persistence (default), time manipulation (`evm_increaseTime`, `evm_setNextBlockTimestamp`) |
| [Nethereum.DevChain.Server](src/Nethereum.DevChain.Server/) | HTTP server wrapper for DevChain with CORS, health checks, and ASP.NET Core hosted service. Compatible with MetaMask, Foundry, Hardhat, and any Ethereum tooling |

### Application Chain (Preview)

Nethereum.AppChain extends CoreChain to provide an application-specific chain layer. Applications run their own chain for domain-specific data and business rules — game state, social graphs, content, governance — while users retain the ability to exit with their data at any time. Financial assets and high-value state remain on L1s and L2s where they benefit from full Ethereum security.

| Package | Description |
|---|---|
| [Nethereum.AppChain](src/Nethereum.AppChain/) | Application chain core: custom chain configuration and node setup |
| [Nethereum.AppChain.Server](src/Nethereum.AppChain.Server/) | HTTP server for application chains |
| [Nethereum.AppChain.Sequencer](src/Nethereum.AppChain.Sequencer/) | Transaction ordering and sequencing |
| [Nethereum.AppChain.Sync](src/Nethereum.AppChain.Sync/) | Multi-node state synchronisation |
| [Nethereum.AppChain.P2P](src/Nethereum.AppChain.P2P/) | P2P networking abstractions |
| [Nethereum.AppChain.P2P.DotNetty](src/Nethereum.AppChain.P2P.DotNetty/) | P2P implementation using DotNetty transport |
| [Nethereum.AppChain.P2P.Server](src/Nethereum.AppChain.P2P.Server/) | Standalone P2P server hosting |
| [Nethereum.AppChain.Policy](src/Nethereum.AppChain.Policy/) | Governance, validation rules, and access policies |
| [Nethereum.AppChain.Anchoring](src/Nethereum.AppChain.Anchoring/) | L1 anchoring for state commitment and data availability |
| [Nethereum.Consensus.Clique](src/Nethereum.Consensus.Clique/) | Clique Proof-of-Authority consensus |

---

## 6. Account Abstraction (ERC-4337 / ERC-7579)

Full ERC-4337 account abstraction stack: UserOperation creation and validation, a complete bundler with mempool and gas estimation, an RPC server, and ERC-7579 modular smart account contracts (validators, executors, hooks, session keys, paymasters).

| Package | Description |
|---|---|
| [Nethereum.AccountAbstraction](src/Nethereum.AccountAbstraction/) | UserOperation creation, encoding, gas estimation, and validation |
| [Nethereum.AccountAbstraction.Bundler](src/Nethereum.AccountAbstraction.Bundler/) | Full ERC-4337 bundler: mempool management, reputation tracking, BLS aggregation, bundle building and submission |
| Nethereum.AccountAbstraction.Bundler.RocksDB | RocksDB-backed persistent UserOperation mempool and reputation storage |
| [Nethereum.AccountAbstraction.Bundler.RpcServer](src/Nethereum.AccountAbstraction.Bundler.RpcServer/) | JSON-RPC server: `eth_sendUserOperation`, `eth_estimateUserOperationGas`, `eth_getUserOperationByHash`, `eth_getUserOperationReceipt`, `eth_supportedEntryPoints` |
| [Nethereum.AccountAbstraction.SimpleAccount](src/Nethereum.AccountAbstraction.SimpleAccount/) | SimpleAccount smart account and factory interaction |
| [Nethereum.AccountAbstraction.AppChain](src/Nethereum.AccountAbstraction.AppChain/) | Account abstraction integration for AppChain (gasless UX, session keys on app chains) |

---

## 7. Blockchain Data Processing & Storage

Libraries for crawling blockchain data, detecting chain reorganisations, indexing tokens and internal transactions, and persisting to relational databases using Entity Framework Core.

### Processing Engine

| Package | Description |
|---|---|
| [Nethereum.BlockchainProcessing](src/Nethereum.BlockchainProcessing/) | Block/transaction/log crawling orchestrator with reorg detection, token transfer indexing (ERC-20/721/1155), internal transaction extraction, progress tracking, and retry logic |

### Database Storage (Entity Framework Core)

| Package | Description |
|---|---|
| [Nethereum.BlockchainStore.EFCore](src/Nethereum.BlockchainStore.EFCore/) | Shared EF Core base: entity mappings for blocks, transactions, logs, contracts, token transfers, token balances, NFT inventory |
| [Nethereum.BlockchainStore.Postgres](src/Nethereum.BlockchainStore.Postgres/) | PostgreSQL provider with snake_case naming conventions |
| [Nethereum.BlockchainStore.SqlServer](src/Nethereum.BlockchainStore.SqlServer/) | SQL Server provider with configurable schema |
| [Nethereum.BlockchainStore.Sqlite](src/Nethereum.BlockchainStore.Sqlite/) | SQLite provider for lightweight/embedded scenarios |

### Processing Services (Hosted / DI-Ready)

| Package | Description |
|---|---|
| [Nethereum.BlockchainStorage.Processors](src/Nethereum.BlockchainStorage.Processors/) | Shared processing services: `BlockchainProcessingService`, `InternalTransactionProcessingService`, hosted service wrappers, options binding |
| [Nethereum.BlockchainStorage.Processors.Postgres](src/Nethereum.BlockchainStorage.Processors.Postgres/) | PostgreSQL DI wiring: `AddPostgresBlockchainProcessor()` |
| [Nethereum.BlockchainStorage.Processors.SqlServer](src/Nethereum.BlockchainStorage.Processors.SqlServer/) | SQL Server DI wiring: `AddSqlServerBlockchainProcessor()` |
| [Nethereum.BlockchainStorage.Processors.Sqlite](src/Nethereum.BlockchainStorage.Processors.Sqlite/) | SQLite DI wiring: `AddSqliteBlockchainProcessor()` |
| [Nethereum.BlockchainStorage.Token.Postgres](src/Nethereum.BlockchainStorage.Token.Postgres/) | Token processing pipeline: ERC-20/721/1155 transfer indexing, balance aggregation, metadata resolution |

### Explorer

| Package | Description |
|---|---|
| [Nethereum.Explorer](src/Nethereum.Explorer/) | Blazor Server blockchain explorer: block/transaction/log browsing, ABI-decoded contract interaction (read/write via EIP-6963 wallet), token pages (transfers, balances, metadata), MUD table viewer |

---

## 8. MUD Framework

Client libraries for interacting with [MUD](https://mud.dev/) autonomous World instances, processing store events, and persisting/normalising MUD table data to relational databases.

| Package | Description |
|---|---|
| [Nethereum.Mud](src/Nethereum.Mud/) | MUD client: table schema queries, record encoding/decoding, store subscriptions, predicate-based queries |
| [Nethereum.Mud.Contracts](src/Nethereum.Mud.Contracts/) | MUD core contract services (Store, World) and store event log processing |
| [Nethereum.Mud.Repositories.EntityFramework](src/Nethereum.Mud.Repositories.EntityFramework/) | EF Core repository for MUD store records with chain state tracking |
| [Nethereum.Mud.Repositories.Postgres](src/Nethereum.Mud.Repositories.Postgres/) | PostgreSQL MUD store: data normalisation, background processing, hosted service, SQL-based querying |
| [Nethereum.MudBlazorComponents](src/Nethereum.MudBlazorComponents/) | Blazor UI components for MUD table interaction (query by key, upsert, deploy, scan assembly for tables) |

---

## 9. Wallet & UI Frameworks

Multi-platform wallet implementation using MVVM architecture (CommunityToolkit.Mvvm) with shared ViewModels and platform-specific renderers for Blazor, Avalonia, MAUI, and Unity.

### Wallet Core

| Package | Description |
|---|---|
| [Nethereum.Wallet](src/Nethereum.Wallet/) | Core wallet services: transaction building, signing, state changes preview (EVM simulation before signing), token management |
| [Nethereum.UI](src/Nethereum.UI/) | Shared UI abstractions: host providers, Ethereum authentication |

### UI Components (MVVM ViewModels)

| Package | Description |
|---|---|
| [Nethereum.Wallet.UI.Components](src/Nethereum.Wallet.UI.Components/) | Platform-agnostic MVVM ViewModels for all wallet screens (accounts, networks, tokens, transactions, settings) with localisation (EN/ES) |
| [Nethereum.Wallet.UI.Components.Trezor](src/Nethereum.Wallet.UI.Components.Trezor/) | Trezor-specific wallet ViewModels |

### Platform Renderers

| Package | Description |
|---|---|
| [Nethereum.Wallet.UI.Components.Blazor](src/Nethereum.Wallet.UI.Components.Blazor/) | Blazor Server/WASM wallet UI (MudBlazor-based forms, validation, contract interaction) |
| [Nethereum.Wallet.UI.Components.Blazor.Trezor](src/Nethereum.Wallet.UI.Components.Blazor.Trezor/) | Blazor Trezor hardware wallet UI |
| Nethereum.Wallet.UI.Components.Avalonia | Avalonia desktop wallet UI |
| [Nethereum.Wallet.UI.Components.Maui](src/Nethereum.Wallet.UI.Components.Maui/) | .NET MAUI mobile/desktop wallet UI |

### Web & Browser Wallet Integration

| Package | Description |
|---|---|
| [Nethereum.Blazor](src/Nethereum.Blazor/) | Blazor integration: EIP-6963 multi-wallet discovery, Ethereum authentication state provider, dynamic contract interaction components (query, transact, deploy — no code generation needed) |
| [Nethereum.Blazor.Solidity](src/Nethereum.Blazor.Solidity/) | EVM step-through debugger and Solidity source viewer Blazor components |
| [Nethereum.EIP6963WalletInterop](src/Nethereum.EIP6963WalletInterop/) | EIP-6963 multi-wallet discovery protocol JavaScript interop |
| [Nethereum.Metamask](src/Nethereum.Metamask/) | MetaMask wallet provider abstraction |
| [Nethereum.Metamask.Blazor](src/Nethereum.Metamask.Blazor/) | MetaMask Blazor integration |
| [Nethereum.WalletConnect](src/Nethereum.WalletConnect/) | WalletConnect v2 protocol integration |
| [Nethereum.Reown.AppKit.Blazor](src/Nethereum.Reown.AppKit.Blazor/) | Reown (WalletConnect) AppKit modal for Blazor |

### Unity & Gaming

| Package | Description |
|---|---|
| Nethereum.Unity | Unity game engine integration: coroutine-based RPC, transaction signing, contract interaction |
| [Nethereum.Unity.EIP6963](src/Nethereum.Unity.EIP6963/) | EIP-6963 wallet discovery for Unity WebGL builds |
| Nethereum.Unity.Metamask | MetaMask integration for Unity WebGL |

### Mobile

| Package | Description |
|---|---|
| Nethereum.Maui.AndroidUsb | Android USB transport for hardware wallet communication |

---

## 10. Ecosystem & Extensions

### Client Extensions

| Package | Description |
|---|---|
| [Nethereum.Geth](src/Nethereum.Geth/) | Geth-specific RPC methods (admin, miner, personal, txpool, debug tracers) |
| [Nethereum.Besu](src/Nethereum.Besu/) | Hyperledger Besu-specific RPC methods (permissioning, privacy, IBFT/QBFT) |

### Consensus & Cryptography

| Package | Description |
|---|---|
| [Nethereum.Merkle](src/Nethereum.Merkle/) | Merkle tree implementations: standard, incremental (cached layers), frontier (append-only) |
| [Nethereum.Merkle.Patricia](src/Nethereum.Merkle.Patricia/) | Modified Merkle Patricia Trie for state and storage root calculation |
| [Nethereum.Ssz](src/Nethereum.Ssz/) | Simple Serialize (SSZ) encoding for Ethereum consensus layer |
| [Nethereum.Consensus.LightClient](src/Nethereum.Consensus.LightClient/) | Ethereum consensus light client (sync committee verification) |
| Nethereum.Beaconchain | Beacon chain data structures and API types |

### Data & Metadata Services

| Package | Description |
|---|---|
| Nethereum.DataServices | Data aggregation and external API integration |
| [Nethereum.TokenServices](src/Nethereum.TokenServices/) | Token metadata discovery: logos, chain info, CoinGecko integration |
| Nethereum.Sourcify.Database | Sourcify contract verification and ABI database (V2 API, parquet download) |
| [Nethereum.ChainStateVerification](src/Nethereum.ChainStateVerification/) | Chain state integrity and consistency verification |

### .NET Aspire Orchestration

The `aspire/` directory provides .NET Aspire orchestration for spinning up a complete Ethereum development environment — DevChain, PostgreSQL, blockchain indexer, explorer, and optional bundler — with a single `dotnet run`.

| Resource | Description |
|---|---|
| `aspire/devchain/` | Dev environment: AppHost + DevChain + Indexer + Explorer + Bundler + LoadGenerator, wired with Aspire service discovery and OpenTelemetry |
| `aspire/shared/` | Shared Aspire ServiceDefaults (health checks, resilient HTTP, telemetry) |
| `aspire/templates/` | `dotnet new nethereum-devchain` template: generates a standalone NuGet-based Aspire solution with configurable `--NethereumVersion`, `--ChainId`, and `--AspireVersion` |

---

## Supported Platforms

| Target | Scope |
|---|---|
| netstandard2.0, net451, net461, net6.0, net8.0, net9.0, net10.0 | Core libraries |
| net8.0, net9.0, net10.0 | CoreChain |
| net8.0, net10.0 | AppChain |
| net10.0 | Server components, Wallet UI |
| net6.0, net8.0, net9.0, net10.0 | Blazor UI |
| net461, net472, netstandard2.1 | Unity |

## Repository Layout

```
src/                        Core libraries (130+ projects)
aspire/                     .NET Aspire orchestration (devchain/, shared/, templates/)
contracts/                  Solidity contracts (Foundry project)
generators/                 Code generation CLI tools and NuGet packages
tests/                      Unit and integration test projects
consoletests/               Sample console applications and demos
buildConf/                  Build configuration (Version.props, Frameworks.props)
testchain/                  Pre-configured development chain binaries
```
