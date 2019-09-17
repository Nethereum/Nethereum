using Moq;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.Subscriptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers.Streaming.Subscription.Event
{
    public class EthSyncingSubscriptionTester : StreamingRPCRequestTester
    {
        /// <summary>
        /// Uses a mocked streaming client and a mock streaming response
        /// to ensure the subscription receives syncing messages and invokes the event
        /// Mocking is used because waiting for syncing on a real network takes too long
        /// and is too unpredicable for a reliable test
        /// </summary>
        [Fact(DisplayName = "Subscription - Event - Should receive syncing notifications")]
        public override async Task ExecuteAsync()
        {
            var subscriptionId = Guid.NewGuid().ToString();

            Mock<IStreamingClient> mockStreamingClient = CreateMockStreamingClient();
            var streamingClient = mockStreamingClient.Object;

            RpcStreamingResponseMessage mockSyncingResponseMessage = CreateMockStreamingResponseMessage(subscriptionId);

            RpcRequest actualRpcRequest = null;

            // mock the Subscribe call so we can verify the request
            mockStreamingClient
                .Setup(s => s.SendRequestAsync(It.IsAny<RpcRequest>(), It.IsAny<IRpcStreamingResponseHandler>(), It.IsAny<string>()))
                .Callback<RpcRequest, IRpcStreamingResponseHandler, string>((request, handler, route) =>
                {
                    actualRpcRequest = request;
                })
                .Returns(Task.CompletedTask);

            var subscription = new EthSyncingSubscription(streamingClient);

            var receivedMessages = new ConcurrentBag<JObject>();
            // attach our event handler
            subscription.SubscriptionDataResponse += delegate (object sender, StreamingEventArgs<JObject> args)
            {
                receivedMessages.Add(args.Response);
            };

            if (!streamingClient.IsStarted) await streamingClient.StartAsync();
            await subscription.SubscribeAsync(subscriptionId);

            // ensure that Subscribe RPC Request has been created and sent to the client
            Assert.Equal(ApiMethods.eth_subscribe.ToString(), actualRpcRequest.Method);
            Assert.Equal("syncing", actualRpcRequest.RawParameters[0]);

            // simulate a response (because we don't actually have a websocket)
            // this will trigger the event on the same thread
            subscription.HandleResponse(mockSyncingResponseMessage);

            var firstMessageReceived = receivedMessages.FirstOrDefault();

            //ensure the message we received was the one we sent
            Assert.Equal(JsonConvert.SerializeObject(mockSyncingResponseMessage.Params.Result), JsonConvert.SerializeObject(firstMessageReceived));
        }

        private static Mock<IStreamingClient> CreateMockStreamingClient()
        {
            var mockStreamingClient = new Mock<IStreamingClient>();

            mockStreamingClient
                .Setup(s => s.StartAsync()).Returns(Task.CompletedTask);
            mockStreamingClient
                .Setup(s => s.IsStarted).Returns(true);
            return mockStreamingClient;
        }

        private static RpcStreamingResponseMessage CreateMockStreamingResponseMessage(string id)
        {
            const string JSON_TEMPLATE = "{\"subscription\":\"[SUBSCRIPTION_ID]\",\"result\":{\"syncing\":true,\"status\":{\"startingBlock\":674427,\"currentBlock\":67400,\"highestBlock\":674432,\"pulledStates\":0,\"knownStates\":0}}}";
            var json = JSON_TEMPLATE.Replace("[SUBSCRIPTION_ID]", id);
            var mockStreamingParams = JsonConvert.DeserializeObject<RpcStreamingParams>(json);
            var mockResponseMessage = new RpcStreamingResponseMessage("eth_subscribe", mockStreamingParams);
            return mockResponseMessage;
        }
    }
}
