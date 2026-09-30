using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.WebAuthn.Client;
using Nethereum.AccountAbstraction.WebAuthn.ERC7579.Modules;
using Nethereum.AccountAbstraction.WebAuthn.Signing;
using Nethereum.Documentation;
using Nethereum.JsonRpc.Client;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [Collection(WebAuthnModularAccountBundlerCollection.COLLECTION_NAME)]
    public class WebAuthnOnRampE2ETests
    {
        private readonly WebAuthnModularAccountBundlerFixture _fixture;

        public WebAuthnOnRampE2ETests(WebAuthnModularAccountBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private IAAClient BuildClient()
        {
            var infra = new AADeploymentAddresses(
                _fixture.EntryPointService.ContractAddress,
                _fixture.FactoryService.ContractAddress,
                EcdsaValidatorAddress: string.Empty,
                VerifyingPaymasterAddress: string.Empty);

            var services = new ServiceCollection();
            services.AddNethereumAccountAbstraction(o => o
                .UseWeb3(_fixture.Bootstrap.OperatorWeb3)
                .UseDeploymentAddresses(infra)
                .UseBundler(_fixture.Bundler));

            return services.BuildServiceProvider().GetRequiredService<IAAClient>();
        }

        private (SoftwareWebAuthnAuthenticator authenticator, byte[] credentialId, byte[] initData, WebAuthnValidatorModule validator)
            BuildCredentialAndInitData()
        {
            var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: false, origin: "https://nethereum.local");
            var (pubKeyX, pubKeyY) = authenticator.GetPublicKey();
            var credentialId = WebAuthnValidatorFormat.GenerateCredentialId(pubKeyX, pubKeyY);

            var initData = WebAuthnAccountInitDataBuilder.BuildWebAuthn(
                _fixture.WebAuthnValidatorService.ContractAddress, pubKeyX, pubKeyY, requireUV: false);

            var validator = new WebAuthnValidatorModule(_fixture.WebAuthnValidatorService.ContractAddress);

            return (authenticator, credentialId, initData, validator);
        }

        [Fact]
        [Trait("UseCase", "WebAuthnValidator")]
        [Trait("Doc", "docs/aa/webauthn.md")]
        [NethereumDocExample(DocSection.AccountAbstraction, "webauthn-passkeys", "The Simple Way: create a passkey-owned account with CreateWebAuthnAccountAsync and send", Order = 1)]
        public async Task Given_WebAuthn_account_created_via_the_generic_on_ramp_When_a_normal_typed_call_is_sent_Then_bundler_estimation_and_execution_land_through_the_handler()
        {
            var client = BuildClient();

            var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: false, origin: "https://nethereum.local");
            var created = await authenticator.CreateCredentialAsync(new WebAuthnCredentialCreationOptions { RequireUserVerification = false });

            var account = await client.CreateWebAuthnAccountAsync(
                created, authenticator, _fixture.WebAuthnValidatorService.ContractAddress, rpId: "nethereum.local");
            Assert.IsType<NethereumSmartAccount>(account);
            Assert.False(account.IsDeployed);

            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));
            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());

            client.Configure(testCounter, account);

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
        [Trait("UseCase", "WebAuthnValidator")]
        [Trait("Doc", "docs/aa/webauthn.md")]
        public async Task Given_WebAuthn_account_via_the_generic_on_ramp_When_signed_by_an_unregistered_credential_Then_the_handler_path_rejects_it()
        {
            var client = BuildClient();

            var (registeredAuthenticator, credentialId, initData, validator) = BuildCredentialAndInitData();

            var account = await client.CreateAccountAsync(
                new WebAuthnAccountSigningService(registeredAuthenticator, credentialId, rpId: "nethereum.local"),
                validator, initData);
            Assert.False(account.IsDeployed);

            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));
            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());

            var wrongKeyAuthenticator = new SoftwareWebAuthnAuthenticator(requireUV: false, origin: "https://nethereum.local");
            var wrongAccountForSigning = new NethereumSmartAccount(
                account.Address,
                new WebAuthnAccountSigningService(wrongKeyAuthenticator, credentialId, rpId: "nethereum.local"),
                validator,
                isDeployed: account.IsDeployed,
                account.Salt,
                account.InitData);

            client.Configure(testCounter, wrongAccountForSigning);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => testCounter.CountRequestAndWaitForReceiptAsync());

            var code = ex switch
            {
                BundlerRpcException bundlerEx => bundlerEx.Code,
                RpcResponseException rpcEx => rpcEx.RpcError.Code,
                _ => throw new Exception($"Unexpected exception type {ex.GetType()}: {ex.Message}", ex)
            };
            Assert.Equal(BundlerErrorCodes.InvalidSignature, code);
            Assert.Contains("AA24", ex.Message);

            var codeAfterAttempt = await _fixture.Bootstrap.Node.GetCodeAsync(account.Address);
            Assert.True(codeAfterAttempt == null || codeAfterAttempt.Length == 0);
        }
    }
}
