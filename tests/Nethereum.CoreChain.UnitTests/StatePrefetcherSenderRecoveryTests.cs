using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public partial class StatePrefetcherTests
    {
        private const string SenderRecoveryThreadPrefix = "sender-recovery-";

        private sealed class RefusingVerifier : ITransactionVerificationAndRecovery
        {
            private readonly ITransactionVerificationAndRecovery _inner = new TransactionVerificationAndRecoveryImp();
            private readonly ISignedTransaction _refused;

            public RefusingVerifier(ISignedTransaction refused) { _refused = refused; }

            public byte[] GetPublicKey(ISignedTransaction transaction) => _inner.GetPublicKey(transaction);
            public byte[] GetPublicKey(string rlp) => _inner.GetPublicKey(rlp);
            public string GetSenderAddress(string rlp) => _inner.GetSenderAddress(rlp);
            public bool VerifyTransaction(ISignedTransaction transaction) => _inner.VerifyTransaction(transaction);
            public bool VerifyTransaction(string rlp) => _inner.VerifyTransaction(rlp);

            public string GetSenderAddress(ISignedTransaction transaction) =>
                ReferenceEquals(transaction, _refused)
                    ? throw new InvalidOperationException("signature cannot be recovered")
                    : _inner.GetSenderAddress(transaction);
        }

        [Fact]
        public async Task Given_ABlockAndNoPrefetcher_When_TheExecutorRuns_Then_EverySenderIsRecoveredOnceOnASenderRecoveryThread()
        {
            var (header, transactions) = await ProduceBlockAsync();

            var executed = await ExecuteAfterPrefetchOfAsync(header, transactions, transactions, prefetchWorkers: 0);

            Assert.Equal(header.StateRoot, executed.Result.PostStateRoot);
            Assert.Equal(transactions.Count, executed.Verifier.Recoveries);
            Assert.All(executed.Verifier.RecoveryThreads, name => Assert.StartsWith(SenderRecoveryThreadPrefix, name));
        }

        [Fact]
        public async Task Given_APrefetcher_When_ThePrefetchAndTheExecutorRun_Then_EverySenderIsRecoveredOnceInTotalOnASenderRecoveryThread()
        {
            var (header, transactions) = await ProduceBlockAsync();

            var executed = await ExecuteAfterPrefetchOfAsync(header, transactions, transactions, prefetchWorkers: 3);

            Assert.Equal(header.StateRoot, executed.Result.PostStateRoot);
            Assert.Equal(transactions.Count, executed.Verifier.Recoveries);
            Assert.All(executed.Verifier.RecoveryThreads, name => Assert.StartsWith(SenderRecoveryThreadPrefix, name));
        }

        [Fact]
        public async Task Given_ATransactionInstanceThatIsNotTheOneRecovered_When_TheExecutorRuns_Then_ItIsRecoveredByTheProcessorOnTheExecutingThread()
        {
            var (header, transactions) = await ProduceBlockAsync();
            var otherTransactions = transactions.Select((_, index) => CallToContract(100 + index)).ToList();

            var executed = await ExecuteAfterPrefetchOfAsync(header, transactions, otherTransactions, prefetchWorkers: 3);

            Assert.Equal(transactions.Count, executed.Recoveries);
            Assert.Equal(
                transactions.Count,
                executed.Verifier.RecoveryThreads.Count(name => !name.StartsWith(SenderRecoveryThreadPrefix)));
        }

        [Fact]
        public async Task Given_ASenderThatCannotBeRecovered_When_TheBlockExecutes_Then_ThatTransactionIsRefusedAsAnInvalidSignatureAndTheOthersExecute()
        {
            var (header, transactions) = await ProduceBlockAsync();
            using var bundle = InMemoryChainStoreBundle.Open();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(bundle.State, Fork);
            await SeedAsync(bundle.State);
            await ShapedHeaders.SaveGenesisAsync(bundle.Blocks, Fork, 30_000_000);
            using var stack = ExecutionStackFactory.Build(
                bundle, new FixedChainActivations(Fork), _ => Config, _ => Config.GetHardforkConfig(),
                new ExecutionStackOptions
                {
                    RewardPolicy = NoRewardPolicy.Instance,
                    TransactionVerifier = new RefusingVerifier(transactions[2])
                });

            var result = await stack.Executor.ExecuteAsync(
                header,
                transactions.Select(t => new TxEntry(t)).ToList(),
                null,
                null,
                new BlockExecutionOptions { Role = BlockExecutionRole.Validating });

            Assert.True(result.ContainsInvalidTransaction);
            Assert.Equal(TransactionError.InvalidSignature, result.Receipts[2].ErrorCode);
            Assert.True(result.Receipts[2].Skipped);
            Assert.True(result.Receipts[0].Success);
        }
    }
}
