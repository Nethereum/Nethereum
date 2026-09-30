using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    [Collection(ExampleCollection.COLLECTION_NAME)]
    public class DiagnosticsViewModelTests
    {
        private readonly ExampleFixture _fixture;

        public DiagnosticsViewModelTests(ExampleFixture fixture)
        {
            _fixture = fixture;
        }

        private SessionState NewSession() => _fixture.NewReadySession();

        private async Task<SessionState> NewFundedSessionAsync()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session);
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);
            await _fixture.FundAsync(session.Account!.Address);
            return session;
        }

        [Fact]
        [Trait("UseCase", "Diagnostics")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#diagnostics-success")]
        public async Task Given_a_funded_account_When_the_user_runs_the_diagnosed_op_Then_real_gas_bundler_lookup_and_packed_fields_are_populated()
        {
            var session = await NewFundedSessionAsync();
            var diagnostics = new DiagnosticsViewModel(session);

            await diagnostics.RunDiagnosedOpCommand.ExecuteAsync(null);

            Assert.Null(diagnostics.ErrorMessage);
            Assert.NotNull(diagnostics.LastReceipt);
            Assert.True(diagnostics.LastReceipt!.UserOpSuccess, diagnostics.LastReceipt.RevertReason);
            Assert.Equal(UserOperationFailureKind.Succeeded, diagnostics.LastReceipt.FailureKind);
            Assert.True(diagnostics.LastReceipt.ActualGasUsed > BigInteger.Zero);

            Assert.NotNull(diagnostics.BundlerLookup);
            Assert.True(diagnostics.BundlerLookup!.Sender.IsTheSameAddress(session.Account!.Address));
            Assert.False(string.IsNullOrEmpty(diagnostics.BundlerLookup.CallDataHex));
            Assert.False(string.IsNullOrEmpty(diagnostics.BundlerLookup.SignatureHex));
            Assert.False(diagnostics.BundlerLookup.IsPending);
            Assert.NotNull(diagnostics.BundlerLookup.BlockNumber);

            Assert.NotNull(diagnostics.GasPanel);
            Assert.True(diagnostics.GasPanel!.RequestedCallGasLimit > BigInteger.Zero);
            Assert.Equal(diagnostics.LastReceipt.ActualGasUsed, diagnostics.GasPanel.ActualGasUsed);

            Assert.NotNull(diagnostics.PackedView);
            Assert.Equal(diagnostics.BundlerLookup.CallGasLimit, diagnostics.PackedView!.UnpackedCallGasLimit);
            Assert.Equal(diagnostics.BundlerLookup.VerificationGasLimit, diagnostics.PackedView.UnpackedVerificationGasLimit);
            Assert.True(diagnostics.PackedView.TotalGas > BigInteger.Zero);
        }

        [Fact]
        [Trait("UseCase", "Diagnostics")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#diagnostics-revert")]
        public async Task Given_a_deployed_account_When_the_user_runs_the_reverting_op_Then_it_is_diagnosed_as_RevertedWithReason()
        {
            var session = await NewFundedSessionAsync();
            var diagnostics = new DiagnosticsViewModel(session);
            await diagnostics.RunDiagnosedOpCommand.ExecuteAsync(null);
            Assert.Null(diagnostics.ErrorMessage);

            await diagnostics.RunRevertingOpCommand.ExecuteAsync(null);

            Assert.Null(diagnostics.ErrorMessage);
            Assert.NotNull(diagnostics.LastReceipt);
            Assert.False(diagnostics.LastReceipt!.UserOpSuccess);
            Assert.Equal(UserOperationFailureKind.RevertedWithReason, diagnostics.LastReceipt.FailureKind);
            Assert.False(diagnostics.LastReceipt.IsLikelyOutOfGas);
            Assert.False(string.IsNullOrEmpty(diagnostics.LastReceipt.RevertReason));
            Assert.Contains("count failed", diagnostics.LastReceipt.RevertReason);
            Assert.False(string.IsNullOrEmpty(diagnostics.LastReceipt.FailureDiagnostic));
        }

        [Fact]
        [Trait("UseCase", "Diagnostics")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#diagnostics-out-of-gas")]
        public async Task Given_a_deployed_account_When_the_user_runs_the_out_of_gas_op_Then_it_is_diagnosed_as_FailedWithoutReason_and_likely_out_of_gas()
        {
            var session = await NewFundedSessionAsync();
            var diagnostics = new DiagnosticsViewModel(session);
            await diagnostics.RunDiagnosedOpCommand.ExecuteAsync(null);
            Assert.Null(diagnostics.ErrorMessage);

            await diagnostics.RunOutOfGasOpCommand.ExecuteAsync(null);

            Assert.Null(diagnostics.ErrorMessage);
            Assert.NotNull(diagnostics.LastReceipt);
            Assert.False(diagnostics.LastReceipt!.UserOpSuccess);
            Assert.Equal(UserOperationFailureKind.FailedWithoutReason, diagnostics.LastReceipt.FailureKind);
            Assert.True(diagnostics.LastReceipt.IsLikelyOutOfGas);
            Assert.Contains("callGasLimit", diagnostics.LastReceipt.FailureDiagnostic);

            Assert.NotNull(diagnostics.BundlerLookup);
            Assert.Equal(BigInteger.One, diagnostics.BundlerLookup!.CallGasLimit);
        }

        [Fact]
        [Trait("UseCase", "Diagnostics")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#diagnostics-guard")]
        public async Task Given_a_not_yet_deployed_account_When_the_user_runs_the_reverting_op_Then_a_readable_guard_error_is_surfaced()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session);
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);
            var diagnostics = new DiagnosticsViewModel(session);

            await diagnostics.RunRevertingOpCommand.ExecuteAsync(null);

            Assert.False(diagnostics.IsBusy);
            Assert.Null(diagnostics.LastReceipt);
            Assert.False(string.IsNullOrEmpty(diagnostics.ErrorMessage));
            Assert.Contains("not deployed", diagnostics.ErrorMessage);
        }

        [Fact]
        [Trait("UseCase", "Diagnostics")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#diagnostics-guard")]
        public async Task Given_no_active_account_When_the_diagnosed_op_runs_Then_a_readable_error_is_surfaced()
        {
            var session = NewSession();
            var diagnostics = new DiagnosticsViewModel(session);

            await diagnostics.RunDiagnosedOpCommand.ExecuteAsync(null);

            Assert.False(diagnostics.IsBusy);
            Assert.Null(diagnostics.LastReceipt);
            Assert.False(string.IsNullOrEmpty(diagnostics.ErrorMessage));
            Assert.Contains("No active account", diagnostics.ErrorMessage);
        }
    }
}
