using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Deployment;
using Nethereum.DevChain;
using Nethereum.Web3;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.Deployment
{
    public class AAModuleDeployerTests : IAsyncLifetime
    {
        private const int CHAIN_ID = 31337;
        private const string OPERATOR_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

        private DevChainNode _node = null!;
        private IWeb3 _web3 = null!;

        public async Task InitializeAsync()
        {
            var operatorAccount = new Web3Account(OPERATOR_PRIVATE_KEY, CHAIN_ID);
            _node = new DevChainNode(new DevChainConfig
            {
                ChainId = CHAIN_ID,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            });
            await _node.StartAsync(new[] { operatorAccount.Address }, Nethereum.Web3.Web3.Convert.ToWei(10000));
            _web3 = _node.CreateWeb3(operatorAccount);
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        [Trait("Category", "E2E-AppChainModernization")]
        public async Task Given_default_options_When_deploying_Then_owner_validator_session_stack_and_recovery_are_deployed_with_code_and_unrequested_modules_are_null()
        {
            var addresses = await new AAModuleDeployer(_web3).DeployAsync();

            await AssertHasCodeAsync(addresses.EcdsaValidator);
            await AssertHasCodeAsync(addresses.SmartSession);
            await AssertHasCodeAsync(addresses.SudoPolicy);
            await AssertHasCodeAsync(addresses.UniActionPolicy);
            await AssertHasCodeAsync(addresses.EcdsaSessionValidator);
            await AssertHasCodeAsync(addresses.SocialRecovery);

            Assert.Null(addresses.OwnableExecutor);

            var deployed = new[]
            {
                addresses.EcdsaValidator, addresses.SmartSession, addresses.SudoPolicy,
                addresses.UniActionPolicy, addresses.EcdsaSessionValidator, addresses.SocialRecovery
            };
            Assert.Equal(deployed.Length, deployed.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        [Trait("Category", "E2E-AppChainModernization")]
        public async Task Given_options_selecting_a_subset_When_deploying_Then_only_the_selected_modules_are_deployed()
        {
            var addresses = await new AAModuleDeployer(_web3).DeployAsync(new AAModuleDeploymentOptions
            {
                EcdsaValidator = false,
                SmartSession = false,
                SocialRecovery = false,
                OwnableExecutor = true
            });

            await AssertHasCodeAsync(addresses.OwnableExecutor);

            Assert.Null(addresses.EcdsaValidator);
            Assert.Null(addresses.SmartSession);
            Assert.Null(addresses.SudoPolicy);
            Assert.Null(addresses.UniActionPolicy);
            Assert.Null(addresses.EcdsaSessionValidator);
            Assert.Null(addresses.SocialRecovery);
        }

        private async Task AssertHasCodeAsync(string? address)
        {
            Assert.False(string.IsNullOrEmpty(address), "Expected a deployed module address, got null/empty.");
            var code = await _web3.Eth.GetCode.SendRequestAsync(address);
            Assert.False(string.IsNullOrEmpty(code) || code == "0x",
                $"Expected deployed bytecode at {address}, found none.");
        }
    }
}
