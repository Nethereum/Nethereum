using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Subscriptions;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    //TODO:Subscriptions
    public class EthPendingTransactionSubscriptionTester : StreamingRPCRequestTester
    {
        [Fact]
        public async Task ShouldRetrievePendingTransaction()
        {

            string receivedMessage = null;
            var subscription = new EthNewPendingTransactionSubscription(StreamingClient);
            subscription.SubscriptionDataResponse += delegate (object sender, StreamingEventArgs<string> args)
            {
                receivedMessage = args.Response;
            };

            await subscription.SubscribeAsync(Guid.NewGuid().ToString());

            try
            {
                await Task.Delay(10000);
            }
            catch (TaskCanceledException)
            {
                // swallow, escape hatch
            }

            Assert.NotNull(receivedMessage);
        }

        public override async Task ExecuteAsync(IStreamingClient client)
        {
            var subscription = new EthNewPendingTransactionSubscription(client);
            await subscription.SubscribeAsync(Guid.NewGuid().ToString());
        }

        public override Type GetRequestType()
        {
            return typeof(EthNewPendingTransactionSubscription);
        }
    }
}