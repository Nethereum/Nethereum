using System.Numerics;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    [Collection(ExampleCollection.COLLECTION_NAME)]
    public class AccountPaymentModeTests
    {
        private readonly ExampleFixture _fixture;

        public AccountPaymentModeTests(ExampleFixture fixture)
        {
            _fixture = fixture;
        }

        private SessionState NewSession() => _fixture.NewReadySession();

        [Fact]
        [Trait("UseCase", "PaymentMode")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#payment-mode-send")]
        public async Task Given_a_paymaster_sponsored_account_created_in_Setup_When_Send_is_used_Then_the_paymaster_pays_and_the_balance_stays_zero()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session) { SelectedPaymentMode = AccountPaymentMode.PaymasterSponsored };

            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);

            Assert.Null(setupViewModel.ErrorMessage);
            Assert.Equal(AccountPaymentMode.PaymasterSponsored, session.PaymentMode);
            var accountAddress = session.Account!.Address;
            var balanceBefore = await _fixture.TestCounter.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, balanceBefore.Value);

            var interactionViewModel = new InteractionViewModel(session);
            await interactionViewModel.SendCountCommand.ExecuteAsync(null);

            Assert.Null(interactionViewModel.ErrorMessage);
            Assert.True(interactionViewModel.LastReceipt!.UserOpSuccess, interactionViewModel.LastReceipt.FailureDiagnostic);
            var balanceAfter = await _fixture.TestCounter.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, balanceAfter.Value);
        }

        [Fact]
        [Trait("UseCase", "PaymentMode")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#payment-mode-batch")]
        public async Task Given_a_paymaster_sponsored_account_created_in_Setup_When_Batch_is_used_Then_the_paymaster_pays_and_the_balance_stays_zero()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session) { SelectedPaymentMode = AccountPaymentMode.PaymasterSponsored };
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);
            Assert.Equal(AccountPaymentMode.PaymasterSponsored, session.PaymentMode);
            var accountAddress = session.Account!.Address;

            var batchViewModel = new BatchViewModel(session);
            await batchViewModel.SendBatchCommand.ExecuteAsync(null);

            Assert.Null(batchViewModel.ErrorMessage);
            Assert.True(batchViewModel.LastReceipt!.UserOpSuccess, batchViewModel.LastReceipt.FailureDiagnostic);
            var balanceAfter = await _fixture.TestCounter.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, balanceAfter.Value);
        }

        [Fact]
        [Trait("UseCase", "PaymentMode")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#payment-mode-custom")]
        public async Task Given_a_paymaster_sponsored_account_created_in_Setup_When_Custom_contract_is_used_Then_the_paymaster_pays_and_the_balance_stays_zero()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session) { SelectedPaymentMode = AccountPaymentMode.PaymasterSponsored };
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);
            Assert.Equal(AccountPaymentMode.PaymasterSponsored, session.PaymentMode);
            var accountAddress = session.Account!.Address;
            var slot = new BigInteger(500);

            var customViewModel = new CustomViewModel(session);
            await customViewModel.BookCommand.ExecuteAsync(slot);

            Assert.Null(customViewModel.ErrorMessage);
            Assert.True(customViewModel.LastReceipt!.UserOpSuccess, customViewModel.LastReceipt.FailureDiagnostic);
            var balanceAfter = await _fixture.TestCounter.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, balanceAfter.Value);
        }

        [Fact]
        [Trait("UseCase", "PaymentMode")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#payment-mode-self-funded")]
        public async Task Given_a_self_funded_account_created_in_Setup_When_Send_is_used_Then_it_pays_its_own_gas_and_the_balance_decreases()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session);
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);
            Assert.Equal(AccountPaymentMode.SelfFunded, session.PaymentMode);
            var accountAddress = session.Account!.Address;
            await _fixture.FundAsync(accountAddress);
            var balanceBefore = await _fixture.TestCounter.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);

            var interactionViewModel = new InteractionViewModel(session);
            await interactionViewModel.SendCountCommand.ExecuteAsync(null);

            Assert.Null(interactionViewModel.ErrorMessage);
            Assert.True(interactionViewModel.LastReceipt!.UserOpSuccess, interactionViewModel.LastReceipt.FailureDiagnostic);
            var balanceAfter = await _fixture.TestCounter.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.True(balanceAfter.Value < balanceBefore.Value);
        }

        [Fact]
        [Trait("UseCase", "PaymentMode")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#payment-mode-banner")]
        public async Task Given_an_account_created_with_either_payment_mode_When_the_banner_reads_it_Then_DisplayText_reflects_the_mode()
        {
            var selfFundedSession = NewSession();
            var banner = new AccountBannerViewModel(selfFundedSession);
            var selfFundedSetup = new SetupViewModel(selfFundedSession);
            await selfFundedSetup.CreateAccountCommand.ExecuteAsync(null);
            Assert.EndsWith("· Self-funded", banner.DisplayText);

            var sponsoredSession = NewSession();
            var sponsoredBanner = new AccountBannerViewModel(sponsoredSession);
            var sponsoredSetup = new SetupViewModel(sponsoredSession) { SelectedPaymentMode = AccountPaymentMode.PaymasterSponsored };
            await sponsoredSetup.CreateAccountCommand.ExecuteAsync(null);
            Assert.EndsWith("· Paymaster-sponsored", sponsoredBanner.DisplayText);
        }

        [Fact]
        [Trait("UseCase", "PaymentMode")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#payment-mode-guard")]
        public async Task Given_no_paymaster_configured_When_Setup_creates_an_account_Then_PaymasterSponsored_is_unavailable_and_the_account_is_self_funded()
        {
            var session = NewSession();
            session.Paymaster = null;
            var setupViewModel = new SetupViewModel(session);

            Assert.False(setupViewModel.HasPaymaster);
            Assert.DoesNotContain(AccountPaymentMode.PaymasterSponsored, setupViewModel.AvailablePaymentModes);

            setupViewModel.SelectedPaymentMode = AccountPaymentMode.PaymasterSponsored;
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);

            Assert.Null(setupViewModel.ErrorMessage);
            Assert.Equal(AccountPaymentMode.SelfFunded, session.PaymentMode);

            await _fixture.FundAsync(session.Account!.Address);
            var interactionViewModel = new InteractionViewModel(session);
            await interactionViewModel.SendCountCommand.ExecuteAsync(null);
            Assert.Null(interactionViewModel.ErrorMessage);
            Assert.True(interactionViewModel.LastReceipt!.UserOpSuccess, interactionViewModel.LastReceipt.FailureDiagnostic);
        }

        [Fact]
        [Trait("UseCase", "PaymentMode")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#payment-mode-guard")]
        public void Given_no_paymaster_configured_When_the_Passkey_tab_is_shown_Then_PaymasterSponsored_is_unavailable()
        {
            var session = NewSession();
            session.Paymaster = null;
            var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: true);
            var passkeyViewModel = new PasskeyAccountViewModel(session, authenticator, authenticator);

            Assert.False(passkeyViewModel.HasPaymaster);
            Assert.DoesNotContain(AccountPaymentMode.PaymasterSponsored, passkeyViewModel.AvailablePaymentModes);
        }

        [Fact]
        [Trait("UseCase", "PaymentMode")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#payment-mode-guard")]
        public async Task Given_no_paymaster_configured_When_Passkey_creates_an_account_Then_PaymasterSponsored_is_unavailable_and_the_account_is_self_funded()
        {
            var session = NewSession();
            session.Paymaster = null;
            var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: true);
            var passkeyViewModel = new PasskeyAccountViewModel(session, authenticator, authenticator);

            Assert.False(passkeyViewModel.HasPaymaster);
            Assert.DoesNotContain(AccountPaymentMode.PaymasterSponsored, passkeyViewModel.AvailablePaymentModes);

            passkeyViewModel.SelectedPaymentMode = AccountPaymentMode.PaymasterSponsored;
            await passkeyViewModel.CreateAccountCommand.ExecuteAsync(null);

            Assert.Null(passkeyViewModel.ErrorMessage);
            Assert.Equal(AccountPaymentMode.SelfFunded, session.PaymentMode);

            await _fixture.FundAsync(session.Account!.Address);
            await passkeyViewModel.SendCommand.ExecuteAsync(null);
            Assert.Null(passkeyViewModel.ErrorMessage);
            Assert.True(passkeyViewModel.LastReceipt!.UserOpSuccess, passkeyViewModel.LastReceipt.FailureDiagnostic);
        }
    }
}
