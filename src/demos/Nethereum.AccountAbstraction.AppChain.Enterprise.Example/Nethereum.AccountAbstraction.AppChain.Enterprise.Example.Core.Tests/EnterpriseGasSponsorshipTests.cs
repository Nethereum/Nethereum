using System.Numerics;
using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget;
using Nethereum.Contracts;
using Nethereum.Signer;
using Xunit;
using AccountNotActiveError = Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster.ContractDefinition.AccountNotActiveError;
using DepositFunction = Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition.DepositFunction;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core.Tests
{
    [Collection(EnterpriseDemoCollection.COLLECTION_NAME)]
    public class EnterpriseGasSponsorshipTests
    {
        private readonly EnterpriseDemoFixture _fixture;

        public EnterpriseGasSponsorshipTests(EnterpriseDemoFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        [Trait("UseCase", "EnterpriseGasSponsorship")]
        public async Task Given_a_never_funded_enrolled_account_When_the_owner_installs_a_capped_role_and_pays_within_cap_Then_the_paymaster_sponsors_gas_and_the_accounts_balance_stays_zero()
        {
            var session = _fixture.NewReadySession();
            var adminVm = new EnterpriseAdminViewModel(session) { UserId = "sponsor-positive", InitialFundingEth = 0m };
            await adminVm.EnrollCommand.ExecuteAsync(null);
            Assert.Null(adminVm.ErrorMessage);
            var accountAddress = adminVm.ResolvedAddress!;

            var balanceBeforeInstall = await session.Web3!.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, balanceBeforeInstall.Value);

            var paymaster = new SponsoredPaymasterService(session.Web3!, session.Deployment!.SponsoredPaymasterAddress);
            var paymasterDepositBefore = await paymaster.GetDepositQueryAsync();

            var vm = new EnterpriseOperatorViewModel(session) { Cap = 100, WithinCapAmount = 0 };

            await vm.InstallCappedRoleCommand.ExecuteAsync(null);
            Assert.True(vm.HasCappedRole, vm.ErrorMessage ?? vm.StatusMessage);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);

            var balanceAfterInstall = await session.Web3!.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, balanceAfterInstall.Value);

            await vm.PayWithinCapCommand.ExecuteAsync(null);
            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);

            var balanceAfterPay = await session.Web3!.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, balanceAfterPay.Value);

            var paymasterDepositAfter = await paymaster.GetDepositQueryAsync();
            Assert.True(paymasterDepositAfter < paymasterDepositBefore,
                $"Expected the SponsoredPaymaster's EntryPoint deposit to decrease after sponsoring gas for " +
                $"the install + pay, but it went from {paymasterDepositBefore} to {paymasterDepositAfter}.");
        }

        [Fact]
        [Trait("UseCase", "EnterpriseGasSponsorship")]
        public async Task Given_an_account_the_registry_does_not_know_When_it_submits_a_sponsored_userOp_Then_the_paymaster_rejects_it_as_AccountNotActive()
        {
            var session = _fixture.NewReadySession();

            var strangerKey = EthECKey.GenerateKey();
            var strangerSalt = new byte[32];
            strangerSalt[31] = 77;
            var strangerAccount = await session.Client!.CreateAccountAsync(strangerKey, strangerSalt);

            var payableTarget = new PayableTargetService(session.Web3!, session.PayableTargetAddress!);
            var handler = session.Client!.Configure(payableTarget, strangerAccount);
            handler.WithPaymaster(session.Deployment!.SponsoredPaymasterAddress);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                payableTarget.DepositRequestAndWaitForReceiptAsync(new DepositFunction { AmountToSend = 0 }));

            Assert.False(ex is Xunit.Sdk.XunitException,
                $"Expected the SponsoredPaymaster to reject the op on-chain, but a test assertion threw instead: {ex}");
            Assert.True(IsAccountNotActiveRejection(ex),
                $"Expected the SponsoredPaymaster's registry-gate rejection (AA34 paymaster signature error, or a " +
                $"decoded AccountNotActive() revert), but got: {ex}");
        }

        private static bool IsAccountNotActiveRejection(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is SmartContractCustomErrorRevertException customError &&
                    customError.IsCustomErrorFor<AccountNotActiveError>())
                    return true;
                if (current.Message.IndexOf("AA34", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
