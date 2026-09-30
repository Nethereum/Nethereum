using System;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.ERC7579.Modules.SocialRecovery;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules.SocialRecovery
{
    [Collection(ModularAccountBundlerCollection.COLLECTION_NAME)]
    [Trait("Category", "ERC7579-Module")]
    [Trait("Module", "SocialRecovery")]
    [Trait("Workflow", "Recovery")]
    public class SocialRecoveryWorkflowE2ETests
    {
        private readonly ModularAccountBundlerFixture _fixture;

        public SocialRecoveryWorkflowE2ETests(ModularAccountBundlerFixture fixture)
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

        private static string[] SortedAddresses(params EthECKey[] keys) =>
            MultiGuardianSignatureBlobBuilder.SortAddressesAscending(keys.Select(k => k.GetPublicAddress()));

        [Fact]
        [Trait("UseCase", "SocialRecovery")]
        [NethereumDocExample(DocSection.AccountAbstraction, "social-recovery", "Guardian quorum cosigns a transferOwnership recovery and rotates the owner", Order = 1)]
        public async Task Given_a_guardian_quorum_When_they_cosign_a_transferOwnership_recovery_Then_the_owner_rotates_and_the_new_owner_can_sign()
        {
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));

            var socialRecovery = await SocialRecoveryService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new SocialRecoveryDeployment());

            var guardianKeys = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            const int threshold = 2;

            var ownerAccountService = new NethereumAccountService(_fixture.Bootstrap.OperatorWeb3, account.Address);
            ownerAccountService.UseAccountAbstraction(account, client);
            var installReceipt = (AATransactionReceipt)await ownerAccountService.InstallSocialRecoveryAndWaitForReceiptAsync(
                socialRecovery.ContractAddress, threshold, SortedAddresses(guardianKeys));
            Assert.True(installReceipt.UserOpSuccess, installReceipt.FailureDiagnostic);

            var guardianSigningService = new MultiGuardianSigningService(guardianKeys, threshold);
            var socialRecoveryValidator = new SocialRecoveryValidatorModule(socialRecovery.ContractAddress, threshold);
            var recoveryAccount = client.GetAccount(account.Address, guardianSigningService, socialRecoveryValidator);

            var newOwner = EthECKey.GenerateKey();

            var ecdsaValidatorService = new ECDSAValidatorService(_fixture.Bootstrap.OperatorWeb3, _fixture.EcdsaValidatorService.ContractAddress);
            ecdsaValidatorService.UseAccountAbstraction(recoveryAccount, client);
            var recoveryReceipt = (AATransactionReceipt)await ecdsaValidatorService.TransferOwnershipRequestAndWaitForReceiptAsync(newOwner.GetPublicAddress());
            Assert.True(recoveryReceipt.UserOpSuccess, recoveryReceipt.FailureDiagnostic);

            var rotatedOwner = await _fixture.EcdsaValidatorService.GetOwnerQueryAsync(account.Address);
            Assert.Equal(newOwner.GetPublicAddress().ToLower(), rotatedOwner.ToLower());

            var newOwnerAccount = client.GetAccount(
                account.Address,
                new AccountSigningOfflineService(newOwner),
                new EcdsaValidatorModule(_fixture.EcdsaValidatorService.ContractAddress));

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());
            testCounter.UseAccountAbstraction(newOwnerAccount, client);
            var counterReceipt = (AATransactionReceipt)await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(counterReceipt.UserOpSuccess, counterReceipt.FailureDiagnostic);

            var count = await testCounter.CountersQueryAsync(account.Address);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("UseCase", "SocialRecovery")]
        [NethereumDocExample(DocSection.AccountAbstraction, "social-recovery", "Under-threshold guardian quorum is rejected and the owner is unchanged", Order = 2)]
        public async Task Given_only_M_minus_1_guardians_cosign_When_they_attempt_recovery_Then_it_is_rejected_and_the_owner_is_unchanged()
        {
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));

            var socialRecovery = await SocialRecoveryService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new SocialRecoveryDeployment());

            var guardianKeys = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            const int threshold = 2;

            var ownerAccountService = new NethereumAccountService(_fixture.Bootstrap.OperatorWeb3, account.Address);
            ownerAccountService.UseAccountAbstraction(account, client);
            var installReceipt = (AATransactionReceipt)await ownerAccountService.InstallSocialRecoveryAndWaitForReceiptAsync(
                socialRecovery.ContractAddress, threshold, SortedAddresses(guardianKeys));
            Assert.True(installReceipt.UserOpSuccess, installReceipt.FailureDiagnostic);

            var partialGuardianKeys = guardianKeys.Take(threshold - 1).ToArray();
            var partialThreshold = partialGuardianKeys.Length;

            var partialSigningService = new MultiGuardianSigningService(partialGuardianKeys, partialThreshold);
            var partialValidator = new SocialRecoveryValidatorModule(socialRecovery.ContractAddress, partialThreshold);
            var partialRecoveryAccount = client.GetAccount(account.Address, partialSigningService, partialValidator);

            var newOwner = EthECKey.GenerateKey();

            var ecdsaValidatorService = new ECDSAValidatorService(_fixture.Bootstrap.OperatorWeb3, _fixture.EcdsaValidatorService.ContractAddress);
            ecdsaValidatorService.UseAccountAbstraction(partialRecoveryAccount, client);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ecdsaValidatorService.TransferOwnershipRequestAndWaitForReceiptAsync(newOwner.GetPublicAddress()));
            var rpcException = Assert.IsType<RpcResponseException>(ex.InnerException);
            Assert.Equal(BundlerErrorCodes.SimulateValidation, rpcException.RpcError.Code);
            Assert.Contains("AA23", ex.Message);

            var invalidSignatureSelector = Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes("InvalidSignature()"))[..4].ToHex(true);
            Assert.Contains(invalidSignatureSelector, ex.Message, StringComparison.OrdinalIgnoreCase);

            var ownerAfter = await _fixture.EcdsaValidatorService.GetOwnerQueryAsync(account.Address);
            Assert.Equal(owner.GetPublicAddress().ToLower(), ownerAfter.ToLower());
        }

        [Fact]
        [Trait("UseCase", "SocialRecovery")]
        [NethereumDocExample(DocSection.AccountAbstraction, "social-recovery", "Non-guardian quorum is rejected and the owner is unchanged", Order = 3)]
        public async Task Given_a_non_guardian_quorum_When_they_attempt_recovery_Then_it_is_rejected_and_the_owner_is_unchanged()
        {
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));

            var socialRecovery = await SocialRecoveryService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new SocialRecoveryDeployment());

            var guardianKeys = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            const int threshold = 2;

            var ownerAccountService = new NethereumAccountService(_fixture.Bootstrap.OperatorWeb3, account.Address);
            ownerAccountService.UseAccountAbstraction(account, client);
            var installReceipt = (AATransactionReceipt)await ownerAccountService.InstallSocialRecoveryAndWaitForReceiptAsync(
                socialRecovery.ContractAddress, threshold, SortedAddresses(guardianKeys));
            Assert.True(installReceipt.UserOpSuccess, installReceipt.FailureDiagnostic);

            var nonGuardianKeys = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            var nonGuardianSigningService = new MultiGuardianSigningService(nonGuardianKeys, threshold);
            var nonGuardianValidator = new SocialRecoveryValidatorModule(socialRecovery.ContractAddress, threshold);
            var nonGuardianRecoveryAccount = client.GetAccount(account.Address, nonGuardianSigningService, nonGuardianValidator);

            var newOwner = EthECKey.GenerateKey();

            var ecdsaValidatorService = new ECDSAValidatorService(_fixture.Bootstrap.OperatorWeb3, _fixture.EcdsaValidatorService.ContractAddress);
            ecdsaValidatorService.UseAccountAbstraction(nonGuardianRecoveryAccount, client);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(() =>
                ecdsaValidatorService.TransferOwnershipRequestAndWaitForReceiptAsync(newOwner.GetPublicAddress()));
            Assert.Equal(BundlerErrorCodes.InvalidSignature, ex.Code);
            Assert.Contains("AA24", ex.Message);

            var ownerAfter = await _fixture.EcdsaValidatorService.GetOwnerQueryAsync(account.Address);
            Assert.Equal(owner.GetPublicAddress().ToLower(), ownerAfter.ToLower());
        }
    }
}
