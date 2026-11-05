using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.Parity.Reactive;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Parity.IntegrationTests
{
    public class ParityPubSubObservableSubscriptionTests
    {
        private const string Address = "0x12890d2cce102216644c59daE5baed380d84830c";

        private sealed class FakeStreamingClient : IStreamingClient
        {
            private readonly Dictionary<string, IRpcStreamingResponseHandler> _subscriptions =
                new Dictionary<string, IRpcStreamingResponseHandler>();

            public List<RpcRequest> Sent { get; } = new List<RpcRequest>();

            public bool IsStarted => true;

            public bool AddSubscription(string subscriptionId, IRpcStreamingResponseHandler handler)
            {
                _subscriptions[subscriptionId] = handler;
                return true;
            }

            public bool RemoveSubscription(string subscriptionId) => _subscriptions.Remove(subscriptionId);

            public Task SendRequestAsync(RpcRequest request, IRpcStreamingResponseHandler requestResponseHandler, string route = null)
            {
                Sent.Add(request);
                var json = request.Method == "parity_subscribe"
                    ? "{\"jsonrpc\":\"2.0\",\"id\":\"" + request.Id + "\",\"result\":\"0xsub1\"}"
                    : "{\"jsonrpc\":\"2.0\",\"id\":\"" + request.Id + "\",\"result\":true}";
                requestResponseHandler.HandleResponse(JsonConvert.DeserializeObject<RpcStreamingResponseMessage>(json));
                return Task.CompletedTask;
            }

            public void Push(string subscriptionId, string resultJson)
            {
                var json = "{\"jsonrpc\":\"2.0\",\"method\":\"parity_subscription\",\"params\":{\"subscription\":\"" +
                           subscriptionId + "\",\"result\":" + resultJson + "}}";
                _subscriptions[subscriptionId].HandleResponse(JsonConvert.DeserializeObject<RpcStreamingResponseMessage>(json));
            }

            public Task StartAsync() => Task.CompletedTask;

            public Task StopAsync() => Task.CompletedTask;
        }

        [Fact]
        [NethereumDocExample(DocSection.ClientExtensions, "parity-reactive", "Subscribe through parity_subscribe and observe the results", Order = 1)]
        public async Task ShouldSubscribeThroughParitySubscribeAndObserveTheResults()
        {
            var streamingClient = new FakeStreamingClient();
            var subscription = new ParityPubSubObservableSubscription<JToken>(streamingClient);
            var received = new List<JToken>();
            var subscriptionIds = new List<string>();

            subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionIds.Add);
            subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(result => received.Add(result));

            var balanceRequest = new RpcRequest(1, "eth_getBalance", Address, "latest");
            await subscription.SubscribeAsync(balanceRequest, "sub-1");

            streamingClient.Push("0xsub1", "\"0x10\"");
            streamingClient.Push("0xsub1", "\"0x20\"");

            var sent = Assert.Single(streamingClient.Sent);
            Assert.Equal("parity_subscribe", sent.Method);
            Assert.Equal("eth_getBalance", sent.RawParameters[0]);
            Assert.Equal("0xsub1", subscription.SubscriptionId);
            Assert.Equal(new[] { "0xsub1" }, subscriptionIds);
            Assert.Equal(new[] { "0x10", "0x20" }, new[] { (string)received[0], (string)received[1] });
        }

        [Fact]
        [NethereumDocExample(DocSection.ClientExtensions, "parity-reactive", "Unsubscribe sends the standard unsubscribe request", Order = 2)]
        public async Task ShouldUnsubscribeWithTheStandardUnsubscribeRequest()
        {
            var streamingClient = new FakeStreamingClient();
            var subscription = new ParityPubSubObservableSubscription<JToken>(streamingClient);
            var unsubscribed = new List<bool>();
            subscription.GetUnsubscribeResponseAsObservable().Subscribe(unsubscribed.Add);

            await subscription.SubscribeAsync(new RpcRequest(1, "eth_getBalance", Address, "latest"));
            await subscription.UnsubscribeAsync();

            Assert.Equal("eth_unsubscribe", streamingClient.Sent[1].Method);
            Assert.Equal(new[] { true }, unsubscribed);
        }
    }
}
