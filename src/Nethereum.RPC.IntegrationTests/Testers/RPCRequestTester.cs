using Nethereum.JsonRpc.Client;
using Nethereum.XUnitEthereumClients;
using System;
using System.Threading.Tasks;

namespace Nethereum.RPC.Tests.Testers
{
    public abstract class RPCRequestTester<T>: IRPCRequestTester
    {
        public IClient Client { get; set; }

        public TestSettings Settings { get; set; }

        /// <summary>
        /// Sets up a local test net
        /// </summary>
        /// <param name="ethereumClientIntegrationFixture"></param>
        /// <param name="settingsName"></param>
        protected RPCRequestTester(
            EthereumClientIntegrationFixture ethereumClientIntegrationFixture, TestSettingsCategory testSettingsCategory)
        {
            Settings = new TestSettings(testSettingsCategory);
            Client = ethereumClientIntegrationFixture.GetWeb3().Client;
        }

        protected RPCRequestTester(TestSettingsCategory testSettingsCategory) 
            : this(new TestSettings(testSettingsCategory))
        {
        }

        protected RPCRequestTester(TestSettings settings)
        {
            Settings = settings;
            Client = ClientFactory.GetClient(Settings);
        }

        public Task<T> ExecuteAsync()
        {
            return ExecuteAsync(Client);
        }

        public async Task<object> ExecuteTestAsync(IClient client)
        {
            return await ExecuteAsync(client);
        }

        public abstract Task<T> ExecuteAsync(IClient client);

        public abstract Type GetRequestType();
    }
}