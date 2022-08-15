using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.RPC.Tests.Testers
{
    //TODO:Subscriptions
    public abstract class StreamingRPCRequestTester: IStreamingRPCRequestTester
    {
        public IStreamingClient StreamingClient { get; set; }

        public TestSettings Settings { get; set; }

        protected StreamingRPCRequestTester()
        {
            Settings = new TestSettings();
            StreamingClient = ClientFactory.GetStreamingClient(Settings);
        }

        public Task ExecuteAsync()
        {
            return ExecuteAsync(StreamingClient);
        }

        public async Task ExecuteTestAsync(IStreamingClient client)
        {
            await ExecuteAsync(client).ConfigureAwait(false);
        }

        public abstract Task ExecuteAsync(IStreamingClient client);

        public abstract Type GetRequestType();
    }
}