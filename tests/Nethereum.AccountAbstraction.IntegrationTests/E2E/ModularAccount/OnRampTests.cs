using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.Documentation;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [Collection(ModularAccountBundlerCollection.COLLECTION_NAME)]
    public class OnRampTests
    {
        private readonly ModularAccountBundlerFixture _fixture;

        public OnRampTests(ModularAccountBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private IAAClient BuildClient()
        {
            var infra = new AADeploymentAddresses(
                _fixture.EntryPointService.ContractAddress,
                _fixture.FactoryService.ContractAddress,
                _fixture.EcdsaValidatorService.ContractAddress,
                VerifyingPaymasterAddress: string.Empty);

            var services = new ServiceCollection();
            services.AddNethereumAccountAbstraction(o => o
                .UseWeb3(_fixture.Bootstrap.OperatorWeb3)
                .UseDeploymentAddresses(infra)
                .UseBundler(_fixture.Bundler));

            return services.BuildServiceProvider().GetRequiredService<IAAClient>();
        }

        [Fact]
        [Trait("UseCase", "OnRamp")]
        [Trait("Doc", "docs/aa/getting-started.md")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-account-deployment", "The simple way: create a modular account and land a UserOperation", Order = 5)]
        public async Task Given_AddNethereumAccountAbstraction_When_CreateAccount_and_send_typed_op_Then_lands_in_a_few_lines()
        {
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            Assert.False(account.IsDeployed);

            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));
            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());

            testCounter.UseAccountAbstraction(account, client);
            var receipt = (AATransactionReceipt)await testCounter.CountRequestAndWaitForReceiptAsync();

            Assert.True(receipt.UserOpSuccess, receipt.RevertReason);
            Assert.Equal(account.Address.ToLower(), receipt.Sender?.ToLower());

            var count = await testCounter.CountersQueryAsync(account.Address);
            Assert.Equal(BigInteger.One, count);

            var codeAfterDeploy = await _fixture.Bootstrap.Node.GetCodeAsync(account.Address);
            Assert.NotNull(codeAfterDeploy);
            Assert.True(codeAfterDeploy.Length > 0);
        }

        [Fact]
        [Trait("UseCase", "OnRamp")]
        [Trait("Doc", "docs/aa/getting-started.md")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-account-deployment", "Unfunded counterfactual account fails estimation with AA21", Order = 6)]
        public async Task Given_unfunded_account_created_via_client_When_send_typed_op_Then_AA21_minus32500()
        {
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            Assert.False(account.IsDeployed);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());

            testCounter.UseAccountAbstraction(account, client);

            var ex = await Assert.ThrowsAsync<System.InvalidOperationException>(() =>
                testCounter.CountRequestAndWaitForReceiptAsync());

            var rpcException = Assert.IsType<Nethereum.JsonRpc.Client.RpcResponseException>(ex.InnerException);
            Assert.Equal(BundlerErrorCodes.SimulateValidation, rpcException.RpcError.Code);
            Assert.Contains("AA21", ex.Message);
        }
    }
}
