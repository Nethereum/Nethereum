# Nethereum
 [![Documentation Status](https://readthedocs.org/projects/nethereum/badge/?version=latest)](https://nethereum.readthedocs.io/en/latest/) [![NuGet version](https://badge.fury.io/nu/nethereum.web3.svg)](https://badge.fury.io/nu/nethereum.web3)

Technical support, chat and collaboration at Discord: https://discord.gg/u3Ej2BReNn

# What is Nethereum ?

Nethereum is the .Net integration library for Ethereum, simplifying the access and smart contract interaction with Ethereum nodes both public like Geth (or your preferred client) L2 chains like Optimism, Arbitrum (or your preferred L2), any compatible EVM chain (Gnosis, etc) and permissioned chains like [Quorum](https://www.jpmorgan.com/global/Quorum).

Nethereum is developed targeting netstandard 1.1, netstandard 2.0, netcore 3.1, net451, .net 6, .net 8 and also as a portable library, hence it is compatible with all the operating systems (Windows, Linux, MacOS, Android and OSX) and has been tested on cloud, mobile, desktop, consoles and IoT.

# Nethereum Playground. Try Nethereum now in your browser.
Go to http://playground.nethereum.com to browse and execute all the different samples on how to use Nethereum directly in your browser. 

[![Nethereum Playground](screenshots/playground.png)](http://playground.nethereum.com)

# Do you need support, want to have a chat, or want to help?
Please join the Discord server using this link: https://discord.gg/u3Ej2BReNn
We should be able to answer there any simple queries, general comments or requests, everyone is welcome.
If you want to help or have any ideas for a pull request just come and chat.

## Documentation
The documentation and guides can be found at [Read the docs](https://nethereum.readthedocs.io/en/latest/). 

## Features

* JSON RPC / IPC Ethereum core methods.
* Geth management API (admin, personal, debugging, miner).
* [Parity](https://www.parity.io/) management API.
* [Quorum](https://www.jpmorgan.com/global/Quorum) integration.
* Simplified smart contract interaction for deployment, function calling, transaction and event filtering and decoding of topics.
* [Unity 3d](https://unity3d.com/) Unity integration.
* ABI to .Net type encoding and decoding, including attribute based for complex object deserialization.
* Hd Wallet
* External signers integration (Azure, AWS)
* Wallet integration (Metamask, WalletConnect)
* EVM Simulator
* Transaction, RLP and message signing, verification and recovery of accounts.
* Libraries for standard contracts Tokens, [ENS](https://ens.domains/), MUD (https://mud.dev/), Gnosis Safe
* Integrated TestRPC testing to simplify TDD and BDD (Specflow) development.
* Key storage using Web3 storage standard.
* Simplified account life cycle for both managed by third party client (personal) or stand alone (signed transactions).
* Low level Interception of RPC calls.
* Code generation of smart contracts services.

## Quick installation

Nethereum provides two types of packages. Standalone packages targeting Netstandard 1.1, net451 and where possible net351 to support Unity3d. There is also a Nethereum.Portable library which combines all the packages into a single portable library. As netstandard evolves and is more widely supported, the portable library might be eventually deprecated.

To install the latest version:

#### Windows/Mac/Linux users

```
dotnet add package Nethereum.Web3 
```

## Simple Code generation of Contract definitions
If you are working with smart contracts, you can quickly code generate contract definitions using the vscode solidity extension (please check the documentation for other options)

[![Code generation of Contract Definitions](https://github.com/juanfranblanco/vscode-solidity/raw/master/screenshots/compile-codegnerate-nethereum.png)](https://marketplace.visualstudio.com/items?itemName=JuanBlanco.solidity)

## Main Libraries

|  Project Source | Nuget_Package |  Description |
| ------------- |--------------------------|-----------|
| [Nethereum.Web3](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Web3)    | [![NuGet version](https://badge.fury.io/nu/nethereum.web3.svg)](https://badge.fury.io/nu/nethereum.web3)| Ethereum Web3 Class Library simplifying the interaction via RPC. Includes contract interaction, deployment, transaction, encoding / decoding and event filters |
| [Nethereum.Unity](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Unity) |  | Unity3d integration, libraries can be found in the Nethereum [releases](https://github.com/Nethereum/Nethereum/releases) |


## Core Libraries

|  Project Source | Nuget_Package |  Description |
| ------------- |--------------------------|-----------|
| [Nethereum.ABI](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.ABI) | [![NuGet version](https://badge.fury.io/nu/nethereum.abi.svg)](https://badge.fury.io/nu/nethereum.abi)| Encoding and decoding of ABI Types, functions, events of Ethereum contracts |
| [Nethereum.EVM](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.EVM) | |Ethereum Virtual Machine API|
| [Nethereum.Hex](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Hex) | [![NuGet version](https://badge.fury.io/nu/nethereum.hex.svg)](https://badge.fury.io/nu/nethereum.hex)| HexTypes for encoding and decoding String, BigInteger and different Hex helper functions|
| [Nethereum.RPC](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.RPC)   | [![NuGet version](https://badge.fury.io/nu/nethereum.rpc.svg)](https://badge.fury.io/nu/nethereum.rpc) | Core RPC Class Library to interact via RCP with an Ethereum client |
| [Nethereum.JsonRpc.Client](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.JsonRpc.Client)   | [![NuGet version](https://badge.fury.io/nu/nethereum.jsonrpc.client.svg)](https://badge.fury.io/nu/nethereum.jsonrpc.client) | Nethereum JsonRpc.Client core library to use in conjunction with either the JsonRpc.RpcClient, the JsonRpc.IpcClient or other custom Rpc provider |
| [Nethereum.JsonRpc.RpcClient](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.JsonRpc.RpcClient)   | [![NuGet version](https://badge.fury.io/nu/nethereum.jsonrpc.rpcclient.svg)](https://badge.fury.io/nu/nethereum.jsonrpc.rpcclient) | JsonRpc Rpc Client using Http|
| [Nethereum JsonRpc IpcClient](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.JsonRpc.IpcClient)| [![NuGet version](https://badge.fury.io/nu/nethereum.jsonRpc.ipcclient.svg)](https://badge.fury.io/nu/nethereum.jsonRpc.ipcclient) |JsonRpc IpcClient provider for Windows, Linux and Unix|
| [Nethereum.RLP](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.RLP)  | [![NuGet version](https://badge.fury.io/nu/nethereum.rlp.svg)](https://badge.fury.io/nu/nethereum.rlp) | RLP encoding and decoding |
| [Nethereum.KeyStore](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.KeyStore)  | [![NuGet version](https://badge.fury.io/nu/nethereum.keystore.svg)](https://badge.fury.io/nu/nethereum.keystore) | Keystore generation, encryption and decryption for Ethereum key files using the Web3 Secret Storage definition, https://github.com/ethereum/wiki/wiki/Web3-Secret-Storage-Definition |
| [Nethereum.Signer](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Signer)  | [![NuGet version](https://badge.fury.io/nu/nethereum.signer.svg)](https://badge.fury.io/nu/nethereum.signer) | Nethereum signer library to sign and verify messages, RLP and transactions using an Ethereum account private key |
| [Nethereum.Contracts](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Contracts)  | [![NuGet version](https://badge.fury.io/nu/nethereum.contracts.svg)](https://badge.fury.io/nu/nethereum.contracts) | Core library to interact via RPC with Smart contracts in Ethereum |
| [Nethereum.IntegrationTesting](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.IntegrationTesting)  |   | Integration testing module |
| [Nethereum.HDWallet](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.HDWallet)  | [![NuGet version](https://badge.fury.io/nu/nethereum.HDWallet.svg)](https://badge.fury.io/nu/nethereum.HDWallet) | Generates an HD tree of Ethereum compatible addresses from a randomly generated seed phrase (using BIP32 and BIP39) |

Note: IPC is supported for Windows, Unix and Linux but is only available using Nethereum.Web3 not Nethereum.Portable
 
## Smart contract API Libraries

|  Project Source | Nuget_Package |  Description |
| ------------- |--------------------------|-----------
| [Nethereum.ENS](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.ENS)| [![NuGet version](https://badge.fury.io/nu/nethereum.ens.svg)](https://badge.fury.io/nu/nethereum.ens)| Ethereum Name service library (original ENS) WIP to upgrade to latest ENS |

## Utilities

|  Project Source |  Description |
| ------------- |--------------------------|
| [Nethereum.Generator.Console](https://github.com/Nethereum/Nethereum/tree/master/generators/Nethereum.Generator.Console) |  |
| [Nethereum.Console](https://github.com/Nethereum/Nethereum.Console) | A collection of command line utilities to interact with Ethereum and account management | |
[Testchains](https://github.com/Nethereum/TestChains) | Pre-configured devchains for fast response (PoA) | | 
[DappHybrid](https://github.com/Nethereum/Nethereum.DappHybrid) | A cross-platform hybrid hosting mechanism for web based decentralised applications |
## Training modules

|  Project Source |  Description |
| ------------- |--------------------------|
|[Nethereum.Workbooks](https://github.com/Nethereum/Nethereum.Workbooks) | Xamarin Workbook tutorials including executable code | 
|[Nethereum.Tutorials](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Tutorials) | Tutorials to run on VS Studio |

## Code samples

|  Source |  Description |
| ------------- |------------|
[Keystore generator](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.KeyStore.Console.Sample)| Keystore file generator|
[Faucet](https://github.com/Nethereum/Nethereum.Faucet)| Web application template for an Ether faucet |
[Nethereum Flappy](https://github.com/Nethereum/Nethereum.Flappy)| The source code files for the Unity3d game integrating with Ethereum |
[Nethereum Game Sample](https://github.com/Nethereum/nethereum.game.sample)| Sample game demonstrating how to integrate Nethereum with [UrhoSharp's SamplyGame](https://github.com/xamarin/urho-samples/tree/master/SamplyGame) to build a cross-platform game interacting with Ethereum |
[Nethereum UI wallet sample](https://github.com/Nethereum/nethereum.UI.wallet.sample)| Cross platform wallet example using Nethereum, Xamarin.Forms and MvvmCross, targeting: Android, iOS, Windows Mobile, Desktop (windows 10 uwp), IoT with the Raspberry PI and Xbox. |
|[Nethereum Windows wallet sample](https://github.com/Nethereum/Nethereum.SimpleWindowsWallet) | Windows forms wallet sample providing the core functionality for Loading accounts from different mediums, Ether transfer, Standard token interaction. This is going to be the basis for the future cross-platform wallet / dapp |
[Nethereum Windows wallet sample](https://github.com/Nethereum/Nethereum.SimpleWindowsWallet) | Windows forms wallet sample providing the core functionality for Loading accounts from different mediums, Ether transfer, Standard token interaction. This is going to be the basis for the future cross-platform wallet / dapp |
[Blazor/Blockchain Explorer](https://github.com/Nethereum/NethereumBlazor) | Wasm blockchain explorer based on [Blazor](https://github.com/aspnet/Blazor) and [ReactiveUI](https://github.com/reactiveui/ReactiveUI)|

### Video guides
There are a few video guides, which might be helpful to get started. 
**NOTE: These videos are for version 1.0, so some areas have changed.**

Please use the Nethereum playground for the latest samples.

#### Introductions

These are two videos that can take you through all the initial steps from creating a contract to deployment, one in the classic windows, visual studio environment and another in a cross platform mac and visual studio code. 

##### Windows, Visual Studio
This video takes you through the steps of creating a smart contract, compiling it, starting a private chain and deploying it using Nethereum.

[![Smart contracts, private test chain and deployment to Ethereum with Nethereum](http://img.youtube.com/vi/4t5Z3eX59k4/0.jpg)](http://www.youtube.com/watch?v=4t5Z3eX59k4 "Smart contracts, private test chain and deployment to Ethereum with Nethereum")


#### Introduction to Calls, Transactions, Events, Filters and Topics

This hands on demo provides an introduction to calls, transactions, events filters and topics

[![Introduction to Calls, Transactions, Events, Filters and Topics](http://img.youtube.com/vi/Yir_nu5mmw8/0.jpg)](https://www.youtube.com/watch?v=Yir_nu5mmw8 "Introduction to Calls, Transactions, Events, Filters and Topics")
 
#### Mappings, Structs, Arrays and complex Functions Output (DTOs) 

This video provides an introduction on how to store and retrieve data from structs, mappings and arrays decoding multiple output parameters to Data Transfer Objects

[![Mappings, Structs, Arrays and complex Functions Output (DTOs)](http://img.youtube.com/vi/o8UC96K0rg8/0.jpg)](https://www.youtube.com/watch?v=o8UC96K0rg8 "Mappings, Structs, Arrays and complex Functions Output (DTOs)")


## Thanks and Credits

* Many thanks to Cass for the fantastic logo (https://github.com/c055) and recreating one of the @ethereumjs logo ideas for Nethereum https://github.com/ethereumjs/organization/issues/1
* Many thanks to everyone in Maker for providing very early feedback.
* Many thanks to everyone who has submitted a request for extra features, help or bugs either here in github, gitter/discord or other channels, you are continuously shaping this project. 
  A big shout out specially to @slothbag, @matt.tan, @knocte, @TrekDev, @raymens, @rickzanux, @naddison36, @bobsummerwill, @brendan87, @dylanmckendry that were using Nethereum and providing great feedback from the beginning.
  @djsowa Marcin Sowa for his help on IPC in Linux.
* Everyone in the Ethereum, Consensys and the blockchain community. 
* Huge shout out to everyone developing all the different Ethereum implementations Geth, Parity, EthereumJ, EthCpp, ethereum-js (and every other utility around it), python (in the different shapes), ruby (digix guys), solidity, vyper, serpent, web3 implementations (web3js the first) and ethjs, web3j, etc, etc and last but not least the .Net Bitcoin implementation.
* And many thanks to all you that keep helping and encouraging directly or indirectily.
\n## Documentation\n\nTo build the documentation site:\n\n```bash\nnpm install\nnpm run start\n```\n
