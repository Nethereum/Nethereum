# Nethereum.Parity.Reactive

**Reactive (`IObservable`) subscriptions over Parity's `parity_subscribe` pub/sub**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.Parity.Reactive.svg)](https://www.nuget.org/packages/Nethereum.Parity.Reactive/)

## Overview

`Nethereum.Parity.Reactive` contains one class, `ParityPubSubObservableSubscription<TResponse>`. It takes any `RpcRequest`, re-sends it wrapped in a `parity_subscribe` request over a streaming client, and surfaces the subscription's responses as Rx observables. Use it when you are connected to a Parity / OpenEthereum node through a streaming transport (for example a WebSocket client implementing `IStreamingClient`) and want `IObservable` results for `parity_subscribe`. For the non-reactive `parity_*` and `trace_*` calls use [Nethereum.Parity](../Nethereum.Parity/README.md); the other Rx streams live in `Nethereum.RPC.Reactive`, whose `RpcStreamingSubscriptionObservableHandler<T>` this class derives from.

## Installation

```bash
dotnet add package Nethereum.Parity.Reactive
```

Project references: `Nethereum.Parity` and `Nethereum.RPC.Reactive`; package reference `System.Reactive` 4.1.3. Target frameworks come from the build's `ReactiveFrameworks` property (net461, netstandard2.0, net6.0, net8.0, net9.0, net10.0 in the default build).

## ParityPubSubObservableSubscription

Namespace `Nethereum.Parity.Reactive`.

```csharp
public class ParityPubSubObservableSubscription<TResponse> : RpcStreamingSubscriptionObservableHandler<TResponse>
{
    public ParityPubSubObservableSubscription(IStreamingClient client)

    public Task SubscribeAsync(RpcRequest originalRequestToSubscribe, object id = null)
    public RpcRequest BuildRequest(RpcRequest originalRequestToSubscribe, object id = null)
}
```

- The constructor passes an `EthUnsubscribeRequestBuilder` to the base class, so unsubscribing sends the standard unsubscribe request, not `parity_unsubscribe`.
- `BuildRequest` delegates to `ParitySubscribeRequestBuilder` from `Nethereum.Parity`: the result is a `parity_subscribe` request whose parameters are the original method name followed by the original parameters. A null `id` becomes a new GUID string.
- `SubscribeAsync` builds that request and subscribes with it.

Inherited from `RpcStreamingSubscriptionObservableHandler<TResponse>` and `RpcStreamingSubscriptionHandler<TResponse>`:

```csharp
public IObservable<string> GetSubscribeResponseAsObservable()
public IObservable<TResponse> GetSubscriptionDataResponsesAsObservable()
public IObservable<bool> GetUnsubscribeResponseAsObservable()
public Task UnsubscribeAsync()
public string SubscriptionId { get; protected set; }
public SubscriptionState SubscriptionState { get; protected set; }
```

Subscribing and observing the results:

```csharp
var subscription = new ParityPubSubObservableSubscription<JToken>(streamingClient);

subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionIds.Add);
subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(result => received.Add(result));

var balanceRequest = new RpcRequest(1, "eth_getBalance", Address, "latest");
await subscription.SubscribeAsync(balanceRequest, "sub-1");
```

The streaming client receives one `parity_subscribe` request whose first parameter is `"eth_getBalance"`; when the node answers with the subscription id, `SubscriptionId` is set and the subscribe observable emits it; each later notification for that id is emitted by the data observable. Unsubscribing sends the standard `eth_unsubscribe` request:

```csharp
await subscription.UnsubscribeAsync();
```

Backed by `ParityPubSubObservableSubscriptionTests.ShouldSubscribeThroughParitySubscribeAndObserveTheResults` and `ShouldUnsubscribeWithTheStandardUnsubscribeRequest` (`DocSection.ClientExtensions`, `parity-reactive`) in `tests/Nethereum.Parity.IntegrationTests`. They use a fake `IStreamingClient` that answers the subscribe request with the id `0xsub1` and then pushes `parity_subscription` notifications, so no node is needed; `streamingClient` in a real application is a connected WebSocket (or other) `IStreamingClient`.
