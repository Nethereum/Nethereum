using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [Collection(SmartSessionPolicyBundlerCollection.COLLECTION_NAME)]
    [Trait("Category", "E2E-SmartSession")]
    [Trait("UseCase", "SessionKeyNofMAuthority")]
    public class SmartSessionNofMAuthorityE2ETests
    {
        private readonly SmartSessionPolicyBundlerFixture _fixture;

        public SmartSessionNofMAuthorityE2ETests(SmartSessionPolicyBundlerFixture fixture)
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

        private static List<string> SortedAddresses(params EthECKey[] keys) =>
            MultiGuardianSignatureBlobBuilder.SortAddressesAscending(keys.Select(k => k.GetPublicAddress())).ToList();

        private async Task<(IAAClient client, NethereumSmartAccount account, TestCounterService counter, byte[] permissionId)>
            SetUpAccountWithNofMSessionAsync(byte saltByte, int threshold, IReadOnlyList<EthECKey> owners)
        {
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));

            var counter = new TestCounterService(
                _fixture.Bootstrap.OperatorWeb3, _fixture.TestCounterService.ContractHandler.ContractAddress);
            counter.UseAccountAbstraction(account, client);
            var deployReceipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync();
            Assert.True(deployReceipt.UserOpSuccess, deployReceipt.FailureDiagnostic);

            var sortedOwners = SortedAddresses(owners.ToArray());
            var sessionValidatorInitData = new OwnableValidatorConfig(
                _fixture.OwnableValidatorService.ContractAddress, threshold, sortedOwners.ToArray()).GetInitData();

            var salt = new byte[32];
            salt[31] = saltByte;

            var counterAddress = counter.ContractHandler.ContractAddress;
            var countSelector = new CountFunction().GetCallData();

            var sessionConfig = new SmartSessionConfig()
                .WithSessionValidator(_fixture.OwnableValidatorService.ContractAddress)
                .WithSessionValidatorInitData(sessionValidatorInitData)
                .WithSalt(salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(counterAddress)
                    .WithSelector(countSelector)
                    .WithSudoPolicy(_fixture.SudoPolicyService.ContractAddress)
                    .Build());

            var session = sessionConfig.ToSession();
            var permissionId = await _fixture.SmartSessionService.GetPermissionIdQueryAsync(session);

            sessionConfig.ModuleAddress = _fixture.SmartSessionService.ContractAddress;
            var accountService = new NethereumAccountService(_fixture.Bootstrap.OperatorWeb3, account.Address);
            accountService.UseAccountAbstraction(account, client);
            var installReceipt = (AATransactionReceipt)await accountService.InstallModuleAndWaitForReceiptAsync(sessionConfig);
            Assert.True(installReceipt.UserOpSuccess, installReceipt.FailureDiagnostic);
            Assert.True(await accountService.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, _fixture.SmartSessionService.ContractAddress, Array.Empty<byte>()));

            return (client, account, counter, permissionId);
        }

        [Fact]
        [Trait("UseCase", "SessionKeyNofMAuthority")]
        public async Task Given_a_2of3_member_quorum_When_it_meets_threshold_Then_the_session_userOp_executes_on_chain()
        {
            var owners = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            const int threshold = 2;

            var (client, account, counter, permissionId) =
                await SetUpAccountWithNofMSessionAsync(1, threshold, owners);

            var signingService = new OwnableValidatorSessionSigningService(permissionId, new[] { owners[0], owners[1] }, threshold);
            var validatorModule = BuildValidatorModule(permissionId, estimationSlotCount: threshold);
            var sessionAccount = client.GetAccount(account.Address, signingService, validatorModule);

            counter.UseAccountAbstraction(sessionAccount, client);
            var receipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync();

            Assert.True(receipt.UserOpSuccess, receipt.FailureDiagnostic);
            Assert.Equal(new BigInteger(2), await counter.CountersQueryAsync(account.Address));
        }

        [Fact]
        [Trait("UseCase", "SessionKeyNofMAuthority")]
        public async Task Given_only_1of3_members_When_below_threshold_Then_the_session_userOp_is_rejected_on_chain_with_InvalidSignature()
        {
            var owners = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            const int threshold = 2;

            var (client, account, counter, permissionId) =
                await SetUpAccountWithNofMSessionAsync(2, threshold, owners);

            var signingService = BuildNofMSigningService(
                permissionId, hash => MultiGuardianSignatureBlobBuilder.BuildFromFinalHash(hash, new[] { owners[0] }, 1));
            var validatorModule = BuildValidatorModule(permissionId, estimationSlotCount: 1);
            var sessionAccount = client.GetAccount(account.Address, signingService, validatorModule);

            counter.UseAccountAbstraction(sessionAccount, client);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => counter.CountRequestAndWaitForReceiptAsync());
            var messages = FlattenMessages(ex);
            Assert.True(messages.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA23 validation-revert rejection, but got: {ex}");

            var invalidSignatureSelector = Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes("InvalidSignature()"))[..4].ToHex(true);
            Assert.True(messages.IndexOf(invalidSignatureSelector, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected the CheckSignatures.sol InvalidSignature() selector {invalidSignatureSelector} in the rejection, but got: {ex}");

            Assert.Equal(BigInteger.One, await counter.CountersQueryAsync(account.Address));
        }

        [Fact]
        [Trait("UseCase", "SessionKeyNofMAuthority")]
        public async Task Given_2_signatures_one_from_a_non_member_When_only_1_valid_signer_is_below_threshold_Then_the_session_userOp_is_rejected_on_chain()
        {
            var owners = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            const int threshold = 2;

            var (client, account, counter, permissionId) =
                await SetUpAccountWithNofMSessionAsync(3, threshold, owners);

            var nonMember = EthECKey.GenerateKey();
            var signingService = BuildNofMSigningService(
                permissionId, hash => MultiGuardianSignatureBlobBuilder.BuildFromFinalHash(hash, new[] { owners[0], nonMember }, threshold));
            var validatorModule = BuildValidatorModule(permissionId, estimationSlotCount: threshold);
            var sessionAccount = client.GetAccount(account.Address, signingService, validatorModule);

            counter.UseAccountAbstraction(sessionAccount, client);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => counter.CountRequestAndWaitForReceiptAsync());
            AssertOnChainSignatureRejection(ex);

            Assert.Equal(BigInteger.One, await counter.CountersQueryAsync(account.Address));
        }

        [Fact]
        [Trait("UseCase", "SessionKeyNofMAuthority")]
        public async Task Given_2_signatures_from_the_same_member_When_duplicated_Then_the_session_userOp_is_rejected_on_chain()
        {
            var owners = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            const int threshold = 2;

            var (client, account, counter, permissionId) =
                await SetUpAccountWithNofMSessionAsync(4, threshold, owners);

            var signingService = BuildNofMSigningService(permissionId, hash =>
            {
                var singleSlot = MultiGuardianSignatureBlobBuilder.BuildFromFinalHash(hash, new[] { owners[0] }, 1);
                return ByteUtil.Merge(singleSlot, singleSlot);
            });
            var validatorModule = BuildValidatorModule(permissionId, estimationSlotCount: threshold);
            var sessionAccount = client.GetAccount(account.Address, signingService, validatorModule);

            counter.UseAccountAbstraction(sessionAccount, client);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => counter.CountRequestAndWaitForReceiptAsync());
            AssertOnChainSignatureRejection(ex);

            Assert.Equal(BigInteger.One, await counter.CountersQueryAsync(account.Address));
        }

        private static void AssertOnChainSignatureRejection(Exception ex)
        {
            Assert.False(ex is Xunit.Sdk.XunitException, $"Expected a SmartSession on-chain rejection, but a test assertion threw instead: {ex}");

            if (ex is BundlerRpcException bundlerEx)
            {
                Assert.Equal(BundlerErrorCodes.InvalidSignature, bundlerEx.Code);
                Assert.Contains("AA24", bundlerEx.Message, StringComparison.OrdinalIgnoreCase);
                return;
            }

            var messages = FlattenMessages(ex);
            Assert.True(messages.IndexOf("AA24", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA24 signature-error rejection, but got: {ex}");
        }

        private static string FlattenMessages(Exception ex)
        {
            var builder = new StringBuilder();
            for (var current = ex; current != null; current = current.InnerException)
                builder.AppendLine(current.Message);
            return builder.ToString();
        }

        private static IAccountSigningService BuildNofMSigningService(byte[] permissionId, Func<byte[], byte[]> buildBlob) =>
            new OwnableValidatorSessionSigningService(permissionId, buildBlob);

        private IErc7579ValidatorModule BuildValidatorModule(byte[] permissionId, int estimationSlotCount) =>
            new OwnableValidatorSessionValidatorModule(_fixture.SmartSessionService.ContractAddress, permissionId, estimationSlotCount);
    }
}
