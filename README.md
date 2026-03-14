# Nethereum

[![Documentation](https://img.shields.io/badge/docs-docs.nethereum.com-blue)](https://docs.nethereum.com) [![NuGet version](https://badge.fury.io/nu/nethereum.web3.svg)](https://badge.fury.io/nu/nethereum.web3) [![Discord](https://img.shields.io/discord/765580659816636426?label=Discord&logo=discord)](https://discord.gg/u3Ej2BReNn)

Nethereum is the .NET integration platform for Ethereum and EVM-compatible blockchains. It provides a complete development stack — from smart contract interaction and transaction signing, through a full EVM simulator and in-process Ethereum node, to blockchain data indexing, an ERC-4337 account abstraction bundler, a Blazor blockchain explorer, MUD framework support, multi-platform wallet UIs, Unity game integration, and .NET Aspire orchestration. Nethereum targets netstandard 2.0 through .NET 10, .NET Framework 4.5.1+, and Unity, running on Windows, Linux, macOS, Android, iOS, WebAssembly, and game consoles.

## Try Nethereum in Your Browser

Go to [playground.nethereum.com](http://playground.nethereum.com) to browse and execute samples directly in your browser — no setup required.

[![Nethereum Playground](screenshots/playground.png)](http://playground.nethereum.com)

## Quick Start

```
dotnet add package Nethereum.Web3
```

### Get an account balance

```csharp
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_API_KEY");
var balance = await web3.Eth.GetBalance.SendRequestAsync("0xde0b295669a9fd93d5f28d9ec85e40f4cb697bae");
Console.WriteLine($"Balance: {Web3.Convert.FromWei(balance.Value)} ETH");
```

### Transfer ETH

```csharp
var account = new Account("YOUR_PRIVATE_KEY");
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR_API_KEY");

var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xRecipientAddress", 0.01m);
```

### Interact with a smart contract

```csharp
// Use code-generated typed services (see Code Generation below)
var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
var receipt = await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress,
    new TransferFunction { To = recipientAddress, Value = amount });
```

## What You Can Build

Nethereum includes 130+ packages organised into focused libraries. Every row below links to a how-to guide on the [documentation site](https://docs.nethereum.com/docs/what-do-you-want-to-do).

### [Core Foundation](https://docs.nethereum.com/docs/core-foundation/overview)

| I want to... | Guide |
|---|---|
| Query ETH, ERC-20, ERC-721, and ERC-1155 balances (built-in typed services, no ABI needed) | [Query Balance](https://docs.nethereum.com/docs/core-foundation/guide-query-balance) |
| Send ETH (gas, nonce, EIP-1559 fees — all automatic) | [Transfer Ether](https://docs.nethereum.com/docs/core-foundation/guide-send-eth) |
| Send a transaction with custom data or fees | [Send Transactions](https://docs.nethereum.com/docs/core-foundation/guide-send-transaction) |
| Delegate an EOA to a smart contract (EIP-7702) | [EIP-7702](https://docs.nethereum.com/docs/core-foundation/guide-eip7702) |
| Query blocks, transactions, and receipts | [Query Blocks](https://docs.nethereum.com/docs/core-foundation/guide-query-blocks) |
| Estimate and customize gas fees | [Fee Estimation](https://docs.nethereum.com/docs/core-foundation/guide-fee-estimation) |
| Replace or speed up a pending transaction | [Transaction Replacement](https://docs.nethereum.com/docs/core-foundation/guide-transaction-replacement) |
| Decode function calls from transaction input data | [Decode Transactions](https://docs.nethereum.com/docs/core-foundation/guide-decode-transactions) |
| ABI encode/decode (abi.encode, abi.encodePacked, EIP-712) | [ABI Encoding](https://docs.nethereum.com/docs/core-foundation/guide-abi-encoding) |
| Stream real-time data (new blocks, pending txns, event logs) | [Real-Time Streaming](https://docs.nethereum.com/docs/core-foundation/guide-realtime-streaming) |

### [Signing & Key Management](https://docs.nethereum.com/docs/signing-and-key-management/overview)

| I want to... | Guide |
|---|---|
| Generate keys and create accounts | [Keys & Accounts](https://docs.nethereum.com/docs/signing-and-key-management/guide-keys-accounts) |
| Sign and verify messages | [Message Signing](https://docs.nethereum.com/docs/signing-and-key-management/guide-message-signing) |
| Sign EIP-712 typed structured data | [EIP-712 Signing](https://docs.nethereum.com/docs/signing-and-key-management/guide-eip712-signing) |
| Use HD wallets (BIP32/BIP39 mnemonic) | [HD Wallets](https://docs.nethereum.com/docs/signing-and-key-management/guide-hd-wallets) |
| Sign with Ledger or Trezor hardware wallets | [Hardware Wallets](https://docs.nethereum.com/docs/signing-and-key-management/guide-hardware-wallets) |
| Sign with AWS KMS or Azure Key Vault | [Cloud KMS](https://docs.nethereum.com/docs/signing-and-key-management/guide-cloud-kms) |

### [Smart Contracts](https://docs.nethereum.com/docs/smart-contracts/overview)

| I want to... | Guide |
|---|---|
| Deploy, call, and send transactions to contracts | [Smart Contract Interaction](https://docs.nethereum.com/docs/smart-contracts/guide-smart-contract-interaction) |
| Work with ERC-20 tokens (balance, transfer, approve) | [ERC-20 Tokens](https://docs.nethereum.com/docs/smart-contracts/erc20) |
| Generate C# services from Solidity ABI | [Code Generation](https://docs.nethereum.com/docs/smart-contracts/code-generation) |
| Filter and query contract events | [Events](https://docs.nethereum.com/docs/smart-contracts/guide-events) |
| Batch queries with Multicall or RPC batching | [Multicall](https://docs.nethereum.com/docs/smart-contracts/guide-multicall) |
| Deploy to deterministic addresses with CREATE2 | [CREATE2](https://docs.nethereum.com/docs/smart-contracts/guide-create2-deployment) |

### [EVM Simulator](https://docs.nethereum.com/docs/evm-simulator/overview)

| I want to... | Guide |
|---|---|
| Simulate a transaction and preview state changes | [Transaction Simulation](https://docs.nethereum.com/docs/evm-simulator/guide-transaction-simulation) |
| Debug EVM execution step-by-step (opcodes, stack, storage) | [EVM Debugging](https://docs.nethereum.com/docs/evm-simulator/guide-evm-debugging) |
| Decode nested call trees (contract-to-contract calls) | [Call Tree Decoding](https://docs.nethereum.com/docs/evm-simulator/guide-call-tree-decoding) |
| Simulate ERC-20 transfers and approvals | [ERC-20 Simulation](https://docs.nethereum.com/docs/evm-simulator/guide-erc20-simulation) |
| Execute and disassemble raw bytecode | [Bytecode Execution](https://docs.nethereum.com/docs/evm-simulator/guide-bytecode-execution) |

### [DevChain & Local Development](https://docs.nethereum.com/docs/devchain/overview)

| I want to... | Guide |
|---|---|
| Run a local dev chain (no external node needed) | [DevChain Quickstart](https://docs.nethereum.com/docs/devchain/devchain-quickstart) |
| Fork a live network and manipulate state/time | [Forking & State](https://docs.nethereum.com/docs/devchain/guide-forking-and-state) |
| Trace and debug transactions (opcode-level) | [Debug & Trace](https://docs.nethereum.com/docs/devchain/guide-debug-trace) |
| Spin up a full dev environment with Aspire (DevChain + PostgreSQL + Indexer + Explorer) | `dotnet new nethereum-devchain` |

### [DeFi & Protocols](https://docs.nethereum.com/docs/defi/overview)

| I want to... | Guide |
|---|---|
| Swap tokens on Uniswap (V2/V3/V4) | [Uniswap Swap](https://docs.nethereum.com/docs/defi/guide-uniswap-swap) |
| Manage Uniswap liquidity positions | [Uniswap Liquidity](https://docs.nethereum.com/docs/defi/guide-uniswap-liquidity) |
| Execute Gnosis Safe multi-sig transactions | [Gnosis Safe](https://docs.nethereum.com/docs/defi/guide-gnosis-safe) |
| Accept or pay for crypto payments (x402) | [x402 Payments](https://docs.nethereum.com/docs/defi/guide-x402-payments) |

### [Account Abstraction](https://docs.nethereum.com/docs/account-abstraction/overview)

| I want to... | Guide |
|---|---|
| Create and send a UserOperation | [Send UserOperation](https://docs.nethereum.com/docs/account-abstraction/guide-send-useroperation) |
| Deploy a smart account | [Smart Account Deployment](https://docs.nethereum.com/docs/account-abstraction/guide-smart-account-deployment) |
| Batch operations and use paymasters | [Batching & Paymasters](https://docs.nethereum.com/docs/account-abstraction/guide-batching-and-paymasters) |
| Use ERC-7579 modular accounts (validators, hooks, session keys) | [Modular Accounts](https://docs.nethereum.com/docs/account-abstraction/guide-modular-accounts) |
| Run an ERC-4337 bundler | [Run Bundler](https://docs.nethereum.com/docs/account-abstraction/guide-run-bundler) |

### [Data, Indexing & Explorer](https://docs.nethereum.com/docs/data-and-indexing/overview)

| I want to... | Guide |
|---|---|
| Index blockchain data to PostgreSQL / SQL Server / SQLite | [Database Storage](https://docs.nethereum.com/docs/data-and-indexing/guide-database-storage) |
| Index ERC-20/721/1155 token transfers and balances | [Token Indexing](https://docs.nethereum.com/docs/data-and-indexing/guide-token-indexing) |
| Build a blockchain explorer (ABI decoding, token pages, contract interaction) | [Blockchain Explorer](https://docs.nethereum.com/docs/data-and-indexing/guide-explorer) |
| Scan token balances via multicall (no indexer needed) | [Token Portfolio](https://docs.nethereum.com/docs/data-services/guide-token-portfolio) |
| Fetch ABI from Sourcify or Etherscan | [ABI Retrieval](https://docs.nethereum.com/docs/data-services/guide-abi-retrieval) |
| Get token prices and metadata (CoinGecko) | [CoinGecko API](https://docs.nethereum.com/docs/data-services/guide-coingecko-api) |

### [MUD](https://docs.nethereum.com/docs/mud-framework/overview)

| I want to... | Guide |
|---|---|
| Read, write, and query MUD table records | [MUD Tables](https://docs.nethereum.com/docs/mud-framework/guide-mud-tables) |
| Index MUD Store events to PostgreSQL | [MUD Indexing](https://docs.nethereum.com/docs/mud-framework/guide-mud-indexing) |
| Deploy a MUD World with tables and systems | [MUD Deployment](https://docs.nethereum.com/docs/mud-framework/guide-mud-deployment) |

### [Wallet SDK](https://docs.nethereum.com/docs/wallet-sdk/overview) · [Blazor dApp](https://docs.nethereum.com/docs/blazor-dapp-integration/overview) · [Unity](https://docs.nethereum.com/docs/unity/overview)

| I want to... | Guide |
|---|---|
| Build a multi-platform wallet app (Blazor, MAUI, Avalonia) | [Wallet Quickstart](https://docs.nethereum.com/docs/wallet-sdk/guide-wallet-quickstart) |
| Create accounts (mnemonic, private key, vault encryption) | [Wallet Accounts](https://docs.nethereum.com/docs/wallet-sdk/guide-wallet-accounts) |
| Connect browser wallets in Blazor (EIP-6963 discovery) | [Blazor Wallet Connect](https://docs.nethereum.com/docs/blazor-dapp-integration/guide-blazor-wallet-connect) |
| Authenticate with Sign-In with Ethereum (SIWE) | [Blazor Authentication](https://docs.nethereum.com/docs/blazor-dapp-integration/guide-blazor-authentication) |
| Interact with any contract dynamically (no codegen) | [Blazor Contract Interaction](https://docs.nethereum.com/docs/blazor-dapp-integration/guide-blazor-contract-interaction) |
| Build Unity games with Ethereum | [Unity Quickstart](https://docs.nethereum.com/docs/unity/guide-unity-quickstart) |

### [Verification & Cryptography](https://docs.nethereum.com/docs/consensus-light-client/overview)

| I want to... | Guide |
|---|---|
| Verify ETH balances without trusting RPC | [Verified State](https://docs.nethereum.com/docs/consensus-light-client/guide-verified-state) |
| Track finalized beacon headers via light client | [Light Client](https://docs.nethereum.com/docs/consensus-light-client/guide-light-client) |

### [AppChains (Preview)](https://docs.nethereum.com/docs/application-chain/overview)

| I want to... | Guide |
|---|---|
| Launch a sequencer and deploy contracts | [AppChain Quickstart](https://docs.nethereum.com/docs/application-chain/guide-appchain-quickstart) |
| Use AppChainBuilder for embedded/testing | [AppChain Quickstart](https://docs.nethereum.com/docs/application-chain/guide-appchain-quickstart) |
| Configure RocksDB persistent storage | [AppChain Storage](https://docs.nethereum.com/docs/application-chain/guide-appchain-storage) |
| Sync follower nodes and verify state | [AppChain Sync](https://docs.nethereum.com/docs/application-chain/guide-appchain-sync) |

> For the full list of 100+ use cases with packages, see **[What Do You Want to Do?](https://docs.nethereum.com/docs/what-do-you-want-to-do)** on the documentation site.

## Code Generation

Generate typed C# contract services directly from Solidity ABI using the [VS Code Solidity extension](https://marketplace.visualstudio.com/items?itemName=JuanBlanco.solidity) or the CLI tool:

[![Code generation of Contract Definitions](https://github.com/juanfranblanco/vscode-solidity/raw/master/screenshots/compile-codegnerate-nethereum.png)](https://marketplace.visualstudio.com/items?itemName=JuanBlanco.solidity)

```
dotnet tool install -g Nethereum.Generator.Console
```

This generates typed service classes, function/event DTOs, deployment messages, and optionally Blazor UI components and MUD table services.

## Templates

### Nethereum.Templates.Pack

The templates pack provides ready-to-use project templates for smart contract development, Blazor integration, and authentication.

```
dotnet new install Nethereum.Templates.Pack
```

| Template | Short Name | Description |
|---|---|---|
| Smart Contract Library + ERC20 XUnit | `smartcontract` | Smart contract library with ERC20 example, auto code generation, and integration tests |
| ERC721/ERC1155 Open Zeppelin + XUnit | `nethereum-erc721-oz` | NFT and multi-token development with OpenZeppelin |
| Blazor Metamask Wasm/Server | `nethereum-mm-blazor` | Blazor integration with MetaMask (Wasm and Server) |
| Blazor SIWE Wasm/Server/REST API | `nethereum-siwe` | Sign-In with Ethereum authentication |
| WebSocket Streaming | `nethereum-ws-stream` | Real-time blockchain data streaming |

Source and details: [Nethereum.Templates.SmartContractDefault](https://github.com/Nethereum/Nethereum.Templates.SmartContractDefault), [Nethereum.Templates.SmartContracts.OZ-Erc721-Erc1155](https://github.com/Nethereum/Nethereum.Templates.SmartContracts.OZ-Erc721-Erc1155), [Nethereum.Templates.Metamask.Blazor](https://github.com/Nethereum/Nethereum.Templates.Metamask.Blazor), [Nethereum.Templates.Siwe](https://github.com/Nethereum/Nethereum.Templates.Siwe)

### Nethereum.DevChain.Template (.NET Aspire)

Spin up a complete Ethereum development environment with a single command:

```
dotnet new install Nethereum.DevChain.Template
dotnet new nethereum-devchain -n MyChain
cd MyChain/AppHost && dotnet run
```

This creates an Aspire-orchestrated solution with a DevChain node, PostgreSQL database, blockchain indexer (blocks, transactions, tokens, MUD), and a Blazor blockchain explorer — all wired with service discovery, health checks, and OpenTelemetry.

## Wallets & End-to-End Examples

| Project | Description |
|---------|-------------|
| [Explorer Wallet (Blazor/MAUI)](https://github.com/Nethereum/Nethereum-Explorer-Wallet-Template-Blazor) | Blazor Wasm SPA, Desktop, Android, iOS — light explorer and wallet. [Try it live](https://explorer.nethereum.com) |
| [Desktop Wallet (Avalonia)](https://github.com/Nethereum/Nethereum.UI.Desktop) | Cross-platform desktop wallet with ReactiveUI |
| [Unity3d Sample Template](https://github.com/Nethereum/Unity3dSampleTemplate) | Unity starter — balance query, ERC20, MetaMask, cross-platform |
| [Unity WebGL + MetaMask](https://github.com/Nethereum/Nethereum.Unity.Webgl) | Deploy and interact with ERC721 NFTs from Unity WebGL |
| [Nethereum Flappy](https://github.com/Nethereum/Nethereum.Flappy) | Unity game integrating with Ethereum |

Unity libraries: install via **[Nethereum.Unity](https://github.com/Nethereum/Nethereum.Unity)** (git URL) or from `src/compiledlibraries/` in each [GitHub release](https://github.com/Nethereum/Nethereum/releases). Try samples in the [Nethereum Playground](http://playground.nethereum.com).

## More Examples

**Demos** (`src/demos/`):
- [Wallet Blazor Demo](src/demos/Nethereum.Wallet.Blazor.Demo/) — Blazor Server wallet application
- [X402 Simple Client](src/demos/Nethereum.X402.SimpleClient/) — pay for x402-protected API endpoints
- [X402 Simple Server](src/demos/Nethereum.X402.SimpleServer/) — accept crypto payments in your API
- [X402 Facilitator Server](src/demos/Nethereum.X402.FacilitatorServer/FacilitatorServer/) — payment verification service

**Console Examples** (`consoletests/`):
- [DevChain Integration Demo](consoletests/Nethereum.DevChain.IntegrationDemo/) — in-process Ethereum node usage
- [Metamask Blazor Example](consoletests/MetamaskExampleBlazor.Wasm/) — MetaMask Blazor Wasm integration
- [Blazor Example Project](consoletests/BlazorExampleProject.Wasm/) — Blazor Wasm starter
- [WalletConnect Blazor](consoletests/NethereumWCBlazor/) — WalletConnect v2 integration
- [WalletConnect Avalonia](consoletests/NethereumWCAvalonia/) — WalletConnect with Avalonia desktop
- [Reown AppKit Blazor](consoletests/NethereumReownAppKitBlazor/) — Reown (WalletConnect) AppKit modal
- [Godot + WalletConnect Avalonia](consoletests/NethereumGodotWCAvalonia/) — Godot game engine integration
- [MUD Log Processing](consoletests/NethereumMudLogProcessing/) — MUD store event indexing
- [MUD Store REST API](consoletests/NethereumMudStoredRecordsRestApi/) — serve MUD records via REST
- [HD Wallet Blazor Test](consoletests/Nethereum.HDWallet.BlazorTest/) — BIP32/BIP39 wallet in Blazor
- [Trezor Console](consoletests/Nethereum.Signer.Trezor.Console/) — Trezor hardware wallet signing
- [Azure Key Vault Console](consoletests/Nethereum.Signer.AzureKeyVault.Console/) — Azure KMS signing
- [WebSocket Streaming](consoletests/Nethereum.WebSocketsStreamingTest/) — real-time blockchain data

## Supported Platforms

| Target | Scope |
|---|---|
| netstandard 2.0, net451, net461, net6.0, net8.0, net9.0, net10.0 | Core libraries |
| net8.0, net10.0 | CoreChain, AppChain, Server components |
| net6.0–net10.0 | Blazor UI |
| net461, net472, netstandard 2.1 | Unity |

Compatible with Windows, Linux, macOS, Android, iOS, WebAssembly, and game consoles.

## Full Component Catalog

For a complete guide covering all 130+ packages organised by use case — with package names, descriptions, and links to individual READMEs — see the **[Component Catalog](https://docs.nethereum.com/docs/component-catalog)** or **[What Do You Want to Do?](https://docs.nethereum.com/docs/what-do-you-want-to-do)**.

## Documentation & Community

- **Documentation**: [docs.nethereum.com](https://docs.nethereum.com)
- **Playground**: [playground.nethereum.com](http://playground.nethereum.com)
- **Discord**: [Join the community](https://discord.gg/u3Ej2BReNn) — technical support, chat, and collaboration

## Thanks and Credits

Building Nethereum has been a journey of over ten years, and it would not exist without the incredible people and community around it.

First and foremost, thank you to my family for their patience, support, and understanding through countless late nights, weekends, and "just one more commit" moments. You made this possible.

Thank you to Cass ([@c055](https://github.com/c055)) for the fantastic Nethereum logo, recreating one of the @ethereumjs logo ideas for our project.

To all my friends across the Ethereum ecosystem — the people I've met at conferences, hackathons, online chats, and working groups over the years. The friendships built through this shared passion for decentralised technology have been one of the most rewarding parts of the journey.

A special thank you to Gael, Dave, Kevin, Caleb, Aaron, and Eva for their help and work on Nethereum — your contributions have been invaluable.

To every contributor who has ever submitted a pull request, reported a bug, suggested a feature, or simply asked a question on GitHub, Discord, or Gitter — you are continuously shaping this project. Every issue filed, every piece of feedback, every "it doesn't work when..." has made Nethereum better.

To the early adopters who believed in Nethereum when it was just getting started and provided invaluable feedback — your trust and patience in those early days meant everything.

To the teams and projects I've had the privilege of collaborating with and trying to help over the years — from enterprise pilots to startup MVPs, from gaming studios to DeFi protocols. Seeing Nethereum used in real-world applications is the ultimate motivation.

To everyone building the Ethereum ecosystem — the client teams (Geth, Besu, Nethermind, Erigon, Reth), the compiler teams (Solidity, Vyper), the tooling teams (Foundry, Hardhat, Remix), the library builders (web3.js, ethers.js, web3j, web3.py), the L2 teams, the researchers, the standard authors, and everyone else pushing this technology forward. We all build on each other's work.

To Consensys, the Ethereum Foundation, and the broader blockchain community for fostering an environment where open-source collaboration thrives.

And to everyone who continues to help, contribute, encourage, and inspire — directly or indirectly. The best is yet to come.
