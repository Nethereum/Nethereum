# Nethereum

Nethereum is the .Net integration library for Ethereum, simplifying the access and smart contract interaction with Ethereum nodes both public or permissioned like Geth, Parity or Quorum. 

Nethereum is developed targetting netstandard 1.1, net451 and also as a portable library, hence it is compabitable with all the operating systems (Windows, Linux, MacOS, Android and OSX) and has been tested on cloud, mobile, desktop, xbox, hololens and windows IoT. 

## Features

* JSON RPC / IPC Ethereum core methods
* Geth management api (admin, personal, debugging, miner)
* Parity managment api (WIP)
* Quorum
* Simplified smart contract interaction for deployment, function calling, transaction and event filtering and decoding of topics.
* ABI to .Net type encoding and decoding, including attribute based for complex object deserialisation.
* Transaction, RLP and message signing, verification and recovery of accounts
* Libraries for standard contracts Token, ENS and Uport
* Integrated TestRPC testing to simplify TDD and BDD (Specflow) development
* Key storage using Web3 storage standard, compatible with Geth and Parity.
* Simplified account lifecycle for both managed by third party client (personal) or stand alone (signed transactions)
* Low level Interception of RPC calls.
* Code generation of smart contracts services.


## Quick installation

Nethereum provides two types of packages. Standalone packages targetting Netstandard 1.1, net451 and where possible net350 and the Nethereum.Portable library which combines all the packages into one as a portable library. As netstandard evolves and is more widely supported the portable library might be eventually deprecated, as it won't be longer needed.

To install the latest version you can either:

```
PM > Install-Package Nethereum.Portable -Pre
```
or

```
PM > Install-Package Nethereum.Web3 -Pre
```

## Main Libraries
|  Project Source | Nuget_Package |  Description |
| ------------- |--------------------------|-----------|
| Nethereum.Portable    | [![NuGet version](https://badge.fury.io/nu/nethereum.portable.svg)](https://badge.fury.io/nu/nethereum.portable)| Portable class library combining all the different libraries in one package |
| [Nethereum.Web3](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Web3)    | [![NuGet version](https://badge.fury.io/nu/nethereum.web3.svg)](https://badge.fury.io/nu/nethereum.web3)| Ethereum Web3 Class Library simplifying the interaction via RPC includes contract interaction, deployment, transaction, encoding / decoding and event filters |
| [Nethereum.Geth](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Geth)    | [![NuGet version](https://badge.fury.io/nu/nethereum.geth.svg)](https://badge.fury.io/nu/nethereum.geth)| Nethereum.Geth is the extended Web3 library for Geth. This includes the non-generic RPC API client methods to interact with the Go Ethereum Client (Geth) like Admin, Debug, Miner|  
| [Nethereum.Quorum](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Quorum)| [![NuGet version](https://badge.fury.io/nu/nethereum.quorum.svg)](https://badge.fury.io/nu/nethereum.quorum)| Extension to interact with Quorum, the permissioned implementation of Ethereum supporting data privacy created by JP Morgan|
| [Nethereum.Parity](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Parity)| [![NuGet version](https://badge.fury.io/nu/nethereum.parity.svg)](https://badge.fury.io/nu/nethereum.parity)| Netherum.Parity is the extended Web3 library for Parity. including the non-generic RPC API client methods to interact with Parity. (WIP)|


## Core Libraries
|  Project Source | Nuget_Package |  Description |
| ------------- |--------------------------|-----------|
| [Nethereum.ABI](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.ABI) | [![NuGet version](https://badge.fury.io/nu/nethereum.abi.svg)](https://badge.fury.io/nu/nethereum.abi)| Encoding and decoding of ABI Types, functions, events of Ethereum contracts |
| [Nethereum.Hex](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Hex) | [![NuGet version](https://badge.fury.io/nu/nethereum.hex.svg)](https://badge.fury.io/nu/nethereum.hex)| HexTypes for encoding and encoding String, BigInteger and different Hex helper functions|
| [Nethereum.RPC](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.RPC)   | [![NuGet version](https://badge.fury.io/nu/nethereum.rpc.svg)](https://badge.fury.io/nu/nethereum.rpc) | Core RPC Class Library to interact via RCP with an Ethereum client |
| [Nethereum.JsonRpc.Client](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.JsonRpc.Client)   | [![NuGet version](https://badge.fury.io/nu/nethereum.jsonrpc.client.svg)](https://badge.fury.io/nu/nethereum.jsonrpc.client) | Nethereum JsonRpc.Client core library to use in conjunction with either the JsonRpc.RpcClient, the JsonRpc.IpcClient or other custom Rpc provider |
| [Nethereum.JsonRpc.RpcClient](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.JsonRpc.RpcClient)   | [![NuGet version](https://badge.fury.io/nu/nethereum.jsonrpc.rpcclient.svg)](https://badge.fury.io/nu/nethereum.jsonrpc.rpcclient) | JsonRpc Rpc Client provider using Edjcase.JsonRpc.Client |
| [Nethereum JsonRpc IpcClient](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.JsonRpc.IpcClient)| [![NuGet version](https://badge.fury.io/nu/nethereum.jsonRpc.ipcclient.svg)](https://badge.fury.io/nu/nethereum.jsonRpc.ipcclient) |JsonRpc IpcClient provider for Windows|
| [Nethereum.RLP](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.RLP)  | [![NuGet version](https://badge.fury.io/nu/nethereum.rlp.svg)](https://badge.fury.io/nu/nethereum.rlp) | RLP encoding and decoding |
| [Nethereum.KeyStore](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.KeyStore)  | [![NuGet version](https://badge.fury.io/nu/nethereum.keystore.svg)](https://badge.fury.io/nu/nethereum.keystore) | Keystore generation, encryption and decryption for Ethereum key files using the Web3 Secret Storage definition, https://github.com/ethereum/wiki/wiki/Web3-Secret-Storage-Definition |
| [Nethereum.Signer](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Signer)  | [![NuGet version](https://badge.fury.io/nu/nethereum.signer.svg)](https://badge.fury.io/nu/nethereum.signer) | Nethereum signer library to sign and verify messages, RLP and transactions using an Ethereum account private key |
| [Nethereum.Contracts](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Contracts)  | [![NuGet version](https://badge.fury.io/nu/nethereum.contracts.svg)](https://badge.fury.io/nu/nethereum.contracts) | Core library to interact via RPC with Smart contracts in Ethereum |


## Smart contract api Libraries
|  Project Source | Nuget_Package |  Description |
| ------------- |--------------------------|-----------|
| [Nethereum.StandardTokenEIP20](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.StandardTokenEIP20)| [![NuGet version](https://badge.fury.io/nu/nethereum.standardtokeneip20.svg)](https://badge.fury.io/nu/nethereum.nethereum.standardtokeneip20)| Nethereum.StandardTokenEIP20 Ethereum Service to interact with ERC20 compliant contracts |
| [Nethereum.Uport](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Uport)| [![NuGet version](https://badge.fury.io/nu/nethereum.uport.svg)](https://badge.fury.io/nu/nethereum.uport)| Uport registry library |
| [Nethereum.ENS](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.ENS)| [![NuGet version](https://badge.fury.io/nu/nethereum.ens.svg)](https://badge.fury.io/nu/nethereum.ens)| Ethereum Name service library (original ENS) WIP to upgrade to latest ENS |


Note: Named Pipe Windows is the only IPC api supported, this can only be used in combination with Nethereum.Web3 - Nethereum.RPC packages. Not the Nethereum.Portable package.

