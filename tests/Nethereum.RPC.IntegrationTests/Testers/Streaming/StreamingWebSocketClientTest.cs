using Moq;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers.Streaming
{
    public class StreamingWebSocketClientTest
    {
        /// <summary>
        /// Ensures that there is error handling for the situation where SendAsync is called but the websocket is null
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task WhenWebSocketIsNull_SendAsync_ThrowsExpectedException()
        {
            var client = new StreamingWebSocketClient("");

            var rpcRequestMessage = new RpcRequestMessage("", "");
            var mockResponseHandler = new Mock<IRpcStreamingResponseHandler>();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendRequestAsync(rpcRequestMessage, mockResponseHandler.Object)).ConfigureAwait(false);
            Assert.Equal("Websocket is null.  Ensure that StartAsync has been called to create the websocket.", exception.Message);
        }
    }
}
