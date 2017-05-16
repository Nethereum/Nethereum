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

## Projects and samples
*

## Quick installation

Nethereum provides 

Here is a list of all the nuget packages, if in doubt use Nethereum.Portable as it includes all the packages embedded in one. (Apart from IPC which is windows specific).

```
PM > Install-Package Nethereum.Portable -Pre
```

Another option (if targeting netstardad 1.1) is to use the Nethereum.Web3 package. This top level package include all the dependencies for RPC, ABI and Hex. 

If you have issues installing the packages make sure you have a reference to System.Runtime specific to your environment.

```
PM > Install-Package Nethereum.Web3 -Pre
```

| Package       | Nuget         | 
| ------------- |:-------------:|
| Nethereum.Portable    | [![NuGet version](https://badge.fury.io/nu/nethereum.portable.svg)](https://badge.fury.io/nu/nethereum.portable)| 
| Nethereum.Web3    | [![NuGet version](https://badge.fury.io/nu/nethereum.web3.svg)](https://badge.fury.io/nu/nethereum.web3)|
| Nethereum.ABI    | [![NuGet version](https://badge.fury.io/nu/nethereum.abi.svg)](https://badge.fury.io/nu/nethereum.abi)| 
| Nethereum.RPC    | [![NuGet version](https://badge.fury.io/nu/nethereum.rpc.svg)](https://badge.fury.io/nu/nethereum.rpc)| 
| Nethereum.Hex    | [![NuGet version](https://badge.fury.io/nu/nethereum.hex.svg)](https://badge.fury.io/nu/nethereum.hex)| 
| Nethereum.JsonRpc.IpcClient| [![NuGet version](https://badge.fury.io/nu/nethereum.jsonRpc.ipcclient.svg)](https://badge.fury.io/nu/nethereum.jsonRpc.ipcclient)| 


Finally if you want to use IPC you will need the specific IPC Client library for Windows. 

Note: Named Pipe Windows is the only IPC supported and can only be used in combination with Nethereum.Web3 - Nethereum.RPC packages. So if you are planning to use IPC use the single packages