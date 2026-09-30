using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    /// <summary>
    /// Descriptive tests for <see cref="BatchViewModel"/> - ERC-7579 batch execution through the
    /// example's on-ramp, against a real in-process bundler with ERC-4337 validation ON. Read these
    /// as usage documentation for "send several calls as one atomic UserOperation".
    /// </summary>
    [Collection(ExampleCollection.COLLECTION_NAME)]
    public class BatchViewModelTests
    {
        private readonly ExampleFixture _fixture;

        public BatchViewModelTests(ExampleFixture fixture)
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
        [Trait("UseCase", "Batch")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#batch-execution")]
        public async Task Given_a_smart_account_When_the_user_batches_two_operations_Then_both_apply_in_a_single_user_operation()
        {
            var session = await NewFundedSessionAsync();
            var batchViewModel = new BatchViewModel(session);

            await batchViewModel.SendBatchCommand.ExecuteAsync(null);

            Assert.Null(batchViewModel.ErrorMessage);
            Assert.NotNull(batchViewModel.LastReceipt);
            Assert.True(batchViewModel.LastReceipt!.UserOpSuccess, batchViewModel.LastReceipt.RevertReason);
            Assert.Equal(new BigInteger(2), batchViewModel.Count);

            var onChainCount = await _fixture.TestCounter.CountersQueryAsync(session.Account!.Address);
            Assert.Equal(new BigInteger(2), onChainCount);
        }

        [Fact]
        [Trait("UseCase", "Batch")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#batch-atomicity")]
        public async Task Given_a_batch_where_one_call_reverts_When_sent_Then_the_whole_batch_fails_atomically()
        {
            var session = await NewFundedSessionAsync();
            var batchViewModel = new BatchViewModel(session);

            await batchViewModel.SendBatchWithAFailingCallCommand.ExecuteAsync(null);

            Assert.False(batchViewModel.IsBusy);
            Assert.Null(batchViewModel.LastReceipt);
            Assert.False(string.IsNullOrEmpty(batchViewModel.ErrorMessage));
            Assert.Contains("count failed", batchViewModel.ErrorMessage);

            var onChainCount = await _fixture.TestCounter.CountersQueryAsync(session.Account!.Address);
            Assert.Equal(BigInteger.Zero, onChainCount);
        }
    }
}
