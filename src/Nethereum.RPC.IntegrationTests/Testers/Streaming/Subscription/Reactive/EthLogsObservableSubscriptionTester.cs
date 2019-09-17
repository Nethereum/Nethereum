using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers.Streaming.Subscription.Reactive
{
    public class EthLogsObservableSubscriptionTester : StreamingRPCRequestTester
    {
        [Fact(DisplayName = "Subscription - Reactive - Should receive new logs")]
        public override async Task ExecuteAsync()
        {
            using(var context = await CreateAndStartStreamingClientAsync())
            { 

                var receivedMessages = new ConcurrentBag<FilterLog>();
                var subscription = new EthLogsObservableSubscription(
                    context.StreamingClient);

                subscription
                    .GetSubscriptionDataResponsesAsObservable()
                    .Subscribe(log =>
                    {
                        receivedMessages.Add(log);
                        TryUnsubscribe(subscription);
                    });

                await subscription.SubscribeAsync(Guid.NewGuid().ToString());

                await WaitForFirstMessage(subscription, receivedMessages, TimeSpan.FromMinutes(1));

                Assert.NotEmpty(receivedMessages);
            }
        }
    }
}