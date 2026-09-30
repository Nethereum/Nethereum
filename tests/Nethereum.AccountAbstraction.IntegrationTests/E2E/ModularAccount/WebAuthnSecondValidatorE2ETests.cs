using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.WebAuthn.Client;
using Nethereum.AccountAbstraction.WebAuthn.ERC7579.Modules;
using Nethereum.Documentation;
using Nethereum.Signer;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [Collection(WebAuthnSecondValidatorBundlerCollection.COLLECTION_NAME)]
    public class WebAuthnSecondValidatorE2ETests
    {
        private readonly WebAuthnSecondValidatorBundlerFixture _fixture;

        public WebAuthnSecondValidatorE2ETests(WebAuthnSecondValidatorBundlerFixture fixture)
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

        [Theory]
        [Trait("UseCase", "WebAuthnValidator")]
        [Trait("Doc", "docs/aa/webauthn.md")]
        [NethereumDocExample(DocSection.AccountAbstraction, "webauthn-passkeys", "Install WebAuthn as a second validator alongside an existing ECDSA validator", Order = 3)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_WebAuthn_installed_as_a_second_validator_alongside_ECDSA_When_a_passkey_signed_op_is_sent_with_no_gas_override_Then_it_lands(
            bool usePrecompile)
        {
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());

            testCounter.UseAccountAbstraction(account, client);
            var deployReceipt = (AATransactionReceipt)await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(deployReceipt.UserOpSuccess, deployReceipt.FailureDiagnostic);

            var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: false, origin: "https://nethereum.local");
            var credential = await authenticator.CreateCredentialAsync(
                new WebAuthnCredentialCreationOptions { RequireUserVerification = false });
            var moduleConfig = new WebAuthnValidatorConfig(
                _fixture.WebAuthnValidatorService.ContractAddress, threshold: 1, credential.ToCredential());

            var accountService = new NethereumAccountService(_fixture.Bootstrap.OperatorWeb3, account.Address);
            accountService.UseAccountAbstraction(account, client);
            var installReceipt = await accountService.InstallModuleAndWaitForReceiptAsync(moduleConfig);
            Assert.Equal(BigInteger.One, installReceipt.Status.Value);
            Assert.True(await accountService.IsModuleInstalledAsync(moduleConfig));

            var passkeyAccount = client.GetWebAuthnAccount(
                account.Address, credential, authenticator,
                _fixture.WebAuthnValidatorService.ContractAddress, rpId: "nethereum.local", usePrecompile);

            client.Configure(testCounter, passkeyAccount);
            var receipt = (AATransactionReceipt)await testCounter.CountRequestAndWaitForReceiptAsync();

            Assert.True(receipt.UserOpSuccess, receipt.FailureDiagnostic);

            var count = await testCounter.CountersQueryAsync(account.Address);
            Assert.Equal(new BigInteger(2), count);
        }
    }
}
