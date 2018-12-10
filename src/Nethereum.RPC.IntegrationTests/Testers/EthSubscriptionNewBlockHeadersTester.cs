using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
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
            var tokenSource = new CancellationTokenSource();
            var recievedMessage = false;
            var successfulResponse = false;
            var failureMessage = string.Empty;
            Block successResponse = null;
            this.StreamingClient.StreamingMessageReceived += (s, e) => 
            {
                recievedMessage = true;
                successfulResponse = !e.Message.HasError;
                if (e.Message.HasError)
                {
                    failureMessage = e.Message.Error.Message;
                }
                else
                {
                    successResponse = e.Message.GetResult<Block>();
                }

                tokenSource.Cancel();
            };

            await ExecuteAsync();

            try
            {
                await Task.Delay(10000, tokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // swallow, escape hatch
            }

            Assert.True(recievedMessage, "Did not recieved response");
            Assert.True(successfulResponse, $"Response indicated failure: {failureMessage}");
            Assert.NotNull(successResponse);
        }

        public override async Task ExecuteAsync(IStreamingClient client)
        {
            var subscription = new EthNewBlockHeadersSubscription(client);
            await subscription.SendRequestAsync(Guid.NewGuid());
        }

        public override Type GetRequestType()
        {
            return typeof (EthNewPendingTransactionSubscription);
        }
    }
}