using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Example.Contracts.BookingRegistry;
using Nethereum.AccountAbstraction.Example.Contracts.TestCounter;
using Nethereum.AccountAbstraction.Example.Hosting;
using Nethereum.RPC;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    [CollectionDefinition(COLLECTION_NAME)]
    public class ExampleCollection : ICollectionFixture<ExampleFixture>
    {
        public const string COLLECTION_NAME = ExampleFixture.COLLECTION_NAME;
    }

    public class ExampleFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "AaExample";
        public const int CHAIN_ID = HostBootstrap.ChainId;

        public InProcessBundlerHost Bootstrap { get; private set; } = null!;
        public IWeb3 Web3 { get; private set; } = null!;
        public IAAClient Client { get; private set; } = null!;

        public IAccountAbstractionBundlerService Bundler { get; private set; } = null!;

        public DeployedStack Stack { get; private set; } = null!;

        public TestCounterService TestCounter { get; private set; } = null!;

        public IDevChainFaucet Faucet { get; private set; } = null!;

        public BookingRegistryService BookingRegistry { get; private set; } = null!;

        public string EcdsaValidatorAddress { get; private set; } = null!;

        public string AccountImplementationAddress { get; private set; } = null!;

        private string _entryPointAddress = null!;

        public async Task InitializeAsync()
        {
            var infra = await HostBootstrap.StartInfraAsync();
            Bootstrap = infra.Bootstrap;
            Web3 = infra.OperatorWeb3;
            Bundler = infra.Bundler;
            Faucet = infra.Faucet;

            Stack = await HostBootstrap.DeployStackAsync(infra.OperatorWeb3, infra.Bundler);
            Client = Stack.Client;
            TestCounter = Stack.TestCounter;
            BookingRegistry = Stack.BookingRegistry;
            EcdsaValidatorAddress = Stack.Addresses.EcdsaValidatorAddress;
            AccountImplementationAddress = Stack.NethereumAccountImplementationAddress;
            _entryPointAddress = Stack.Addresses.EntryPointAddress;
        }

        public async Task DisposeAsync()
        {
            await Bootstrap.DisposeAsync();
        }

        public SessionState NewReadySession()
        {
            var session = new SessionState();
            session.PublishInfra(Stack, Web3, Bundler, Faucet);
            return session;
        }

        public Task FundAsync(string address, decimal ether = 1m) => Faucet.FundAsync(address, ether);

        public Task<PaymasterConfig> DeploySponsoringPaymasterAsync(decimal depositEth = 5m) =>
            HostBootstrap.DeploySponsoringPaymasterAsync(Web3, _entryPointAddress, Web3.TransactionManager.Account.Address, depositEth);
    }
}
