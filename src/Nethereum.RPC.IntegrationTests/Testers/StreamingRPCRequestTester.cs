using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Tests.Testers
{
    public abstract class StreamingRPCRequestTester<T>: IStreamingRPCRequestTester
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
            await ExecuteAsync(client);
        }

        public abstract Task ExecuteAsync(IStreamingClient client);

        public abstract Type GetRequestType();
    }
}