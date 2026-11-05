# Nethereum.Parity

**`Web3Parity`: Nethereum's Web3 extended with the Parity / OpenEthereum `parity_*` and `trace_*` JSON-RPC methods**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.Parity.svg)](https://www.nuget.org/packages/Nethereum.Parity/)

## Overview

`Nethereum.Parity` is a thin client library. It contains one `Web3` subclass, `Web3Parity`, five service groups that expose typed request handlers for the Parity-specific RPC methods, and the request builders used for Parity's `parity_subscribe` / `parity_unsubscribe`. There is no node-side logic and no Parity-specific data model: results come back as `JObject`, `JArray`, `string`, `int` or `HexBigInteger`.

Use it only if you talk to a node that serves the `parity_*` or `trace_*` methods (Parity, OpenEthereum and compatible clients). For everything else keep using `Web3`; `Web3Parity` derives from `Web3.Web3`, so `web3.Eth`, `web3.Net` and the rest are unchanged. The source does not mark the package obsolete. The package description says: "Nethereum.Parity is the extended Web3 library for Parity, including the non-generic RPC API client methods."

For reactive `parity_subscribe` subscriptions see [Nethereum.Parity.Reactive](../Nethereum.Parity.Reactive/README.md).

## Installation

```bash
dotnet add package Nethereum.Parity
```

Depends on `Nethereum.RPC` and `Nethereum.Web3`.

## Web3Parity

```csharp
public interface IWeb3Parity : IWeb3
{
    IAccountsApiService Accounts { get; }
    IAdminApiService Admin { get; }
    IBlockAuthoringApiService BlockAuthoring { get; }
    INetworkApiService Network { get; }
    ITraceApiService Trace { get; }
}

public class Web3Parity : Web3.Web3, IWeb3Parity
{
    public Web3Parity(IClient client)
    public Web3Parity(string url = @"http://localhost:8545/", ILogger log = null, AuthenticationHeaderValue authenticationHeader = null)
    public Web3Parity(IAccount account, IClient client)
    public Web3Parity(IAccount account, string url = @"http://localhost:8545/", ILogger log = null, AuthenticationHeaderValue authenticationHeader = null)
}
```

The five properties are created when the `Web3` inner services are initialised.

```csharp
var web3 = new Web3Parity(client);

var versionInfo = await web3.Admin.VersionInfo.SendRequestAsync();
var netPort = await web3.Network.NetPort.SendRequestAsync();
var minGasPrice = await web3.BlockAuthoring.MinGasPrice.SendRequestAsync();
```

`versionInfo` is a `JObject` (`hash`, `track`, `version`), `netPort` an `int`, and `minGasPrice` a `HexBigInteger`. The three calls send `parity_versionInfo`, `parity_netPort` and `parity_minGasPrice`. Backed by `ParityClientRequestTests.ShouldCallParityMethodsThroughTheServiceGroups` (`DocSection.ClientExtensions`, `parity-client`, Order 1) in `tests/Nethereum.Parity.IntegrationTests/ParityClientRequestTests.cs`, which runs `Web3Parity` over a canned-response `IClient` and therefore needs no node.

## Service groups

Every handler exposes `SendRequestAsync(...)` and, for those with parameters, `BuildRequest(...)`; the parameterless handlers derive from `GenericRpcRequestResponseHandlerNoParam<T>` (in `Nethereum.RPC.Infrastructure`), which implements `IGenericRpcRequestResponseHandlerNoParam<T>`, whose only member is `SendRequestAsync(object id = null)`. `GenericRpcRequestResponseHandlerNoParam<T>` itself derives from `RpcRequestResponseHandlerNoParam<T>`, and `BuildRequest(object id = null)` comes from that class, so it is not reachable through the interface-typed properties of the service groups.

### Accounts (`IAccountsApiService`)

