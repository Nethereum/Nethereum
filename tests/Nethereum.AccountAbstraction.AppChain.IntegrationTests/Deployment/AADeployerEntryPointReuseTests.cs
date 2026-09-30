using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.Web3;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;
using DevChainRpcClient = Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Fixtures.DevChainRpcClient;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.Deployment
{
    public class AADeployerEntryPointReuseTests : IAsyncLifetime
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
        public async Task Given_a_pre_deployed_EntryPoint_configured_When_deploying_Then_it_is_reused_not_redeployed()
        {
            var preDeployedReceipt = await EntryPointService.DeployContractAndWaitForReceiptAsync(
                _web3, new EntryPointDeployment());
            var preDeployedAddress = preDeployedReceipt.ContractAddress;

            var config = new AppChainConfig
            {
                Owner = _operator.Address,
                ChainId = CHAIN_ID,
                EntryPointAddress = preDeployedAddress
            };

            var deployment = await new AADeployer(_web3).DeployAsync(config);

            Assert.Equal(preDeployedAddress, deployment.EntryPointAddress, ignoreCase: true);
            await AssertHasCodeAsync(deployment.EntryPointAddress);
        }

        [Fact]
        [Trait("Category", "E2E-AppChainModernization")]
        public async Task Given_no_configured_EntryPoint_When_deploying_Then_a_fresh_one_is_deployed_as_before()
        {
            var config = new AppChainConfig { Owner = _operator.Address, ChainId = CHAIN_ID };

            var deployment = await new AADeployer(_web3).DeployAsync(config);

            Assert.False(string.IsNullOrEmpty(deployment.EntryPointAddress));
            await AssertHasCodeAsync(deployment.EntryPointAddress);
        }

        [Fact]
        [Trait("Category", "E2E-AppChainModernization")]
        public async Task Given_a_configured_EntryPoint_with_no_code_on_chain_When_deploying_Then_it_falls_back_to_a_fresh_deploy()
        {
            const string codelessAddress = "0x1234000000000000000000000000000000abcd";

            var config = new AppChainConfig
            {
                Owner = _operator.Address,
                ChainId = CHAIN_ID,
                EntryPointAddress = codelessAddress
            };

            var deployment = await new AADeployer(_web3).DeployAsync(config);

            Assert.NotEqual(codelessAddress.ToLowerInvariant(), deployment.EntryPointAddress?.ToLowerInvariant());
            await AssertHasCodeAsync(deployment.EntryPointAddress);
        }

        [Fact]
        [Trait("Category", "E2E-AppChainModernization")]
        public async Task Given_a_configured_EntryPoint_address_that_has_code_but_is_not_an_EntryPoint_When_deploying_Then_it_throws_a_clear_configuration_error()
        {
            var nonEntryPointReceipt = await PayableTargetService.DeployContractAndWaitForReceiptAsync(
                _web3, new PayableTargetDeployment());
            var nonEntryPointAddress = nonEntryPointReceipt.ContractAddress;
            await AssertHasCodeAsync(nonEntryPointAddress);

            var config = new AppChainConfig
            {
                Owner = _operator.Address,
                ChainId = CHAIN_ID,
                EntryPointAddress = nonEntryPointAddress
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new AADeployer(_web3).DeployAsync(config));

            Assert.Contains(nonEntryPointAddress, ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("EntryPoint", ex.Message, StringComparison.Ordinal);
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
