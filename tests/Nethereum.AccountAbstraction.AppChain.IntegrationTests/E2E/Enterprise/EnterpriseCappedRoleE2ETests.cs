using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.ABI;
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Modernization;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Enterprise
{
    [Collection(AppChainModularBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "E2E-AppChainModernization")]
    [Trait("UseCase", "EnterpriseTier1CappedRole")]
    public class EnterpriseCappedRoleE2ETests
    {
        private const int Cap = 100;
        private const int WithinCapValue = 60;
        private const int OverCapValue = 150;

        private readonly AppChainModularBundlerFixture _fixture;

        public EnterpriseCappedRoleE2ETests(AppChainModularBundlerFixture fixture)
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
        public async Task Given_an_enrolled_account_When_the_owner_installs_a_tier1_capped_operator_role_Then_within_cap_deposits_execute_and_over_cap_deposits_are_rejected_by_the_policy()
        {
            var d = _fixture.Deployment;
            var client = BuildClient();

            var owner = EthECKey.GenerateKey();
            var accountConfig = new AppChainAccountConfig { Owner = owner.GetPublicAddress(), Salt = 21 };
            var accountAddress = await _fixture.AdminService.EnrollAccountAsync(accountConfig);
            Assert.True(await _fixture.AdminService.IsAccountDeployedAsync(accountAddress));
            Assert.True(await _fixture.AdminService.IsActiveAsync(accountAddress));

            await _fixture.Node.SetBalanceAsync(accountAddress, Nethereum.Web3.Web3.Convert.ToWei(10));

            var salt = new byte[32];
            salt[31] = 21;
            var account = await client.CreateAccountAsync(owner, salt);
            Assert.Equal(accountAddress.ToLowerInvariant(), account.Address.ToLowerInvariant());

            var operatorKey = EthECKey.GenerateKey();
            var capConfigInitData = new ABIEncode().GetABIEncoded(
                new ABIValue("bytes32", _fixture.CapRuleId),
                new ABIValue("uint256", (BigInteger)Cap));
            var depositSelector = new DepositFunction().GetCallData();

            var sessionSalt = new byte[32];
            sessionSalt[31] = 22;

            var sessionConfig = new SmartSessionConfig()
                .WithSessionValidator(d.Modules.EcdsaSessionValidator)
                .WithSessionValidatorInitData(operatorKey.GetPublicAddress())
                .WithSalt(sessionSalt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(_fixture.PayableTarget.ContractAddress)
                    .WithSelector(depositSelector)
                    .WithPolicy(_fixture.ValueCapCombinator.ContractAddress, capConfigInitData)
                    .Build());

            var session = sessionConfig.ToSession();
            var smartSessionQuery = new SmartSessionService(_fixture.OperatorWeb3, d.Modules.SmartSession);
            var permissionId = await smartSessionQuery.GetPermissionIdQueryAsync(session);

            sessionConfig.ModuleAddress = d.Modules.SmartSession;
            var accountService = new NethereumAccountService(_fixture.OperatorWeb3, accountAddress);
            accountService.UseAccountAbstraction(account, client);
            var installReceipt = (AATransactionReceipt)await accountService.InstallModuleAndWaitForReceiptAsync(sessionConfig);
            Assert.True(installReceipt.UserOpSuccess, installReceipt.FailureDiagnostic);
            Assert.True(await accountService.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, d.Modules.SmartSession, Array.Empty<byte>()));

            var operatorAccount = client.GetAccount(
                accountAddress,
                new SmartSessionKeySigningService(operatorKey, permissionId),
                new SmartSessionValidatorModule(d.Modules.SmartSession, permissionId));

            var payableTarget = new PayableTargetService(_fixture.OperatorWeb3, _fixture.PayableTarget.ContractAddress);
            payableTarget.UseAccountAbstraction(operatorAccount, client);

            var balanceBefore = await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(_fixture.PayableTarget.ContractAddress);
            var withinCapReceipt = (AATransactionReceipt)await payableTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = WithinCapValue });
            Assert.True(withinCapReceipt.UserOpSuccess, withinCapReceipt.FailureDiagnostic);
            var balanceAfterWithinCap = await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(_fixture.PayableTarget.ContractAddress);
            Assert.Equal(balanceBefore.Value + WithinCapValue, balanceAfterWithinCap.Value);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => payableTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = OverCapValue }));
            Assert.False(ex is Xunit.Sdk.XunitException,
                $"Expected a SmartSession on-chain rejection, but a test assertion threw instead: {ex}");

            var messages = FlattenMessages(ex);
            Assert.True(messages.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected an AA23 validation-revert rejection (cap policy), but got: {ex}");

            var policyViolationSelector = Sha3Keccack.Current.CalculateHash(
                Encoding.UTF8.GetBytes("PolicyViolation(bytes32,address)"))[..4].ToHex(true);
            Assert.True(messages.IndexOf(policyViolationSelector, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected the SmartSession PolicyViolation(bytes32,address) selector {policyViolationSelector} (cap policy) in the rejection, but got: {ex}");

            var balanceAfterOverCap = await _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(_fixture.PayableTarget.ContractAddress);
            Assert.Equal(balanceAfterWithinCap.Value, balanceAfterOverCap.Value);
        }

        private static string FlattenMessages(Exception ex)
        {
            var builder = new StringBuilder();
            for (var current = ex; current != null; current = current.InnerException)
                builder.AppendLine(current.Message);
            return builder.ToString();
        }
    }
}
