using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.WebAuthn.ERC7579.Modules;
using Nethereum.AccountAbstraction.WebAuthn.Signing;
using Nethereum.Documentation;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [Collection(WebAuthnPrecompileModularAccountBundlerCollection.COLLECTION_NAME)]
    public class WebAuthnOnRampPrecompileE2ETests
    {
        private readonly WebAuthnPrecompileModularAccountBundlerFixture _fixture;

        public WebAuthnOnRampPrecompileE2ETests(WebAuthnPrecompileModularAccountBundlerFixture fixture)
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

        [Fact]
        [Trait("UseCase", "WebAuthnValidator")]
        [Trait("Doc", "docs/aa/webauthn.md")]
        [NethereumDocExample(DocSection.AccountAbstraction, "webauthn-passkeys", "usePrecompile: true routes verification through the EIP-7951 P256VERIFY precompile", Order = 2)]
        public async Task Given_WebAuthn_account_using_the_P256VERIFY_precompile_When_a_normal_typed_call_is_sent_Then_it_lands_at_precompile_gas_cost()
        {
            var client = BuildClient();

            var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: false, origin: "https://nethereum.local");
            var (pubKeyX, pubKeyY) = authenticator.GetPublicKey();
            var credentialId = WebAuthnValidatorFormat.GenerateCredentialId(pubKeyX, pubKeyY);

            var initData = WebAuthnAccountInitDataBuilder.BuildWebAuthn(
                _fixture.WebAuthnValidatorService.ContractAddress, pubKeyX, pubKeyY, requireUV: false);

            var validator = new WebAuthnValidatorModule(_fixture.WebAuthnValidatorService.ContractAddress, usePrecompile: true);
            var signingService = new WebAuthnAccountSigningService(authenticator, credentialId, rpId: "nethereum.local", usePrecompile: true);

            var account = await client.CreateAccountAsync(signingService, validator, initData);
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

            var landedFirst = await _fixture.Bundler.GetUserOperationByHash.SendRequestAsync(receipt.UserOpHash);
            Assert.NotNull(landedFirst?.UserOperation);
            var verificationGasLimitDeployInclusive = landedFirst.UserOperation.VerificationGasLimit.Value;

            var receipt2 = (AATransactionReceipt)await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(receipt2.UserOpSuccess, receipt2.RevertReason);

            var count2 = await testCounter.CountersQueryAsync(account.Address);
            Assert.Equal(new BigInteger(2), count2);

            var landedSecond = await _fixture.Bundler.GetUserOperationByHash.SendRequestAsync(receipt2.UserOpHash);
            Assert.NotNull(landedSecond?.UserOperation);
            var verificationGasLimitSteadyState = landedSecond.UserOperation.VerificationGasLimit.Value;

            Assert.True(verificationGasLimitSteadyState < 150_000,
                $"Expected the P256VERIFY precompile path's steady-state (no-deployment) " +
                $"VerificationGasLimit to sit near the ~92698 measured figure (77698 shared ERC-7579 " +
                $"dispatch base + WebAuthnValidatorModule's 15000 precompile buffer), well under the " +
                $"FCL fallback's ~477698 - got {verificationGasLimitSteadyState}.");
            Assert.True(verificationGasLimitSteadyState < verificationGasLimitDeployInclusive,
                $"Steady-state VerificationGasLimit ({verificationGasLimitSteadyState}) should be lower " +
                $"than the deploy-inclusive first call's ({verificationGasLimitDeployInclusive}).");
        }
    }
}
