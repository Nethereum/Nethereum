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

Nethereum includes 130+ packages organised into focused libraries. For a complete guide to every capability, package, and how-to — see **[What Do You Want to Do?](https://docs.nethereum.com/docs/what-do-you-want-to-do)** on the documentation site.

### [Core Foundation](https://docs.nethereum.com/docs/core-foundation/overview)

- Query balances (ETH, ERC-20, ERC-721, ERC-1155) with built-in typed services — no ABI needed
- Send ETH and contract transactions (gas, nonce, EIP-1559 fees — all automatic)
- Delegate an EOA to a smart contract with [EIP-7702](https://docs.nethereum.com/docs/core-foundation/guide-eip7702) (`web3.Eth.GetEIP7022AuthorisationService()`)
- Query blocks, transactions, and receipts
- Decode raw transaction input and recover sender addresses
- ABI encoding/decoding, RLP serialization, hex/address utilities

### [Signing & Key Management](https://docs.nethereum.com/docs/signing-and-key-management/overview)

- Sign transactions offline
- Use an HD wallet (BIP32/BIP39)
- Sign with Trezor or Ledger hardware wallets
- Sign with AWS KMS or Azure Key Vault
- Sign EIP-712 typed structured data

### [EVM Simulator](https://docs.nethereum.com/docs/evm-simulator/overview)

- Simulate transactions in-process — preview state changes, token transfers, and balance impacts before signing
- Step-by-step EVM debugging with opcode traces, stack, and storage inspection
- Capture and compare full execution traces between local simulation and live chain
- Extract state diffs, log emissions, and internal calls from any transaction

### [DevChain & Local Development](https://docs.nethereum.com/docs/devchain/overview)

- Run a local dev chain — no external node required (pre-funded accounts, auto-mine, time manipulation)
- Spin up a full dev environment with .NET Aspire (DevChain + PostgreSQL + Indexer + Explorer)

### [Code Generation](https://docs.nethereum.com/docs/smart-contracts/overview)

- Generate C# contract services from Solidity ABI
- Generate UI components from contract definitions
- Generate MUD table services and queries

### [Data & Indexing](https://docs.nethereum.com/docs/data-and-indexing/overview)

- Index blockchain data to a database (PostgreSQL, SQL Server, or SQLite)
- Index token transfers and compute balances
- Build a [blockchain explorer](https://docs.nethereum.com/docs/data-and-indexing/guide-explorer) (Blazor Server with ABI decoding, token pages, MUD table browsing)
- Discover and scan token balances across wallets (multicall batching)

### [Data Services](https://docs.nethereum.com/docs/data-services/overview)

- Fetch ABI and source from Sourcify or Etherscan
- Get token prices, metadata, and logos (CoinGecko integration)
- Discover RPC endpoints via Chainlist
- Look up function/event signatures (4Byte Directory)

### [DeFi & Protocols](https://docs.nethereum.com/docs/defi/overview)

- Swap tokens on Uniswap (V2/V3/V4)
- Use Permit2 for gasless token approvals
- Accept crypto payments in your API (x402 server middleware + facilitator)
- Pay for x402-protected API endpoints (client with EIP-3009 signed authorizations)
- Resolve ENS names (typed ENS services built-in)
- Use Gnosis Safe multi-sig
- Interact with Circles UBI protocol

### [Account Abstraction](https://docs.nethereum.com/docs/account-abstraction/overview)

- Use smart accounts (ERC-4337 UserOperations)
- Build an ERC-4337 bundler (mempool, gas estimation, reputation tracking)
- Run a bundler RPC server
- Deploy ERC-7579 modular smart accounts (validators, executors, hooks, session keys, paymasters)

### [MUD — Autonomous Worlds](https://docs.nethereum.com/docs/mud-framework/overview)

- Work with MUD World systems and tables — systems are smart contracts that must be registered and have their own lifecycle (registration, discovery, access control), while tables define on-chain schemas with typed encoding and store events
- Index and normalise MUD store records to Postgres
- Query normalised MUD tables with predicates
- Build MUD table UIs in Blazor

### [Wallet SDK](https://docs.nethereum.com/docs/wallet-sdk/overview)

- Build a multi-platform wallet app (MVVM architecture with Blazor, MAUI, and Avalonia renderers)
- Manage accounts (mnemonic, private key, keystore) with encrypted vault storage
- Interact with any contract dynamically — no code generation needed

### [Blazor dApp Integration](https://docs.nethereum.com/docs/blazor-dapp-integration/overview)

- Integrate browser wallets in Blazor (EIP-6963 multi-wallet discovery)
- Connect via WalletConnect / Reown AppKit
- Implement Sign-In with Ethereum (SIWE) authentication

### [Unity](https://docs.nethereum.com/docs/unity/overview)

- Build Unity games with Ethereum — coroutine-based RPC, ERC-20/721 tokens, WebGL wallet connectivity

### [Verification & Cryptography](https://docs.nethereum.com/docs/consensus-light-client/overview)

- Verify beacon chain state via light client
- Validate account balances and state against proofs
- Calculate Merkle proofs and state roots (Patricia Trie)

### [Client Extensions](https://docs.nethereum.com/docs/client-extensions/overview)

- Access Geth admin, debug, miner, and personal APIs
- Access Besu-specific permissioning and IBFT APIs

### [AppChains (Preview)](https://docs.nethereum.com/docs/application-chain/overview)

- Run a custom application chain — domain-specific Ethereum extension layers with full EVM, sequencer, and L1 anchoring
- Configure RocksDB persistent storage
- Sync follower nodes with multi-peer failover and state verification
- Use System.Text.Json / AOT-friendly RPC
- Stream real-time data via WebSocket subscriptions

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
