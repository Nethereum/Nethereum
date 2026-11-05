using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Prefetch;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public partial class StatePrefetcherTests
    {
        private sealed class CountingVerifier : ITransactionVerificationAndRecovery
        {
            private readonly ITransactionVerificationAndRecovery _inner = new TransactionVerificationAndRecoveryImp();
            private int _recoveries;
            private readonly System.Collections.Concurrent.ConcurrentBag<string> _threads = new();

            public int Recoveries => Volatile.Read(ref _recoveries);

            public IReadOnlyCollection<string> RecoveryThreads => _threads.ToArray();

            public byte[] GetPublicKey(ISignedTransaction transaction) => _inner.GetPublicKey(transaction);
            public byte[] GetPublicKey(string rlp) => _inner.GetPublicKey(rlp);
            public string GetSenderAddress(string rlp) => _inner.GetSenderAddress(rlp);
            public bool VerifyTransaction(ISignedTransaction transaction) => _inner.VerifyTransaction(transaction);
            public bool VerifyTransaction(string rlp) => _inner.VerifyTransaction(rlp);

            public string GetSenderAddress(ISignedTransaction transaction)
            {
                Interlocked.Increment(ref _recoveries);
                _threads.Add(Thread.CurrentThread.Name ?? "");
                return _inner.GetSenderAddress(transaction);
            }
        }

        private static async Task<(BlockExecutionResult Result, int Recoveries, CountingVerifier Verifier)> ExecuteAfterPrefetchOfAsync(
            BlockHeader header, List<ISignedTransaction> executed, List<ISignedTransaction> prefetched, int prefetchWorkers)
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(bundle.State, Fork);
            await SeedAsync(bundle.State);
            await ShapedHeaders.SaveGenesisAsync(bundle.Blocks, Fork, 30_000_000);
            var verifier = new CountingVerifier();
            using var stack = ExecutionStackFactory.Build(
                bundle, new FixedChainActivations(Fork), _ => Config, _ => Config.GetHardforkConfig(),
                new ExecutionStackOptions
                {
                    RewardPolicy = NoRewardPolicy.Instance,
                    PrefetchWorkers = prefetchWorkers,
                    TransactionVerifier = verifier
                });

            PrefetchSession session = null;
            if (stack.Prefetcher != null)
            {
                session = stack.Prefetcher.Begin(header, prefetched);
                await session.Completed.WaitAsync(TimeSpan.FromSeconds(30));
            }

            var recoveriesByPrefetch = verifier.Recoveries;

            var result = await stack.Executor.ExecuteAsync(
                header,
                executed.Select(t => new TxEntry(t)).ToList(),
                null,
                null,
                new BlockExecutionOptions { Role = BlockExecutionRole.Validating, Prefetch = session });
            return (result, verifier.Recoveries - recoveriesByPrefetch, verifier);
        }

        [Fact]
        public async Task Given_a_prefetched_sender_When_the_importer_executes_the_transaction_Then_it_uses_it_without_recovering_again()
        {
            var (header, transactions) = await ProduceBlockAsync();

            var executed = await ExecuteAfterPrefetchOfAsync(header, transactions, transactions, prefetchWorkers: 3);

            Assert.Equal(header.StateRoot, executed.Result.PostStateRoot);
            Assert.Equal(0, executed.Recoveries);
        }

        [Fact]
        public async Task Given_a_sender_recorded_for_a_different_transaction_at_the_same_index_When_executed_Then_the_importer_recovers_itself()
        {
            var (header, transactions) = await ProduceBlockAsync();
            var otherTransactions = transactions.Select((_, index) => CallToContract(100 + index)).ToList();

            var executed = await ExecuteAfterPrefetchOfAsync(header, transactions, otherTransactions, prefetchWorkers: 3);

            Assert.Equal(header.StateRoot, executed.Result.PostStateRoot);
            Assert.Equal(transactions.Count, executed.Recoveries);
        }

        private sealed class UppercasingVerifier : ITransactionVerificationAndRecovery
        {
            private readonly ITransactionVerificationAndRecovery _inner = new TransactionVerificationAndRecoveryImp();

            public byte[] GetPublicKey(ISignedTransaction transaction) => _inner.GetPublicKey(transaction);
            public byte[] GetPublicKey(string rlp) => _inner.GetPublicKey(rlp);
            public string GetSenderAddress(string rlp) => _inner.GetSenderAddress(rlp);
            public bool VerifyTransaction(ISignedTransaction transaction) => _inner.VerifyTransaction(transaction);
            public bool VerifyTransaction(string rlp) => _inner.VerifyTransaction(rlp);

            public string GetSenderAddress(ISignedTransaction transaction) =>
                "0x" + _inner.GetSenderAddress(transaction).Substring(2).ToUpperInvariant();
        }

        [Fact]
        public async Task Given_a_custom_verifier_injected_into_the_stack_When_the_prefetch_recovers_Then_the_cached_sender_is_what_that_verifier_returned_and_not_the_default_recovery()
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
                    PrefetchWorkers = 2,
                    TransactionVerifier = new UppercasingVerifier()
                });

            var session = stack.Prefetcher.Begin(header, transactions);
            await session.Completed.WaitAsync(TimeSpan.FromSeconds(30));

            var cached = session.TryGetSender(0, transactions[0]);
            var defaultRecovery = new TransactionVerificationAndRecoveryImp().GetSenderAddress(transactions[0]);
            Assert.Equal(new UppercasingVerifier().GetSenderAddress(transactions[0]), cached);
            Assert.NotEqual(defaultRecovery, cached);
        }

        [Fact]
        public async Task Given_no_prefetch_Then_behaviour_is_unchanged()
        {
            var (header, transactions) = await ProduceBlockAsync();

            var executed = await ExecuteAfterPrefetchOfAsync(header, transactions, transactions, prefetchWorkers: 0);

            Assert.Equal(header.StateRoot, executed.Result.PostStateRoot);
            Assert.Equal(transactions.Count, executed.Recoveries);
        }
    }
}
