using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;

namespace Nethereum.Besu.IntegrationTests
{
    public abstract class RPCRequestTester<T> : IRPCRequestTester
    {
        protected RPCRequestTester()
        {
            Settings = new TestSettings();
            Client = ClientFactory.GetClient(Settings);
        }

        public IClient Client { get; set; }
        public TestSettings Settings { get; set; }

        public async Task<object> ExecuteTestAsync(IClient client)
        {
            return await ExecuteAsync(client).ConfigureAwait(false);
        }

        public abstract Type GetRequestType();

        public Task<T> ExecuteAsync()
        {
            return ExecuteAsync(Client);
        }

        public abstract Task<T> ExecuteAsync(IClient client);
    }
}