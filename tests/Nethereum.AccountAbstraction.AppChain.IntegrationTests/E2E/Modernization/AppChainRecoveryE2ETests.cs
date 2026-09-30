using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.ERC7579.Modules.SocialRecovery;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Modernization
{
    [Collection(AppChainModularBundlerFixture.COLLECTION_NAME)]
    public class AppChainRecoveryE2ETests
    {
        private const int GuardianThreshold = 2;
        private const int GuardianCount = 3;

        private readonly AppChainModularBundlerFixture _fixture;

        public AppChainRecoveryE2ETests(AppChainModularBundlerFixture fixture)
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
        public async Task Given_an_enrolled_account_with_guardians_When_a_quorum_rotates_the_owner_Then_recovery_preserves_the_enrollment_and_the_new_owner_controls_it()
        {
            var d = _fixture.Deployment;
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var accountConfig = new AppChainAccountConfig { Owner = owner.GetPublicAddress(), Salt = 11 };
            var accountAddress = await _fixture.AdminService.EnrollAccountAsync(accountConfig);
            Assert.True(await _fixture.AdminService.IsActiveAsync(accountAddress));

            await _fixture.Node.SetBalanceAsync(accountAddress, Nethereum.Web3.Web3.Convert.ToWei(10));

            var salt = new byte[32];
            salt[31] = 11;
            var account = await client.CreateAccountAsync(owner, salt);
            Assert.Equal(accountAddress.ToLowerInvariant(), account.Address.ToLowerInvariant());
            var accountService = new NethereumAccountService(_fixture.OperatorWeb3, accountAddress);
            accountService.UseAccountAbstraction(account, client);

            var guardianKeys = Enumerable.Range(0, GuardianCount).Select(_ => EthECKey.GenerateKey()).ToArray();
            var sortedGuardians = MultiGuardianSignatureBlobBuilder.SortAddressesAscending(
                guardianKeys.Select(k => k.GetPublicAddress()));
            var installReceipt = (AATransactionReceipt)await accountService.InstallSocialRecoveryAndWaitForReceiptAsync(
                d.Modules.SocialRecovery, GuardianThreshold, sortedGuardians);
            Assert.True(installReceipt.UserOpSuccess, installReceipt.FailureDiagnostic);

            var ecdsaValidator = new ECDSAValidatorService(_fixture.OperatorWeb3, d.Modules.EcdsaValidator);
            Assert.Equal(owner.GetPublicAddress().ToLowerInvariant(),
                (await ecdsaValidator.GetOwnerQueryAsync(accountAddress)).ToLowerInvariant());

            var newOwner = EthECKey.GenerateKey();
            var recoveryAccount = client.GetAccount(
                accountAddress,
                new MultiGuardianSigningService(guardianKeys, GuardianThreshold),
                new SocialRecoveryValidatorModule(d.Modules.SocialRecovery, GuardianThreshold));
            var recoveryEcdsa = new ECDSAValidatorService(_fixture.OperatorWeb3, d.Modules.EcdsaValidator);
            recoveryEcdsa.UseAccountAbstraction(recoveryAccount, client);
            var recoveryReceipt = (AATransactionReceipt)await recoveryEcdsa.TransferOwnershipRequestAndWaitForReceiptAsync(
                newOwner.GetPublicAddress());
            Assert.True(recoveryReceipt.UserOpSuccess, recoveryReceipt.FailureDiagnostic);

            Assert.Equal(newOwner.GetPublicAddress().ToLowerInvariant(),
                (await ecdsaValidator.GetOwnerQueryAsync(accountAddress)).ToLowerInvariant());
            Assert.True(await _fixture.AdminService.IsActiveAsync(accountAddress));

            var thirdOwner = EthECKey.GenerateKey();
            var recoveredAccount = client.GetAccount(
                accountAddress,
                new AccountSigningOfflineService(newOwner),
                new EcdsaValidatorModule(d.Modules.EcdsaValidator));
            var recoveredEcdsa = new ECDSAValidatorService(_fixture.OperatorWeb3, d.Modules.EcdsaValidator);
            recoveredEcdsa.UseAccountAbstraction(recoveredAccount, client);
            var controlReceipt = (AATransactionReceipt)await recoveredEcdsa.TransferOwnershipRequestAndWaitForReceiptAsync(
                thirdOwner.GetPublicAddress());
            Assert.True(controlReceipt.UserOpSuccess, controlReceipt.FailureDiagnostic);
            Assert.Equal(thirdOwner.GetPublicAddress().ToLowerInvariant(),
                (await ecdsaValidator.GetOwnerQueryAsync(accountAddress)).ToLowerInvariant());
        }

        [Fact]
        [Trait("Category", "E2E-AppChainModernization")]
        public async Task Given_a_2_of_3_guardian_quorum_When_only_one_guardian_signs_Then_recovery_is_rejected_on_chain_and_the_owner_is_unchanged()
        {
            var d = _fixture.Deployment;
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var accountConfig = new AppChainAccountConfig { Owner = owner.GetPublicAddress(), Salt = 12 };
            var accountAddress = await _fixture.AdminService.EnrollAccountAsync(accountConfig);
            await _fixture.Node.SetBalanceAsync(accountAddress, Nethereum.Web3.Web3.Convert.ToWei(10));

            var salt = new byte[32];
            salt[31] = 12;
            var account = await client.CreateAccountAsync(owner, salt);
            Assert.Equal(accountAddress.ToLowerInvariant(), account.Address.ToLowerInvariant());
            var accountService = new NethereumAccountService(_fixture.OperatorWeb3, accountAddress);
            accountService.UseAccountAbstraction(account, client);

            var guardianKeys = Enumerable.Range(0, GuardianCount).Select(_ => EthECKey.GenerateKey()).ToArray();
            var sortedGuardians = MultiGuardianSignatureBlobBuilder.SortAddressesAscending(
                guardianKeys.Select(k => k.GetPublicAddress()));
            var installReceipt = (AATransactionReceipt)await accountService.InstallSocialRecoveryAndWaitForReceiptAsync(
                d.Modules.SocialRecovery, GuardianThreshold, sortedGuardians);
            Assert.True(installReceipt.UserOpSuccess, installReceipt.FailureDiagnostic);

            var ecdsaValidator = new ECDSAValidatorService(_fixture.OperatorWeb3, d.Modules.EcdsaValidator);
            var ownerBefore = await ecdsaValidator.GetOwnerQueryAsync(accountAddress);

            var oneGuardian = guardianKeys.Take(1).ToArray();
            var underThresholdAccount = client.GetAccount(
                accountAddress,
                new MultiGuardianSigningService(oneGuardian, 1),
                new SocialRecoveryValidatorModule(d.Modules.SocialRecovery, 1));
            var underThresholdEcdsa = new ECDSAValidatorService(_fixture.OperatorWeb3, d.Modules.EcdsaValidator);
            underThresholdEcdsa.UseAccountAbstraction(underThresholdAccount, client);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                underThresholdEcdsa.TransferOwnershipRequestAndWaitForReceiptAsync(EthECKey.GenerateKey().GetPublicAddress()));

            var messages = FlattenMessages(ex);
            Assert.True(messages.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA23 validation-revert rejection, but got: {ex}");
            var invalidSignatureSelector = Sha3Keccack.Current
                .CalculateHash(System.Text.Encoding.UTF8.GetBytes("InvalidSignature()"))[..4].ToHex(true);
            Assert.True(messages.IndexOf(invalidSignatureSelector, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected the InvalidSignature() quorum length-guard revert ({invalidSignatureSelector}), but got: {ex}");

            Assert.Equal(ownerBefore.ToLowerInvariant(),
                (await ecdsaValidator.GetOwnerQueryAsync(accountAddress)).ToLowerInvariant());
        }

        private static string FlattenMessages(Exception ex)
        {
            var builder = new System.Text.StringBuilder();
            for (var current = ex; current != null; current = current.InnerException)
                builder.AppendLine(current.Message);
            return builder.ToString();
        }
    }
}