| Property | RPC method | Result |
|---|---|---|
| `AccountsInfo` | `parity_accountsInfo` | `JObject` |
| `DefaultAccount` | `parity_defaultAccount` | `string` |
| `GenerateSecretPhrase` | `parity_generateSecretPhrase` | `string` |
| `HardwareAccountsInfo` | `parity_hardwareAccountsInfo` | `JObject` |

### Admin (`IAdminApiService`)

| Property | RPC method | Result |
|---|---|---|
| `ParityListStorageKeys` | `parity_listStorageKeys` | `string[]` |
| `ConsensusCapability` | `parity_consensusCapability` | `string` |
| `ListOpenedVaults` | `parity_listOpenedVaults` | `string[]` |
| `ListVaults` | `parity_listVaults` | `string[]` |
| `LocalTransactions` | `parity_localTransactions` | `JObject` |
| `PendingTransactionsStats` | `parity_pendingTransactionsStats` | `JObject` |
| `ReleasesInfo` | `parity_releasesInfo` | `JObject` |
| `VersionInfo` | `parity_versionInfo` | `JObject` |

`IParityListStorageKeys` takes parameters:

```csharp
public interface IParityListStorageKeys
{
    Task<string[]> SendRequestAsync(string address, int quantity, string hash, object id = null);
    Task<string[]> SendRequestAsync(string address, int quantity, string hash, BlockParameter blockNumber, object id = null);
    RpcRequest BuildRequest(string address, int quantity, string hash, object id = null);
    RpcRequest BuildRequest(string address, int quantity, string hash, BlockParameter blockNumber, object id = null);
}
```

### Block authoring (`IBlockAuthoringApiService`)

| Property | RPC method | Result |
|---|---|---|
| `DefaultExtraData` | `parity_defaultExtraData` | `string` |
| `ExtraData` | `parity_extraData` | `string` |
| `GasCeilTarget` | `parity_gasCeilTarget` | `HexBigInteger` |
| `GasFloorTarget` | `parity_gasFloorTarget` | `HexBigInteger` |
| `MinGasPrice` | `parity_minGasPrice` | `HexBigInteger` |
| `TransactionsLimit` | `parity_transactionsLimit` | `int` |

### Network (`INetworkApiService`)

| Property | RPC method | Result |
|---|---|---|
| `ChainStatus` | `parity_chainStatus` | `JObject` |
| `GasPriceHistogram` | `parity_gasPriceHistogram` | `JObject` |
| `NetPeers` | `parity_netPeers` | `JObject` |
| `NetPort` | `parity_netPort` | `int` |
| `PendingTransactions` | `parity_pendingTransactions` | `JArray` |

### Trace (`ITraceApiService`)

```csharp
public interface ITraceBlock
{
    RpcRequest BuildRequest(HexBigInteger blockNumber, object id = null);
    Task<JArray> SendRequestAsync(HexBigInteger blockNumber, object id = null);
}

public interface ITraceCall
{
    RpcRequest BuildRequest(CallInput callInput, TraceType[] typeOfTrace, BlockParameter block, object id = null);
    Task<JObject> SendRequestAsync(CallInput callInput, TraceType[] typeOfTrace, BlockParameter block, object id = null);
}

public interface ITraceFilter
{
    RpcRequest BuildRequest(TraceFilterDTO traceFilter, object id = null);
    Task<JArray> SendRequestAsync(TraceFilterDTO traceFilter, object id = null);
}

public interface ITraceGet
{
    RpcRequest BuildRequest(string transactionHash, HexBigInteger[] index, object id = null);
    Task<JObject> SendRequestAsync(string transactionHash, HexBigInteger[] index, object id = null);
}

public interface ITraceRawTransaction
{
    RpcRequest BuildRequest(string rawTransaction, TraceType[] traceTypes, object id = null);
    Task<JObject> SendRequestAsync(string rawTransaction, TraceType[] traceTypes, object id = null);
}

public interface ITraceTransaction
{
    RpcRequest BuildRequest(string transactionHash, object id = null);
    Task<JArray> SendRequestAsync(string transactionHash, object id = null);
}

public enum TraceType { vmTrace, trace, stateDiff }

public static class TraceTypeExtensions
{
    public static string[] ConvertToStringArray(this TraceType[] value)
}
```

