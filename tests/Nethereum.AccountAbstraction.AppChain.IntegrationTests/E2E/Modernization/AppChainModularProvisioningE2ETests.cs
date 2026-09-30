using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Modernization
{
    /// <summary>
    /// Proves an AppChain account provisioned through <see cref="AppChainAccountAdminService"/> is a real
    /// modular ERC-7579 account whose modules are ACTUALLY installed - closing the declared-but-inert gap
    /// end to end. The admin enrolls a user (invite -> provision -> activate), then the owner (their own
    /// key, never the service) installs a SmartSession session policy and SocialRecovery guardians as
    /// userOps through the bundler. The owner validator is asserted installed from creation; SmartSession
    /// and SocialRecovery are asserted NOT installed before and installed after - the before/after flip is
    /// the twin that a "configured but not installed" account would fail.
    /// </summary>
    [Collection(AppChainModularBundlerFixture.COLLECTION_NAME)]
    public class AppChainModularProvisioningE2ETests
    {
        private readonly AppChainModularBundlerFixture _fixture;

        public AppChainModularProvisioningE2ETests(AppChainModularBundlerFixture fixture)
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
        public async Task Given_an_admin_provisions_a_user_When_the_owner_installs_modules_Then_the_account_is_modular_with_owner_session_and_recovery_actually_installed()
        {
            var d = _fixture.Deployment;
            var owner = EthECKey.GenerateKey();
            var accountConfig = new AppChainAccountConfig { Owner = owner.GetPublicAddress(), Salt = BigInteger.One };

            var accountAddress = await _fixture.AdminService.EnrollAccountAsync(accountConfig);
            Assert.True(await _fixture.AdminService.IsAccountDeployedAsync(accountAddress));
            Assert.True(await _fixture.AdminService.IsActiveAsync(accountAddress));

            var accountService = new NethereumAccountService(_fixture.OperatorWeb3, accountAddress);

            Assert.True(await IsInstalledAsync(accountService, d.Modules.EcdsaValidator));
            Assert.False(await IsInstalledAsync(accountService, d.Modules.SmartSession));
            Assert.False(await IsInstalledAsync(accountService, d.Modules.SocialRecovery));

            await _fixture.Node.SetBalanceAsync(
                accountAddress, Nethereum.Web3.Web3.Convert.ToWei(10));

            var client = BuildClient();
            var saltBytes = new byte[32];
            saltBytes[31] = 1;
            var account = await client.CreateAccountAsync(owner, saltBytes);
            Assert.Equal(accountAddress.ToLowerInvariant(), account.Address.ToLowerInvariant());

            accountService.UseAccountAbstraction(account, client);

            var sessionKey = EthECKey.GenerateKey();
            var sessionSalt = new byte[32];
            sessionSalt[31] = 2;
            var sessionConfig = new SmartSessionConfig()
                .WithSessionValidator(d.Modules.EcdsaSessionValidator)
                .WithSessionValidatorInitData(sessionKey.GetPublicAddress())
                .WithSalt(sessionSalt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(accountAddress)
                    .WithSelector(new byte[] { 0x00, 0x00, 0x00, 0x00 })
                    .WithSudoPolicy(d.Modules.SudoPolicy)
                    .Build());
            sessionConfig.ModuleAddress = d.Modules.SmartSession;
            var sessionReceipt = (AATransactionReceipt)await accountService.InstallModuleAndWaitForReceiptAsync(sessionConfig);
            Assert.True(sessionReceipt.UserOpSuccess, sessionReceipt.FailureDiagnostic);

            var guardians = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey() }
                .Select(k => k.GetPublicAddress())
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var recoveryReceipt = (AATransactionReceipt)await accountService.InstallSocialRecoveryAndWaitForReceiptAsync(
                d.Modules.SocialRecovery, threshold: 1, guardians: guardians);
            Assert.True(recoveryReceipt.UserOpSuccess, recoveryReceipt.FailureDiagnostic);

            Assert.True(await IsInstalledAsync(accountService, d.Modules.SmartSession));
            Assert.True(await IsInstalledAsync(accountService, d.Modules.SocialRecovery));
        }

        private static Task<bool> IsInstalledAsync(NethereumAccountService accountService, string moduleAddress) =>
            accountService.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, moduleAddress, Array.Empty<byte>());
    }
}
