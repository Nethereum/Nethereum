using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Fixtures;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.Web3;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;
using DevChainRpcClient = Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Fixtures.DevChainRpcClient;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.Deployment
{
    /// <summary>
    /// Proves the AppChain <see cref="AADeployer"/> DEPLOYS the ERC-7579 module contracts and records them
    /// on <see cref="AppChainDeployment.Modules"/> with real on-chain bytecode - the assertion fails if it
    /// ever regresses to recording module addresses it never deployed. Also proves a partially-populated
    /// supplied module set is rejected rather than silently skipping deployment.
    /// </summary>
    public class AADeployerModuleDeploymentTests : IAsyncLifetime
    {
        private const int CHAIN_ID = 31337;
        private const string OPERATOR_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

        private DevChainNode _node = null!;
        private DevChainRpcClient _rpcClient = null!;
        private Web3Account _operator = null!;
        private IWeb3 _web3 = null!;

        public async Task InitializeAsync()
        {
            _operator = new Web3Account(OPERATOR_PRIVATE_KEY, CHAIN_ID);
            _node = new DevChainNode(new DevChainConfig
            {
                ChainId = CHAIN_ID,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            });
            await _node.StartAsync(new[] { _operator.Address }, Nethereum.Web3.Web3.Convert.ToWei(100000));

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            var dispatcher = new RpcDispatcher(registry, new RpcContext(_node, CHAIN_ID, new EmptyServiceProvider()));
            _rpcClient = new DevChainRpcClient(dispatcher);
            _web3 = new Web3.Web3(_operator, _rpcClient);
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        [Trait("Category", "E2E-AppChainModernization")]
        public async Task Given_no_pre_supplied_modules_When_deploying_Then_the_modules_are_deployed_and_recorded_with_code()
        {
            var config = new AppChainConfig { Owner = _operator.Address, ChainId = CHAIN_ID };

            var deployment = await new AADeployer(_web3).DeployAsync(config);

            Assert.NotNull(deployment.Modules);
            await AssertHasCodeAsync(deployment.Modules.EcdsaValidator);
            await AssertHasCodeAsync(deployment.Modules.SmartSession);
            await AssertHasCodeAsync(deployment.Modules.SudoPolicy);
            await AssertHasCodeAsync(deployment.Modules.UniActionPolicy);
            await AssertHasCodeAsync(deployment.Modules.EcdsaSessionValidator);
            await AssertHasCodeAsync(deployment.Modules.SocialRecovery);

            await AssertHasCodeAsync(deployment.EntryPointAddress);
            await AssertHasCodeAsync(deployment.AccountFactoryAddress);
            await AssertHasCodeAsync(deployment.AccountRegistryAddress);
            await AssertHasCodeAsync(deployment.SponsoredPaymasterAddress);
        }

        [Fact]
        [Trait("Category", "E2E-AppChainModernization")]
        public async Task Given_InstallSocialRecovery_is_false_When_deploying_Then_recovery_is_skipped_but_the_session_stack_is_still_deployed()
        {
            var config = new AppChainConfig
            {
                Owner = _operator.Address,
                ChainId = CHAIN_ID,
                DefaultModules = new DefaultModulesConfig { InstallSocialRecovery = false }
            };

            var deployment = await new AADeployer(_web3).DeployAsync(config);

            Assert.Null(deployment.Modules.SocialRecovery);
            await AssertHasCodeAsync(deployment.Modules.EcdsaValidator);
            await AssertHasCodeAsync(deployment.Modules.SmartSession);
        }

        [Fact]
        [Trait("Category", "E2E-AppChainModernization")]
        public async Task Given_a_partially_supplied_module_set_missing_a_requested_module_When_deploying_Then_it_is_rejected()
        {
            var config = new AppChainConfig
            {
                Owner = _operator.Address,
                ChainId = CHAIN_ID,
                DefaultModules = new DefaultModulesConfig
                {
                    InstallOwnerValidator = true,
                    InstallSessionKeys = true,
                    InstallSocialRecovery = false,
                    ModuleAddresses = new Nethereum.AccountAbstraction.Deployment.AAModuleAddresses
                    {
                        EcdsaValidator = _operator.Address
                    }
                }
            };

            var ex = await Assert.ThrowsAsync<System.InvalidOperationException>(
                () => new AADeployer(_web3).DeployAsync(config));
            Assert.Contains("SmartSession stack", ex.Message);
        }

        private async Task AssertHasCodeAsync(string? address)
        {
            Assert.False(string.IsNullOrEmpty(address), "Expected a deployed address, got null/empty.");
            var code = await _web3.Eth.GetCode.SendRequestAsync(address);
            Assert.False(string.IsNullOrEmpty(code) || code == "0x",
                $"Expected deployed bytecode at {address}, found none.");
        }
    }
}
