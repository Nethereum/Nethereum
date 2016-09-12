#Web3

Web3 provides a simple interaction wrapper to access the RPC methods provided by the Ethereum client categorised by their similar functionality.

It also provides a simplified way to interact with contracts by combining the ABI encoding / decoding of the input / output of the contracts together with the Eth RPC requests.

There are two types of RPC calls the Standard Ethereum JSON RPC

* Eth
* Net
* Shh

And the management RPC.

* Admin
* Personal
* Debug

The best way to learn about the different RPC methods provided is to use as a reference [the official Ethereum RPC API documentation](https://github.com/ethereum/wiki/wiki/JSON-RPC) or the [the official management api for geth](https://github.com/ethereum/go-ethereum/wiki/Management-APIs)
So we won't go into major detail here.

## Web3 constructor

Web3 accepts as a constructor either an url which will use as the default the RPC client, or an IClient which can be an IPC Client, or custom RPC Client.
 
The parameterless constructor uses the defaults address "http://localhost:8545/", which is the default port and used by the ethereum clients to accept RPC requests.

### Parameterless constructor

```csharp
    var web3 = new Nethereum.Web3.Web3();
```

### Url constructor

```csharp
    var web3 = new Nethereum.Web3.Web3("https://myclient.com:8545");
```
### IPC Client constructor

```csharp
    var ipcClient = new Nethereum.JsonRpc.IpcClient("./geth.ipc");
    var web3 = new Nethereum.Web3.Web3(ipcClient);
```
## Properties / methods overview

Web3 acting as the main interaction point offers 2 types of properties / methods RPCClientWrappers and access to core utility services.

### RPCClientWrappers
The RPC Client wrappers are just generic wrappers of specific functionality of the Ethereum client.

We currently have:

Eth, Net, Miner, Admin, Personal and DebugGeth. Eth, Net are as described before generic to the standard Eth and Miner, Admin, Personal and DebugGeth belong to the management RPC.

Eth it is subdivided in further wrappers to enable a simpler way to organise the different RPC calls.

```csharp
    web3.Eth.Transactions.GetTransactionReceipt;
    web3.Eth.Transactions.Call;
    web3.Eth.Transactions.EstimateGas;
    web3.Eth.Transactions.GetTransactionByBlockHashAndIndex;
    web3.Net.PeerCount;
    web3.Eth.GetBalance;
    web3.Eth.Mining.IsMining;
    web3.Eth.Accounts;
```
Each object is an RPC command which can be executed Async as:

```csharp
    await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
```

### Utilities
Web3 also provides several utilities to simplify the interaction.

#### Wei conversion

Wei conversion can be accessed though Convert

```csharp
   Convert.ToWei
   Convert.FromWei
```

Further example can be found on the [conversion unit tests](https://github.com/Nethereum/Nethereum/blob/master/src/Nethereum.Web3.Tests/ConversionTests.cs)

#### Offline transaction signing

"OfflineTransactionSigning" enables the signing of transactions, get the sender address or verify already signed transactions without interacting directly with the client.
This is very convenient as light clients may not be able to store the whole chain, but would prefer to use their privates keys to sign transactions and broadcast the signed raw transaction to the network.

```csharp
web3.OfflineTransactionSigning.SignTransaction
web3.OfflineTransactionSigning.GetSenderAddress
web3.OfflineTransactionSigning.VerifyTransaction
```

To provide offline transaction signing in Nethereum you can do the following:

First, you will need your private key, and sender address. You can retrieve the sender address from your private key using Nethereum.Core.Signing.Crypto.EthECKey.GetPublicAddress(privateKey); if you only have the private key.

```csharp
var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7"; 
var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
```

Now using web3 first you will need to retrieve the total number of transactions of your sender address.

```csharp
var web3 = new Web3(); var txCount = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(senderAddress);
```

The txCount will be used as the nonce to sign the transaction.

Now using web3 again, you can build an encoded transaction as following:

```csharp
var encoded = web3.OfflineTransactionSigning.SignTransaction(privateKey, receiveAddress, 10, txCount.Value);
```

If you need to include the data and gas there are overloads for it.

You can verify an encoded transaction: 

```csharp
Assert.True(web3.OfflineTransactionSigning.VerifyTransaction(encoded));
```

Or get the sender address from an encoded transaction:

```csharp
web3.OfflineTransactionSigning.GetSenderAddress(encoded);
```

To send the encoded transaction you will use the RPC method "SendRawTransaction"

```csharp
var txId = await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync("0x" + encoded);
```

The complete example can be found on the [Transactions signing unit tests](https://github.com/Nethereum/Nethereum/blob/master/src/Nethereum.Web3.Tests/TransactionSigningTests.cs)
or you can see a complete use case on the [Game sample](https://github.com/Nethereum/Nethereum.Game.Sample/) and it's service [Source code](https://github.com/Nethereum/Nethereum.Game.Sample/blob/master/Forms/Core/Ethereum/GameScoreService.cs)

#### Address checksum validation and formatting
There are also utilities to both validate and format addresses

An example of the expectations on the [encoding and decoding can be found on the address unit tests](https://github.com/Nethereum/Nethereum/blob/master/src/Nethereum.ABI.Tests/AddressEncodingTests.cs)

