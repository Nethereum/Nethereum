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



Finally if you want to use IPC you will need the specific IPC Client library for Windows. 

Note: Named Pipe Windows is the only IPC supported and can only be used in combination with Nethereum.Web3 - Nethereum.RPC packages. So if you are planning to use IPC use the single packages