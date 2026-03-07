# Nethereum

The .NET integration platform for Ethereum and EVM-compatible blockchains. From smart contract interaction to a complete in-process Ethereum node, blockchain indexer, explorer, account abstraction bundler, MUD framework, multi-platform wallets, Unity integration, and .NET Aspire orchestration — 130+ packages targeting netstandard 2.0 through .NET 10 and Unity.

## Nethereum Core

The main Nethereum solution and projects: **[github.com/Nethereum/Nethereum](https://github.com/Nethereum/Nethereum)**

```
dotnet add package Nethereum.Web3
```

For a complete guide to all 130+ packages, see **[COMPONENTS.md](https://github.com/Nethereum/Nethereum/blob/master/COMPONENTS.md)**.

### Key Packages

| Package | NuGet | Description |
|---|---|---|
| [Nethereum.Web3](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Web3) | [![NuGet](https://badge.fury.io/nu/nethereum.web3.svg)](https://badge.fury.io/nu/nethereum.web3) | High-level entry point: RPC, contracts, accounts, signing |
| [Nethereum.Contracts](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Contracts) | [![NuGet](https://badge.fury.io/nu/nethereum.contracts.svg)](https://badge.fury.io/nu/nethereum.contracts) | Smart contract interaction with typed services for ERC-20, ERC-721, ERC-1155, ENS, and more |
| [Nethereum.EVM](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.EVM) | | Full in-process EVM simulator with debugging and state tracing |
| [Nethereum.DevChain.Server](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.DevChain.Server) | | In-process Ethereum dev chain — no external node required |
| [Nethereum.BlockchainProcessing](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.BlockchainProcessing) | [![NuGet](https://badge.fury.io/nu/Nethereum.BlockchainProcessing.svg)](https://badge.fury.io/nu/Nethereum.BlockchainProcessing) | Blockchain data indexing with reorg detection and token transfer processing |
| [Nethereum.Explorer](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Explorer) | | Blazor Server blockchain explorer with ABI decoding and contract interaction |
| [Nethereum.AccountAbstraction](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.AccountAbstraction) | | ERC-4337 account abstraction + ERC-7579 modular smart accounts |
| [Nethereum.Mud](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Mud) | | MUD autonomous worlds: table queries, store indexing, normalisation |
| [Nethereum.Uniswap](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Uniswap) | | Uniswap V2/V3/V4 + Permit2 |
| [Nethereum.X402](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.X402) | | HTTP 402 crypto payments (client + server middleware) |
| [Nethereum.GnosisSafe](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.GnosisSafe) | [![NuGet](https://badge.fury.io/nu/Nethereum.GnosisSafe.svg)](https://badge.fury.io/nu/Nethereum.GnosisSafe) | Gnosis Safe multi-sig integration |
| [Nethereum.Siwe](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Siwe) | [![NuGet](https://badge.fury.io/nu/Nethereum.Siwe.svg)](https://badge.fury.io/nu/Nethereum.Siwe) | Sign-In with Ethereum (EIP-4361) |


All packages: [nuget.org/profiles/nethereum](https://www.nuget.org/profiles/nethereum)

## Nethereum Playground

Try Nethereum directly in your browser — chain interaction, Ether transfers, ERC20/ERC721, ENS, SIWE, HD wallets, log processing, and more.

[![Nethereum Playground](https://raw.githubusercontent.com/Nethereum/Nethereum/master/screenshots/playground.png)](http://playground.nethereum.com)

## Templates

Get started quickly with `dotnet new` templates:

```
dotnet new install Nethereum.Templates.Pack
dotnet new install Nethereum.DevChain.Template
```

| Template | Short Name | Description |
|---|---|---|
| Smart Contract Library + ERC20 XUnit | `smartcontract` | Smart contract dev with auto code generation and integration tests |
| ERC721/ERC1155 Open Zeppelin | `nethereum-erc721-oz` | NFT and multi-token development with OpenZeppelin |
| Blazor MetaMask Wasm/Server | `nethereum-mm-blazor` | Blazor + MetaMask integration |
| Blazor SIWE Wasm/Server/REST | `nethereum-siwe` | Sign-In with Ethereum authentication |
| WebSocket Streaming | `nethereum-ws-stream` | Real-time blockchain data streaming |
| Aspire DevChain Environment | `nethereum-devchain` | Full dev environment: DevChain + PostgreSQL + Indexer + Explorer |

Sources: [SmartContractDefault](https://github.com/Nethereum/Nethereum.Templates.SmartContractDefault), [OZ-Erc721-Erc1155](https://github.com/Nethereum/Nethereum.Templates.SmartContracts.OZ-Erc721-Erc1155), [Metamask.Blazor](https://github.com/Nethereum/Nethereum.Templates.Metamask.Blazor), [SIWE](https://github.com/Nethereum/Nethereum.Templates.Siwe)

## Wallets & End-to-End Examples

### Blazor / MAUI Hybrid Explorer Wallet

A .NET Blazor Wasm SPA, Desktop (Windows/Mac), Android and iOS light blockchain explorer and wallet.

Source: [Nethereum-Explorer-Wallet-Template-Blazor](https://github.com/Nethereum/Nethereum-Explorer-Wallet-Template-Blazor) | Try it: [explorer.nethereum.com](https://explorer.nethereum.com)

### Desktop Wallet (Avalonia)

A reactive cross-platform desktop wallet using Nethereum, Avalonia, and ReactiveUI.

Source: [Nethereum.UI.Desktop](https://github.com/Nethereum/Nethereum.UI.Desktop)

## Unity

| Resource | Description |
|---|---|
| [Nethereum.Unity](https://github.com/Nethereum/Nethereum.Unity) | Unity package — install via git URL |
| [Unity3dSampleTemplate](https://github.com/Nethereum/Unity3dSampleTemplate) | Getting started: BlockNumber, Ether transfer, ERC20, MetaMask, cross-platform |
| [Nethereum.Unity.Webgl](https://github.com/Nethereum/Nethereum.Unity.Webgl) | WebGL + MetaMask: deploy ERC721 NFTs from Unity |

## Documentation & Community

- **Documentation**: [nethereum.readthedocs.io](https://nethereum.readthedocs.io/en/latest/)
- **Playground**: [playground.nethereum.com](http://playground.nethereum.com)
- **Discord**: [Join the community](https://discord.gg/u3Ej2BReNn) — technical support, chat, and collaboration
- **Full Component Catalog**: [COMPONENTS.md](https://github.com/Nethereum/Nethereum/blob/master/COMPONENTS.md)
