using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.Subscriptions;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers.Streaming.Subscription.Event
{
    public class EthPendingTransactionSubscriptionTester : StreamingRPCRequestTester
    {

        [Fact(DisplayName = "Subscription - Event - Should receive pending transactions")]
        public override async Task ExecuteAsync()
        {
            using(var context = await CreateAndStartStreamingClientAsync())
            { 
                var receivedMessages = new ConcurrentBag<StreamingEventArgs<string>>();
                var subscription = new EthNewPendingTransactionSubscription(context.StreamingClient);

                subscription.SubscriptionDataResponse += delegate (object sender, StreamingEventArgs<string> args)
                {
                    receivedMessages.Add(args);
                    TryUnsubscribe(subscription);
                };

                await subscription.SubscribeAsync(Guid.NewGuid().ToString());

                await WaitForFirstMessage(subscription, receivedMessages, TimeSpan.FromMinutes(1));

                if(subscription.SubscriptionState == SubscriptionState.Subscribed) 
                       await subscription.UnsubscribeAsync();

                Assert.NotEmpty(receivedMessages);
                Assert.DoesNotContain(receivedMessages, m => m.Exception != null);
            }
        }

    }
}