using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.RPC.Tests.Testers
{
    public abstract class RPCRequestTester<T>: IRPCRequestTester
    {
        public IClient Client { get; set; }
        public IStreamingClient StreamingClient { get; set; }

        public TestSettings Settings { get; set; }

        protected RPCRequestTester()
        {
            Settings = new TestSettings();
            Client = ClientFactory.GetClient(Settings);
            StreamingClient = ClientFactory.GetStreamingClient(Settings);
        }

        public Task<T> ExecuteAsync()
        {
            return ExecuteAsync(Client);
        }

        public async Task<object> ExecuteTestAsync(IClient client)
        {
            return await ExecuteAsync(client).ConfigureAwait(false);
        }

        public abstract Task<T> ExecuteAsync(IClient client);

        public abstract Type GetRequestType();
    }
}