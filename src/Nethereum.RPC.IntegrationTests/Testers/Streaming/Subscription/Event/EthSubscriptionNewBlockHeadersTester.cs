using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Subscriptions;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers.Streaming.Subscription.Event
{

    public class EthSubscriptionNewBlockHeadersTester : StreamingRPCRequestTester
    {

        [Fact(DisplayName = "Subscription - Event - Should receive new block headers")]
        public override async Task ExecuteAsync()
        {
            using(var context = await CreateAndStartStreamingClientAsync())
            { 
                var receivedMessages = new ConcurrentBag<Block>();
                var subscription = new EthNewBlockHeadersSubscription(context.StreamingClient);

                subscription.SubscriptionDataResponse += delegate(object source, StreamingEventArgs<Block> args)
                {
                    receivedMessages.Add(args.Response);
                    TryUnsubscribe(subscription);
                };

                await subscription.SubscribeAsync(Guid.NewGuid().ToString());

                await WaitForFirstMessage(subscription, receivedMessages, TimeSpan.FromMinutes(1));

                Assert.NotEmpty(receivedMessages);
            }
        }

    }
}