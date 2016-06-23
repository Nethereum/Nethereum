# Nethereum

Nethereum is a .Net RPC / IPC Client library for Ethereum, it allows you to interact with Ethereum clients like geth using .Net. The library has very similar functionality as the Javascript Etherum Web3 RPC Client Library.

All the JSON RPC/IPC methods are implemented as they appear in new versions of the clients, closely supported the geth client inclusding its extensions for admin, personal, debugging.

Interaction with contracts has been simplified for deployment, function calling, transaction and event filtering and decoding of topics.


# Quick installation

Here is a list of all the nuget packages, if in doubt use Nethereum.Portable as it includes all the packages apart from IPC which is windows specific.

```
PM > Install-Package Nethereum.Portable -Pre
```

Another quick start option if targetting netstardad 1.2 is use the Web3 package, which the top level package that includes all the dependencies. 
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
