using Nethereum.JsonRpc.Client.Streaming;
using System;

namespace Nethereum.RPC.Tests.Testers.Streaming
{
    public class StreamingWebSocketClientTestContext : IDisposable
    {
        public StreamingWebSocketClientTestContext(TestSettings settings)
        {
            StreamingClient = ClientFactory.GetStreamingClient(settings);
        }

        public IStreamingClient StreamingClient { get; private set; }

        public void Dispose()
        {
            if(StreamingClient.IsStarted) StreamingClient.StopAsync().Wait();
        }
    }
}