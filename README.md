# Nethereum
[![Join the chat at https://gitter.im/juanfranblanco/Ethereum.RPC](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/juanfranblanco/Ethereum.RPC?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge) [![Documentation Status](https://readthedocs.org/projects/nethereum/badge/?version=latest)](https://nethereum.readthedocs.io/en/latest/) [![NuGet version](https://badge.fury.io/nu/nethereum.web3.svg)](https://badge.fury.io/nu/nethereum.web3)

Nethereum is the .Net integration library for Ethereum, it allows you to interact with Ethereum clients like geth, eth or parity using RPC. 

The library has very similar functionality as the Javascript Etherum Web3 RPC Client Library.

All the JSON RPC/IPC methods are implemented as they appear in new versions of the clients. 

The geth client is the one that is closely supported and tested, including its management extensions for admin, personal, debugging, miner.

Interaction with contracts has been simplified for deployment, function calling, transaction and event filtering and decoding of topics.

The library has been tested in all the platforms .Net Core, Mono, Linux, iOS, Android, Raspberry PI, Xbox and of course Windows.

## Issues, Requests and help

Please join the chat at:  [![Join the chat at https://gitter.im/juanfranblanco/Ethereum.RPC](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/juanfranblanco/Ethereum.RPC?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

We should be able to answer there any simple queries, general comments or requests, everyone is welcome. In a similar feel free to raise any issue or pull request.

## Quick installation

Here is a list of all the nuget packages, if in doubt use Nethereum.Portable as it includes all the packages embedded in one. (Apart from IPC which is windows specific).

```
PM > Install-Package Nethereum.Portable -Pre
```

Another option (if targeting netstandard 1.1) is to use the Nethereum.Web3 package. This top level package include all the dependencies for RPC, ABI and Hex. 

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

## Documentation
The documentation and guides are now in [Read the docs](https://nethereum.readthedocs.io/en/latest/). 

### Video guides
There a few video guides, which might be helpful to get started.

The code for these tutorials can be found [here](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Tutorials)

#### Introductions

These are two videos that can take you through all the initial steps from creating a contract to deployment, one in the classic windows, visual studio environment and another in a cross platform mac and visual studio code.

##### Windows, Visual Studio, .Net 451 Video
This video takes you through the steps of creating the a smart contract, compile it, start a private chain and deploy it using Nethereum.

[![Smart contracts, private test chain and deployment to Ethereum with Nethereum](http://img.youtube.com/vi/4t5Z3eX59k4/0.jpg)](http://www.youtube.com/watch?v=4t5Z3eX59k4 "Smart contracts, private test chain and deployment to Ethereum with Nethereum")

##### Cross platform, Visual Studio Code, .Net core Video

If you want to develop in a cross platform environment this video takes you through same steps as the Windows .Net 451 step by step guide but this time in a Mac using Visual Studio Code and .Net Core.

[![Cross platform development in Ethereum using .Net Core and VsCode and Nethereum](http://img.youtube.com/vi/M1qKcJyQcMY/0.jpg)](http://www.youtube.com/watch?v=M1qKcJyQcMY "Cross platform development in Ethereum using .Net Core and VsCode and Nethereum")


#### Introduction to Calls, Transactions, Events, Filters and Topics

This hands on demo provides an introduction to calls, transactions, events filters and topics

[![Introduction to Calls, Transactions, Events, Filters and Topics](http://img.youtube.com/vi/Yir_nu5mmw8/0.jpg)](https://www.youtube.com/watch?v=Yir_nu5mmw8 "Introduction to Calls, Transactions, Events, Filters and Topics")
 
#### Mappings, Structs, Arrays and complex Functions Output (DTOs) 

This video provides an introduction on how to store and retrieve data from structs, mappings and arrays decoding multiple output parameters to Data Transfer Objects

[![Mappings, Structs, Arrays and complex Functions Output (DTOs)](http://img.youtube.com/vi/o8UC96K0rg8/0.jpg)](https://www.youtube.com/watch?v=o8UC96K0rg8 "Mappings, Structs, Arrays and complex Functions Output (DTOs)")

## Sample Projects

### Nethereum Wallet

This is a cross platform wallet example using Nethereum, Xamarin.Forms and MvvmCross, targeting all main mobile platforms Android, iOS, Windows, Desktop (Windows 10 uwp), IoT with the Raspberry PI and Xbox.

[Source Code](https://github.com/Nethereum/Nethereum.UI.Wallet.Sample)

### Nethereum Web 

This example demonstrates using Nethereum on a web project

[Source Code](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Web.Sample)

### Nethereum Game

This sample game demonstrates how to integrate Nethereum with the UrhoSharp SamplyGame (game sample) to create a cross-platform game that interacts with Ethereum.

A simple smart contract deployed to Ethereum allows you to tracks the users high scores and the top scores. The sample uses private keys to sign "offline" transactions and use a public node for all the communication, this way avoids the local installation of an ethereum client.

[Source Code](https://github.com/Nethereum/Nethereum.Game.Sample)

#### Small video tutorial
This video gives you a quick introduction on the sample:

* Quick overview of UrhoSharp and cross-platform development
* Overview of public nodes like infura.io
* Overview of private keys
* Overview of the solution structure
* Overview of the smart contract, .net service, offline transaction signing.

[![Cross platform game development with Ethereum using UrhoSharp and .Net](http://img.youtube.com/vi/WtpmCmP11Iw/0.jpg)](https://www.youtube.com/watch?v=WtpmCmP11Iw "Cross platform game development with Ethereum using UrhoSharp and .Net")

### Nethereum Hololens
Very simple sample to demonstrate integration with Unity3d, UWP and Hololens.

Unity3d does not support (yet) the latest version of .net for scripting, but this is not an issue with UWP.

This project demonstrates how to enable this by using the [Origami sample](https://developer.microsoft.com/en-us/windows/holographic/holograms_101e) from the 101e on Holograms by Microsoft,
and adding an script in which we will get the latest blockNumber.

##Video sample
This video provides a quick overview of the structure, #pragmas and dlls necessary on the plugins folder.
[![Hololens, Unity3d and Nethereum](http://img.youtube.com/vi/2MXAWHTRw7E/0.jpg)](https://www.youtube.com/watch?v=2MXAWHTRw7E "Hololens, Unity3d and Nethereum")



## Thanks and Credits

* Many thanks to Cass for the fantastic logo (https://github.com/cassiopaia)
* Many thanks to everyone who has submitted a request for extra features, help or bugs either here in github, gitter or other channels, you are continuously shaping this project. 
  A big shout out specially to @slothbag, @matt.tan, @knocte, @TrekDev, @raymens, @rickzanux, @naddison36, @bobsummerwill, @brendan87, @dylanmckendry.
* Of course everyone at Ujo, Consensys and Ethereum
* Huge thanks to Ethan Celleti from edjCase for his great support on his JsonRpc library and NBitcoin for his implementation of the Elliptic Curve in .Net
