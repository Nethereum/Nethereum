using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers.Streaming.Subscription.Reactive
{
    public class EthNewPendingTransactionObservableSubscriptionTester : StreamingRPCRequestTester
    {
        [Fact(DisplayName = "Subscription - Reactive - Should receive pending transactions")]
        public override async Task ExecuteAsync()
        {
            using(var context = await CreateAndStartStreamingClientAsync())
            { 
                var receivedMessages = new ConcurrentBag<string>();
                var subscription = new EthNewPendingTransactionObservableSubscription(context.StreamingClient);

                subscription
                    .GetSubscriptionDataResponsesAsObservable()
                    .Subscribe(txHash =>
                    {
                        receivedMessages.Add(txHash);
                        TryUnsubscribe(subscription);
                    });

                await subscription.SubscribeAsync(Guid.NewGuid().ToString());

                await WaitForFirstMessage(subscription, receivedMessages, TimeSpan.FromMinutes(2));

                Assert.NotEmpty(receivedMessages);
            }
        }
    }
}