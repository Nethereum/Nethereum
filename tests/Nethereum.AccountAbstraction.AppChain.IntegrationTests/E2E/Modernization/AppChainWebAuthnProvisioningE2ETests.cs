using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.WebAuthn.ERC7579.Modules;
using Nethereum.AccountAbstraction.WebAuthn.Signing;
using Nethereum.Signer;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Modernization
{
    [Collection(AppChainModularBundlerFixture.COLLECTION_NAME)]
    public class AppChainWebAuthnProvisioningE2ETests
    {
        private readonly AppChainModularBundlerFixture _fixture;

        public AppChainWebAuthnProvisioningE2ETests(AppChainModularBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private IAAClient BuildClient()
        {
            var services = new ServiceCollection();
            services.AddNethereumAccountAbstraction(o => o
                .UseWeb3(_fixture.OperatorWeb3)
                .UseDeploymentAddresses(_fixture.Deployment.ToAADeploymentAddresses())
                .UseBundler(_fixture.Bundler));

            return services.BuildServiceProvider().GetRequiredService<IAAClient>();
        }

        [Fact]
        [Trait("Category", "E2E-AppChainModernization")]
        public async Task Given_an_admin_enrolls_a_passkey_owned_account_When_the_passkey_installs_a_module_Then_a_non_ecdsa_signer_owns_and_operates_it()
        {
            var d = _fixture.Deployment;

            var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: false, origin: "https://nethereum.local");
            var (pubKeyX, pubKeyY) = authenticator.GetPublicKey();
            var credentialId = WebAuthnValidatorFormat.GenerateCredentialId(pubKeyX, pubKeyY);
            var initData = WebAuthnAccountInitDataBuilder.BuildWebAuthn(
                _fixture.WebAuthnValidatorAddress, pubKeyX, pubKeyY, requireUV: false);

            var salt = new byte[32];
            salt[31] = 7;
            var accountAddress = await _fixture.AdminService.EnrollAccountAsync(salt, initData);
            Assert.True(await _fixture.AdminService.IsAccountDeployedAsync(accountAddress));
            Assert.True(await _fixture.AdminService.IsActiveAsync(accountAddress));

            var accountService = new NethereumAccountService(_fixture.OperatorWeb3, accountAddress);
            Assert.True(await IsInstalledAsync(accountService, _fixture.WebAuthnValidatorAddress));
            Assert.False(await IsInstalledAsync(accountService, d.Modules.SocialRecovery));

            await _fixture.Node.SetBalanceAsync(
                accountAddress, Nethereum.Web3.Web3.Convert.ToWei(10));

            var client = BuildClient();
            var passkeyAccount = client.GetAccount(
                accountAddress,
                new WebAuthnAccountSigningService(authenticator, credentialId, rpId: "nethereum.local"),
                new WebAuthnValidatorModule(_fixture.WebAuthnValidatorAddress));

            accountService.UseAccountAbstraction(passkeyAccount, client);
            var guardians = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey() }
                .Select(k => k.GetPublicAddress())
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var receipt = (AATransactionReceipt)await accountService.InstallSocialRecoveryAndWaitForReceiptAsync(
                d.Modules.SocialRecovery, threshold: 1, guardians: guardians);
            Assert.True(receipt.UserOpSuccess, receipt.FailureDiagnostic);

            Assert.True(await IsInstalledAsync(accountService, d.Modules.SocialRecovery));
        }

        private static Task<bool> IsInstalledAsync(NethereumAccountService accountService, string moduleAddress) =>
            accountService.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, moduleAddress, Array.Empty<byte>());
    }
}
