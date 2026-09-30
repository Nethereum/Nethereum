using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.NonModularAccount
{
    [Collection(NonModularAccountBundlerCollection.COLLECTION_NAME)]
    public class OnRampTests
    {
        private readonly NonModularAccountBundlerFixture _fixture;

        public OnRampTests(NonModularAccountBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private IAAClient BuildClient()
        {
            var deploymentAddresses = new AADeploymentAddresses(
                _fixture.EntryPointService.ContractAddress,
                NethereumAccountFactoryAddress: string.Empty,
                EcdsaValidatorAddress: string.Empty,
                VerifyingPaymasterAddress: string.Empty);

            var services = new ServiceCollection();
            services.AddNethereumAccountAbstraction(o => o
                .UseWeb3(_fixture.Bootstrap.OperatorWeb3)
                .UseDeploymentAddresses(deploymentAddresses)
                .UseBundler(_fixture.Bundler));

            return services.BuildServiceProvider().GetRequiredService<IAAClient>();
        }

        [Fact]
        [Trait("UseCase", "OnRamp")]
        [Trait("Doc", "docs/aa/getting-started.md")]
        public async Task Given_deployed_SimpleAccount_When_GetAccount_and_Configure_and_send_typed_op_Then_lands_through_the_SmartAccount_tier()
        {
            var client = BuildClient();

            var salt = (ulong)System.Random.Shared.NextInt64();
            var (accountAddress, ownerKey) = await _fixture.DeployFundedSimpleAccountAsync(salt);

            var signingService = new AccountSigningOfflineService(ownerKey);
            var account = client.GetAccount(accountAddress, signingService);
            Assert.IsType<SmartAccount>(account);
            Assert.True(account.IsDeployed);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());

            client.Configure(testCounter, account);
            var receipt = (AATransactionReceipt)await testCounter.CountRequestAndWaitForReceiptAsync();

            Assert.True(receipt.UserOpSuccess, receipt.RevertReason);
            Assert.Equal(accountAddress.ToLower(), receipt.Sender?.ToLower());

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);
        }
    }
}
