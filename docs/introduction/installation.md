## Quick installation

The simplest way to install Nethereum is to use Nuget, here is a list of all the nuget packages, if in doubt use Nethereum.Portable as it includes all the packages embeded in one. (Apart from IPC which is windows specific).

```
PM > Install-Package Nethereum.Portable -Pre
```

Another option (if targetting netstardad 1.1) is to use the Nethereum.Web3 package. This top level package include all the dependencies for RPC, ABI and Hex. 

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


Finally if you want to use IPC you will need the specific IPC Client library for Windows. 
Note: Named Pipe Windows is the only IPC supported and can only be used in combination with Nethereum.Web3 - Nethereum.RPC packages. So if you are planning to use IPC use the single packages