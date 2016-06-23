# Nethereum

Nethereum is a .Net RPC / IPC Client library for Ethereum, it allows you to interact with Ethereum clients like geth using .Net. The library has very similar functionality as the Javascript Etherum Web3 RPC Client Library.

All the JSON RPC/IPC methods are implemented as they appear in new versions of the clients. The geth client is the one that is closely supported and tested, including its management extensions for admin, personal, debugging, miner.

Interaction with contracts has been simplified for deployment, function calling, transaction and event filtering and decoding of topics.

The library has been tested in all the platforms .Net Core, Mono, Linux, iOS, Android, Raspberry PI, Xbox and of course Windows.

## Quick installation

Here is a list of all the nuget packages, if in doubt use Nethereum.Portable as it includes all the packages apart from IPC which is windows specific.

```
PM > Install-Package Nethereum.Portable -Pre
```

Another option (if targetting netstardad 1.2) is to use the Nethereum.Web3 package. This top level package include all the dependencies for RPC, ABI and Hex. 

If you have issues intalling the packages make sure you have a reference to System.Runtime specific to your environment.

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


Finally if you want to use IPC you will need the specific IPC Client library for Windows. Note: Currently only Windows is supported.