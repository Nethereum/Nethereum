using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.Freezer.UnitTests
{
    public class DecodedClusterCacheTests
    {

        [Fact]
        public void Given_MaxBlocks_When_ExceededByOne_Then_LruEvictsLeastRecentlyUsed()
        {
            var cache = new DecodedClusterCache(maxBlocks: 3);
            var decodeCount = new Dictionary<long, int>();

            DecodedCluster Factory(long n)
            {
                decodeCount[n] = decodeCount.GetValueOrDefault(n) + 1;
                return EmptyCluster();
            }

            cache.GetOrAdd(1, Factory);
            cache.GetOrAdd(2, Factory);
            cache.GetOrAdd(3, Factory);

            cache.GetOrAdd(1, Factory);

            cache.GetOrAdd(4, Factory);

            Assert.Equal(3, cache.Count);

            cache.GetOrAdd(1, Factory);
            cache.GetOrAdd(3, Factory);
            cache.GetOrAdd(2, Factory);

            Assert.Equal(1, decodeCount[1]);
            Assert.Equal(1, decodeCount[3]);
            Assert.Equal(2, decodeCount[2]);
            Assert.Equal(1, decodeCount[4]);
        }


        [Fact]
        public void Given_ConcurrentGetOrAddSameBlock_When_ManyThreads_Then_FactoryRunsExactlyOnce()
        {
            var cache = new DecodedClusterCache(maxBlocks: 512);
            var factoryCalls = 0;
            var results = new DecodedCluster[64];
            var barrier = new Barrier(results.Length);

            DecodedCluster Factory(long n)
            {
                Interlocked.Increment(ref factoryCalls);
                Thread.Sleep(20);
                return EmptyCluster();
            }

            var threads = new Thread[results.Length];
            for (var i = 0; i < threads.Length; i++)
            {
                var index = i;
                threads[index] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    results[index] = cache.GetOrAdd(42, Factory);
                });
                threads[index].Start();
            }

            foreach (var thread in threads)
                thread.Join();

            Assert.Equal(1, factoryCalls);
            for (var i = 1; i < results.Length; i++)
                Assert.Same(results[0], results[i]);
        }

        [Fact]
        public void Given_ConcurrentGetOrAddDifferentBlocks_When_Parallel_Then_AllCorrectNoDeadlock()
        {
            var cache = new DecodedClusterCache(maxBlocks: 1024);
            const int blockCount = 200;

            var observed = new DecodedCluster[blockCount];

            Parallel.For(0, blockCount, i =>
            {
                var blockNumber = (long)i;
                observed[i] = cache.GetOrAdd(blockNumber, n => ClusterTaggedWith((int)n));
            });

            for (var i = 0; i < blockCount; i++)
            {
                Assert.NotNull(observed[i]);
                Assert.Single(observed[i].Receipts);
                Assert.Equal(i, (int)observed[i].Receipts[0].GasUsed);
            }
        }


        [Fact]
        public void Given_CreateTxCluster_When_Cached_Then_SenderAndContractAddressInSameEntry()
        {
            var cache = new DecodedClusterCache(maxBlocks: 8);
            const string sender = "0x1111111111111111111111111111111111111111";
            const string contractAddress = "0x2222222222222222222222222222222222222222";

            var cluster = new DecodedCluster(
                EmptyCluster().Body,
                new List<DerivedReceipt>(),
                new List<string> { sender },
                new List<string> { contractAddress });

            var result = cache.GetOrAdd(7, _ => cluster);

            Assert.Equal(sender, result.Senders[0]);
            Assert.Equal(contractAddress, result.ContractAddresses[0]);
        }


        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Decode-once cache with invalidation")]
        public void Given_CachedBlock_When_Invalidate_Then_NextGetOrAddReRunsFactory()
        {
            var cache = new DecodedClusterCache(maxBlocks: 8);
            var decodeCount = 0;
            DecodedCluster Factory(long n)
            {
                decodeCount++;
                return EmptyCluster();
            }

            var first = cache.GetOrAdd(9, Factory);
            cache.Invalidate(9);
            var second = cache.GetOrAdd(9, Factory);

            Assert.Equal(2, decodeCount);
            Assert.NotSame(first, second);
        }


        private static DecodedCluster EmptyCluster() =>
            new(
                new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader>(), null),
                new List<DerivedReceipt>(),
                new List<string>(),
                new List<string>());

        private static DecodedCluster ClusterTaggedWith(int tag) =>
            new(
                new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader>(), null),
                new List<DerivedReceipt>
                {
                    new(
                        postStateOrStatus: new byte[] { 1 },
                        cumulativeGasUsed: tag,
                        gasUsed: tag,
                        bloom: new byte[256],
                        logs: new List<DerivedLog>(),
                        txHash: new byte[32],
                        transactionType: TransactionType.LegacyTransaction,
                        effectiveGasPrice: default,
                        contractAddress: null,
                        blobGasUsed: null,
                        blobGasPrice: null)
                },
                new List<string>(),
                new List<string>());
    }
}
