using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.Subscriptions;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthPendingTransactionSubscriptionTester : StreamingRPCRequestTester<String[]>
    {
        [Fact]
        public async Task ShouldRetrieveAccounts()
        {
            var recievedMessage = false;
            var successfulResponse = false;
            var failureMessage = string.Empty;
            var successResponse = string.Empty;
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
                    successResponse = e.Message.GetResult<string>();
                }
            };

            await ExecuteAsync();

            await Task.Delay(5000);

            Assert.True(recievedMessage, "Did not recieved response");
            Assert.True(successfulResponse, $"Response indicated failure: {failureMessage}");
            Assert.False(string.IsNullOrEmpty(successResponse));
            Console.WriteLine(successResponse);
        }

        public override async Task ExecuteAsync(IStreamingClient client)
        {
            var subscription = new EthNewPendingTransactionSubscription(client);
            await subscription.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof (EthNewPendingTransactionSubscription);
        }
    }
}