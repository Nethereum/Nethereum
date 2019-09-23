using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Subscriptions;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers.Streaming.Subscription.Event
{

    public class EthLogsSubscriptionTester : StreamingRPCRequestTester
    {

        [Fact(DisplayName = "Subscription - Event - Should receive new logs")]
        public override async Task ExecuteAsync()
        {
            using(var context = await CreateAndStartStreamingClientAsync())
            { 
                var receivedMessages = new ConcurrentBag<FilterLog>();
                var subscription = new EthLogsSubscription(context.StreamingClient);

                subscription.SubscriptionDataResponse += delegate (object sender, StreamingEventArgs<FilterLog> args)
                {
                    receivedMessages.Add(args.Response);
                    TryUnsubscribe(subscription);
                };

                await subscription.SubscribeAsync(new NewFilterInput(), Guid.NewGuid().ToString());

                await WaitForFirstMessage(subscription, receivedMessages, TimeSpan.FromMinutes(1));

                Assert.NotEmpty(receivedMessages);
            }
        }

    }
}