`ITraceApiService` exposes them as `TraceBlock`, `TraceCall`, `TraceFilter`, `TraceGet`, `TraceRawTransaction` and `TraceTransaction` (RPC methods `trace_block`, `trace_call`, `trace_filter`, `trace_get`, `trace_rawTransaction`, `trace_transaction`). `TraceFilterDTO` (`Nethereum.Parity.RPC.Trace.TraceDTOs`) has `FromBlock`, `ToBlock` (`BlockParameter`), `FromAddresses`, `ToAddress` (`string[]`), `Count` and `After` (`int`), serialised as `fromBlock`, `toBlock`, `fromAddress`, `toAddress`, `count`, `after`.

```csharp
var traces = await web3.Trace.TraceTransaction.SendRequestAsync(TransactionHash);

var callInput = new CallInput { To = "0x12890d2cce102216644c59daE5baed380d84830c" };
var request = web3.Trace.TraceCall.BuildRequest(
    callInput, new[] { TraceType.trace, TraceType.stateDiff }, BlockParameter.CreateLatest());
```

`traces` is a `JArray` of trace entries. `BuildRequest` creates the `trace_call` request without sending it; `TraceType` values are sent as the strings `"trace"` and `"stateDiff"`, converted by `TraceTypeExtensions.ConvertToStringArray` (each value's `ToString()`). Backed by `ParityClientRequestTests.ShouldTraceATransactionAndBuildATraceCallRequest` (`parity-client`, Order 2).

## Other types

- `Nethereum.Parity.RPC.ApiMethods` is an enum of Parity method names (`parity_*`, `trace_*`, `parity_subscribe`, `parity_unsubscribe`). It lists more methods than the package has handlers for; the handlers are the ones in the tables above plus `ParityDevLogs` (`parity_devLogs`, `string[]`) and `ParityDevLogsLevels` (`parity_devLogsLevels`, `string`), which are classes you construct with an `IClient` and are not attached to `Web3Parity`.
- `ParitySubscribeRequestBuilder` and `ParityUnsubscribeRequestBuilder` (`Nethereum.Parity.RPC.PubSub`):

```csharp
public class ParitySubscribeRequestBuilder : RpcRequestBuilder
{
    public RpcRequest BuildRequest(RpcRequest originalRequestToSubscribe, object id = null)
}

public class ParityUnsubscribeRequestBuilder : RpcRequestBuilder, IUnsubscribeSubscriptionRpcRequestBuilder
{
    public RpcRequest BuildRequest(string subscriptionHash, object id = null)
}
```

`ParitySubscribeRequestBuilder` wraps any request into a `parity_subscribe` request whose parameters are the original method name followed by the original parameters; a null `id` becomes a new GUID string. `ParityUnsubscribeRequestBuilder` builds `parity_unsubscribe` with the subscription hash, adding the `0x` prefix when missing.

```csharp
var original = new RpcRequest(1, "eth_getBalance", "0x12890d2cce102216644c59daE5baed380d84830c", "latest");

var subscribe = new ParitySubscribeRequestBuilder().BuildRequest(original, "sub-1");
var unsubscribe = new ParityUnsubscribeRequestBuilder().BuildRequest("abcd", "unsub-1");
```

Backed by `ParityClientRequestTests.ShouldWrapARequestInAParitySubscribeRequest` (`parity-client`, Order 3).

## Tests

`tests/Nethereum.Parity.IntegrationTests` also contains one `*Tester` class per RPC method (for example `TraceTransactionTester`), which run against a live Parity node configured in `test-settings.json` (`rpcUrl` `http://localhost:8545/`); they are not part of the examples above.
