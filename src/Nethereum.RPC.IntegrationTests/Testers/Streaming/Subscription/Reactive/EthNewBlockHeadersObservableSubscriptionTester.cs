using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers.Streaming.Subscription.Reactive
{

    public class EthNewBlockHeadersObservableSubscriptionTester : StreamingRPCRequestTester
    {
        [Fact(DisplayName = "Subscription - Reactive - Should receive new block headers")]
        public override async Task ExecuteAsync()
        {
            using(var context = await CreateAndStartStreamingClientAsync())
            { 
                var receivedMessages = new ConcurrentBag<Block>();
                var subscription = new EthNewBlockHeadersObservableSubscription(context.StreamingClient);

                subscription
                    .GetSubscriptionDataResponsesAsObservable()
                    .Subscribe(block =>
                    {
                        receivedMessages.Add(block);
                        TryUnsubscribe(subscription);
                    });

                await subscription.SubscribeAsync(Guid.NewGuid().ToString());

                await WaitForFirstMessage(subscription, receivedMessages, TimeSpan.FromMinutes(1));

                Assert.NotEmpty(receivedMessages);
            }
        }
    }
}