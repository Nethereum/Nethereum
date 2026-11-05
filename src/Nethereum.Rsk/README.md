# Nethereum.Rsk

**`Web3Rsk`: Nethereum's Web3 with Rsk's extended block DTOs (the `minimumGasPrice` field) and RSKIP-60 chain-id checksum addresses**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.Rsk.svg)](https://www.nuget.org/packages/Nethereum.Rsk/)

## Overview

`Nethereum.Rsk` is a small extension package for the Rsk chain (a Bitcoin-secured EVM chain). It adds exactly two things to the standard Nethereum stack:

1. `Web3Rsk`, a `Web3` subclass with a `RskEth` service whose four block queries return block DTOs that carry Rsk's extra `minimumGasPrice` field.
2. `AddressUtil`, which converts and validates Rsk checksum addresses, where the checksum depends on the chain id.

Everything else (sending transactions, contracts, ERC-20 and so on) is the normal `Web3` API: `Web3Rsk` derives from `Web3.Web3`. The package description says "the extended Web3 library for Rsk, including the non-generic RPC API client methods to interact with the Rsk chain".

## Installation

```bash
dotnet add package Nethereum.Rsk
```

Depends on `Nethereum.RPC` and `Nethereum.Web3`.

## Web3Rsk and RskEth

```csharp
public class Web3Rsk : Web3.Web3
{
    public Web3Rsk(IClient client)
    public Web3Rsk(string url = @"http://localhost:8545/", ILogger log = null, AuthenticationHeaderValue authenticationHeader = null)
    public Web3Rsk(IAccount account, IClient client)
    public Web3Rsk(IAccount account, string url = @"http://localhost:8545/", ILogger log = null, AuthenticationHeaderValue authenticationHeader = null)

    public IRskEthApiService RskEth { get; private set; }
}

public interface IRskEthApiService
{
    IRskEthGetBlockWithTransactionsByHash GetBlockWithTransactionsByHash { get; }
    IRskEthGetBlockWithTransactionsByNumber GetBlockWithTransactionsByNumber { get; }
    IRskEthGetBlockWithTransactionsHashesByHash GetBlockWithTransactionsHashesByHash { get; }
    IRskEthGetBlockWithTransactionsHashesByNumber GetBlockWithTransactionsHashesByNumber { get; }
}
```

The four handlers send the ordinary `eth_getBlockByHash` and `eth_getBlockByNumber` requests; the "WithTransactions" ones pass `true` for full transactions and the "WithTransactionsHashes" ones pass `false`. What differs from `web3.Eth.Blocks` is the result type:

```csharp
public interface IRskEthGetBlockWithTransactionsByHash
{
    Task<RskBlockWithTransactions> SendRequestAsync(string blockHash, object id = null);
    RpcRequest BuildRequest(string blockHash, object id = null);
}

public interface IRskEthGetBlockWithTransactionsHashesByNumber
{
    Task<RskBlockWithTransactionHashes> SendRequestAsync(HexBigInteger number, object id = null);
    Task<RskBlockWithTransactionHashes> SendRequestAsync(BlockParameter blockParameter, object id = null);
    RpcRequest BuildRequest(HexBigInteger number, object id = null);
    RpcRequest BuildRequest(BlockParameter blockParameter, object id = null);
}
```

`IRskEthGetBlockWithTransactionsByNumber` has the same four members as `...HashesByNumber` but returns `RskBlockWithTransactions`; `IRskEthGetBlockWithTransactionsHashesByHash` has the `string blockHash` members of `...ByHash` but returns `RskBlockWithTransactionHashes`. In the concrete handlers, a block hash gets its `0x` prefix added if missing, and a null argument throws `ArgumentNullException` in `RskEthGetBlockWithTransactionsByNumber`, `RskEthGetBlockWithTransactionsHashesByHash` and `RskEthGetBlockWithTransactionsHashesByNumber`. `RskEthGetBlockWithTransactionsByHash` has no null check in either `SendRequestAsync` or `BuildRequest`: a null `blockHash` is passed through `EnsureHexPrefix` (which returns null for null) and sent as a null request parameter. Backed by `RskDocExampleTests.ShouldThrowForANullHashInThreeHandlersAndPassItThroughInTheWithTransactionsByHashHandler` (`rsk-client`, Order 3).

The concrete classes, all taking the `IClient` in their constructor and deriving from `RpcRequestResponseHandler<T>` with the method name `eth_getBlockByHash` or `eth_getBlockByNumber`, and the `RskEthApiService` that `Web3Rsk` creates:

```csharp
public class RskEthApiService : RpcClientWrapper, IRskEthApiService
{
    public RskEthApiService(IClient client)
    public IRskEthGetBlockWithTransactionsByHash GetBlockWithTransactionsByHash { get; private set; }
    public IRskEthGetBlockWithTransactionsByNumber GetBlockWithTransactionsByNumber { get; private set; }
    public IRskEthGetBlockWithTransactionsHashesByHash GetBlockWithTransactionsHashesByHash { get; private set; }
    public IRskEthGetBlockWithTransactionsHashesByNumber GetBlockWithTransactionsHashesByNumber { get; private set; }
}

public class RskEthGetBlockWithTransactionsByHash : RpcRequestResponseHandler<RskBlockWithTransactions>, IRskEthGetBlockWithTransactionsByHash
{
    public RskEthGetBlockWithTransactionsByHash(IClient client)
    public Task<RskBlockWithTransactions> SendRequestAsync(string blockHash, object id = null)
    public RpcRequest BuildRequest(string blockHash, object id = null)
}

public class RskEthGetBlockWithTransactionsByNumber : RpcRequestResponseHandler<RskBlockWithTransactions>, IRskEthGetBlockWithTransactionsByNumber
{
    public RskEthGetBlockWithTransactionsByNumber(IClient client)
    public Task<RskBlockWithTransactions> SendRequestAsync(BlockParameter blockParameter, object id = null)
    public Task<RskBlockWithTransactions> SendRequestAsync(HexBigInteger number, object id = null)
    public RpcRequest BuildRequest(HexBigInteger number, object id = null)
    public RpcRequest BuildRequest(BlockParameter blockParameter, object id = null)
}

public class RskEthGetBlockWithTransactionsHashesByHash : RpcRequestResponseHandler<RskBlockWithTransactionHashes>, IRskEthGetBlockWithTransactionsHashesByHash
{
    public RskEthGetBlockWithTransactionsHashesByHash(IClient client)
    public Task<RskBlockWithTransactionHashes> SendRequestAsync(string blockHash, object id = null)
    public RpcRequest BuildRequest(string blockHash, object id = null)
}

public class RskEthGetBlockWithTransactionsHashesByNumber : RpcRequestResponseHandler<RskBlockWithTransactionHashes>, IRskEthGetBlockWithTransactionsHashesByNumber
{
    public RskEthGetBlockWithTransactionsHashesByNumber(IClient client)
    public Task<RskBlockWithTransactionHashes> SendRequestAsync(HexBigInteger number, object id = null)
    public Task<RskBlockWithTransactionHashes> SendRequestAsync(BlockParameter blockParameter, object id = null)
    public RpcRequest BuildRequest(HexBigInteger number, object id = null)
    public RpcRequest BuildRequest(BlockParameter blockParameter, object id = null)
}
```

### Block DTOs

Namespace `Nethereum.Rsk.RPC.RskEth.DTOs`.

```csharp
public interface IRskBlockExtended
{
    string MinimumGasPriceString { get; set; }
}

public class RskBlockWithTransactionHashes : BlockWithTransactionHashes, IRskBlockExtended
{
    [JsonProperty(PropertyName = "minimumGasPrice")]
    public string MinimumGasPriceString { get; set; }
}

public class RskBlockWithTransactions : BlockWithTransactions, IRskBlockExtended
{
    [JsonProperty(PropertyName = "minimumGasPrice")]
    public string MinimumGasPriceString { get; set; }
}

public static class RskBlockExtendedExtensions
{
    public static BigInteger GetMinimumGasPriceAsBigInteger(this IRskBlockExtended rskBlock)
}
```

`GetMinimumGasPriceAsBigInteger` returns 0 for an empty `MinimumGasPriceString`, otherwise the parsed hex quantity.

```csharp
var web3 = new Web3Rsk(client);

var block = await web3.RskEth.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new HexBigInteger("0x143828"));
var request = web3.RskEth.GetBlockWithTransactionsByNumber.BuildRequest(BlockParameter.CreateLatest());

var minimumGasPriceHex = block.MinimumGasPriceString;
var minimumGasPrice = block.GetMinimumGasPriceAsBigInteger();
```

For a response with `"minimumGasPrice":"0x387ee40"`, `minimumGasPrice` is `59240000`; `request` is an `eth_getBlockByNumber` request whose second parameter is `true`. Backed by `RskDocExampleTests.ShouldReadTheRskMinimumGasPriceFromABlock` (`DocSection.ClientExtensions`, `rsk-client`, Order 1) in `tests/Nethereum.Rsk.IntegrationTests/RskDocExampleTests.cs`, which runs `Web3Rsk` over a canned-response `IClient` and needs no node. The same project's `GetBlockIntegrationTests` run the four queries against `https://public-node.rsk.co` and are marked `[Trait("Category", "LiveNetwork")]`.

## Checksum addresses: AddressUtil

Namespace `Nethereum.Rsk.Util`.

```csharp
public class AddressUtil
{
    public static AddressUtil Current { get; }

    public string ConvertToChecksumAddress(string address, BigInteger? chainId = null)
    public bool IsChecksumAddress(string address, BigInteger? chainId)
}
```

With a `chainId`, the Keccak-256 hash that decides which letters are upper case is computed over `"{chainId}0x" + lower-case address`; with `null` it is the plain EIP-55 form. So the same address has a different checksum for Rsk mainnet (chain id 30) than for the plain form.

```csharp
var checksum = AddressUtil.Current.ConvertToChecksumAddress("0x5aaEB6053f3e94c9b9a09f33669435E7ef1bEAED", 30);
var validOnRskMainnet = AddressUtil.Current.IsChecksumAddress("0x5aaEB6053f3e94c9b9a09f33669435E7ef1bEAeD", 30);
var validForOtherChain = AddressUtil.Current.IsChecksumAddress("0x5aaEB6053f3e94c9b9a09f33669435E7ef1bEAeD", 1234);
var eip55 = AddressUtil.Current.ConvertToChecksumAddress("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed");
```

`checksum` is `0x5aaEB6053f3e94c9b9a09f33669435E7ef1bEAeD`, `validOnRskMainnet` is `true`, `validForOtherChain` is `false`, and `eip55` is `0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed`. Backed by `RskDocExampleTests.ShouldConvertAndValidateRskChecksumAddresses` (`rsk-client`, Order 2); the existing `UtilChecksumTest` theories in the same project cover more addresses.
