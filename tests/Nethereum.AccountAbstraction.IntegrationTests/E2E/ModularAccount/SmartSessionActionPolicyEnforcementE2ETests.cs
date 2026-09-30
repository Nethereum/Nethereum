using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [Collection(SmartSessionPolicyBundlerCollection.COLLECTION_NAME)]
    public class SmartSessionActionPolicyEnforcementE2ETests
    {
        private const int GasWasterCap = 3;
        private const int WithinCapRepeat = 3;
        private const int OverCapRepeat = 4;

        private readonly SmartSessionPolicyBundlerFixture _fixture;

        public SmartSessionActionPolicyEnforcementE2ETests(SmartSessionPolicyBundlerFixture fixture)
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
        [Trait("Category", "E2E-SmartSession")]
        [Trait("UseCase", "SessionKeyActionPolicy")]
        public async Task Given_a_session_key_scoped_by_action_policies_When_it_sends_userOps_Then_only_in_scope_under_cap_actions_execute_and_out_of_scope_or_over_cap_are_rejected_on_chain()
        {
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            await _fixture.Bootstrap.Node.SetBalanceAsync(
                account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));

            var counter = new TestCounterService(
                _fixture.Bootstrap.OperatorWeb3, _fixture.TestCounterService.ContractHandler.ContractAddress);
            counter.UseAccountAbstraction(account, client);
            var deployReceipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync();
            Assert.True(deployReceipt.UserOpSuccess, deployReceipt.FailureDiagnostic);
            Assert.Equal(BigInteger.One, await counter.CountersQueryAsync(account.Address));

            var sessionKey = EthECKey.GenerateKey();

            var counterAddress = counter.ContractHandler.ContractAddress;
            var countSelector = new CountFunction().GetCallData();
            var gasWasterSelector = new GasWasterFunction { Repeat = 0, ReturnValue2 = string.Empty }.GetCallData()[..4];
            var gasWasterCapInitData = new UniActionPolicyBuilder()
                .WithValueLimit(BigInteger.Zero)
                .WithMaxValue(0, new BigInteger(GasWasterCap).ToByteArray(isUnsigned: true, isBigEndian: true))
                .Build();

            var salt = new byte[32];
            salt[31] = 1;

            var sessionConfig = new SmartSessionConfig()
                .WithSessionValidator(_fixture.SessionValidatorService.ContractAddress)
                .WithSessionValidatorInitData(sessionKey.GetPublicAddress())
                .WithSalt(salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(counterAddress)
                    .WithSelector(countSelector)
                    .WithSudoPolicy(_fixture.SudoPolicyService.ContractAddress)
                    .Build())
                .WithAction(new ActionDataBuilder()
                    .WithTarget(counterAddress)
                    .WithSelector(gasWasterSelector)
                    .WithUniActionPolicy(_fixture.UniActionPolicyService.ContractAddress, gasWasterCapInitData)
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

            var sessionAccount = client.GetAccount(
                account.Address,
                new SmartSessionKeySigningService(sessionKey, permissionId),
                new SmartSessionValidatorModule(_fixture.SmartSessionService.ContractAddress, permissionId));

            counter.UseAccountAbstraction(sessionAccount, client);
            var countReceipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync();
            Assert.True(countReceipt.UserOpSuccess, countReceipt.FailureDiagnostic);
            Assert.Equal(new BigInteger(2), await counter.CountersQueryAsync(account.Address));

            var withinCapReceipt = (AATransactionReceipt)await counter.GasWasterRequestAndWaitForReceiptAsync(
                WithinCapRepeat, string.Empty);
            Assert.True(withinCapReceipt.UserOpSuccess, withinCapReceipt.FailureDiagnostic);
            Assert.Equal(new BigInteger(WithinCapRepeat), await counter.OffsetQueryAsync());

            await AssertSessionOpRejectedAsync(() =>
                counter.GasWasterRequestAndWaitForReceiptAsync(OverCapRepeat, string.Empty));

            await AssertSessionOpRejectedAsync(() =>
                counter.JustemitRequestAndWaitForReceiptAsync());

            Assert.Equal(new BigInteger(WithinCapRepeat), await counter.OffsetQueryAsync());
            Assert.Equal(new BigInteger(2), await counter.CountersQueryAsync(account.Address));
        }

        private static async Task AssertSessionOpRejectedAsync(Func<Task> sendSessionOp)
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(sendSessionOp);
            Assert.False(ex is Xunit.Sdk.XunitException,
                $"Expected a SmartSession on-chain rejection, but a test assertion threw instead: {ex}");

            var messages = FlattenMessages(ex);
            Assert.True(messages.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA23 validation-revert rejection, but got: {ex}");
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
