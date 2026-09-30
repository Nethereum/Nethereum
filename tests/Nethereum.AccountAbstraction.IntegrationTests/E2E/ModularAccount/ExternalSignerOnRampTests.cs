using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Documentation;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [Collection(ModularAccountBundlerCollection.COLLECTION_NAME)]
    public class ExternalSignerOnRampTests
    {
        private readonly ModularAccountBundlerFixture _fixture;

        public ExternalSignerOnRampTests(ModularAccountBundlerFixture fixture)
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
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-account-deployment", "Signer-agnostic on-ramp: create a modular account for any IAccountSigningService", Order = 4)]
        [NethereumDocExample(DocSection.AccountAbstraction, "external-signer", "Own a modular account with an external ECDSA signer (KMS/HSM/hardware wallet)", Order = 1)]
        public async Task Given_an_account_owned_by_an_external_signer_When_it_signs_a_userOp_Then_it_validates_on_chain()
        {
            var client = BuildClient();

            var ownerKey = EthECKey.GenerateKey();
            var externalSigner = new EthECKeyExternalSigner(ownerKey);
            var externalSignerAddress = await externalSigner.GetAddressAsync();

            var signingService = new AccountSigningExternalService(externalSigner);
            var validator = new EcdsaValidatorModule(_fixture.EcdsaValidatorService.ContractAddress);
            var initData = AccountInitDataBuilder.BuildEcdsa(_fixture.EcdsaValidatorService.ContractAddress, externalSignerAddress);

            var account = await client.CreateAccountAsync(signingService, validator, initData);
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

            var onChainOwner = await _fixture.EcdsaValidatorService.GetOwnerQueryAsync(account.Address);
            Assert.Equal(externalSignerAddress.ToLower(), onChainOwner.ToLower());
        }
    }
}
