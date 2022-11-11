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
    public class EthSubscriptionNewBlockHeadersTester : StreamingRPCRequestTester
    {
        [Fact]
        public async Task ShouldRetrieveBlock()
        {

            Block receivedMessage = null;
            var subscription = new EthNewBlockHeadersSubscription(StreamingClient);
            subscription.SubscriptionDataResponse += delegate(object sender, StreamingEventArgs<Block> args)
                {
                    receivedMessage = args.Response;
                };

            await subscription.SubscribeAsync(Guid.NewGuid().ToString()).ConfigureAwait(false);
            
            try
            {
                await Task.Delay(10000).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // swallow, escape hatch
            }

            Assert.NotNull(receivedMessage);
        }

        public override async Task ExecuteAsync(IStreamingClient client)
        {
            var subscription = new EthNewBlockHeadersSubscription(client);
            await subscription.SubscribeAsync(Guid.NewGuid().ToString()).ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof (EthNewPendingTransactionSubscription);
        }
    }
}