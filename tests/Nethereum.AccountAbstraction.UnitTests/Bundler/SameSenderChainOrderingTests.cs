using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Aggregation;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Interfaces;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Bundler
{
    public class SameSenderChainOrderingTests
    {
        private const string EntryPoint = "0x0000000000000000000000000000000000000007";
        private const string SenderA = "0x1111111111111111111111111111111111111111";
        private const string SenderB = "0x2222222222222222222222222222222222222222";

        [Fact]
        public async Task SubmissionOrderedEntries_SameSenderChainInterleavedByPriority_StaysAscendingPerSenderKey()
        {
            var mempool = new InMemoryUserOpMempool();

            await mempool.AddAsync(CreateEntry(SenderA, "0xa0", nonce: 0, priority: 1));
            await mempool.AddAsync(CreateEntry(SenderB, "0xb0", nonce: 0, priority: 100));
            await mempool.AddAsync(CreateEntry(SenderA, "0xa1", nonce: 1, priority: 1));
            await mempool.AddAsync(CreateEntry(SenderA, "0xa2", nonce: 2, priority: 1));

            var executor = new CapturingBundleExecutor();
            var service = CreateService(mempool, executor);

            await service.ExecuteBundleAsync(minBaseFee: null);

            var built = Assert.Single(executor.BuiltBundles);
            Assert.Equal(4, built.Entries.Length);

            var submissionOrdered = built.SubmissionOrderedEntries;
            AssertEachSenderKeyIsStrictlyAscending(submissionOrdered);

            var senderAIndices = submissionOrdered
                .Select((e, i) => (e, i))
                .Where(x => (x.e.UserOperation.Sender ?? "").Equals(SenderA, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.i)
                .ToArray();
            Assert.Equal(3, senderAIndices.Length);
            Assert.Equal(new[] { senderAIndices[0], senderAIndices[0] + 1, senderAIndices[0] + 2 }, senderAIndices);
        }

        [Fact]
        public async Task SubmissionOrderedEntries_SameSenderChainSharingOneAggregator_StaysAscending()
        {
            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(CreateEntry(SenderA, "0xa0", nonce: 0, priority: 1));
            await mempool.AddAsync(CreateEntry(SenderA, "0xa1", nonce: 1, priority: 1));

            var pending = await mempool.GetPendingAsync(10);

            var aggregatorRegistry = new SingleAggregatorRegistry(AggregatorAddress, new NoOpAggregator());
            var executor = new BundleExecutor(
                new Web3.Web3(new StubZeroNonceRpcClient()),
                new BundlerConfig
                {
                    SupportedEntryPoints = new[] { EntryPoint },
                    BeneficiaryAddress = "0x3333333333333333333333333333333333333333"
                },
                aggregatorRegistry);

            var bundle = await executor.BuildBundleAsync(pending);

            Assert.True(bundle.UsesAggregation);
            var group = Assert.Single(bundle.AggregatedGroups.Values);
            Assert.Equal(2, group.Entries.Length);
            AssertEachSenderKeyIsStrictlyAscending(bundle.SubmissionOrderedEntries);
        }

        [Fact]
        public async Task SubmissionOrderedEntries_SameSenderChainSplitAcrossAggregatorDetection_StaysAscendingAndWhole()
        {
            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(CreateEntry(SenderA, "0xa0", nonce: 0, priority: 1));
            await mempool.AddAsync(CreateEntry(SenderA, "0xa1", nonce: 1, priority: 1));
            await mempool.AddAsync(CreateEntry(SenderB, "0xb0", nonce: 0, priority: 1));

            var pending = await mempool.GetPendingAsync(10);

            var aggregatorRegistry = new PerOpAggregatorRegistry(
                AggregatorAddress,
                new NoOpAggregator(),
                userOp => userOp.Nonce == 0 && (userOp.Sender ?? "").Equals(SenderA, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : AggregatorAddress);

            var executor = new BundleExecutor(
                new Web3.Web3(new StubZeroNonceRpcClient()),
                new BundlerConfig
                {
                    SupportedEntryPoints = new[] { EntryPoint },
                    BeneficiaryAddress = "0x3333333333333333333333333333333333333333"
                },
                aggregatorRegistry);

            var bundle = await executor.BuildBundleAsync(pending);

            foreach (var group in bundle.AggregatedGroups.Values)
            {
                var senderAInGroup = group.Entries.Where(e =>
                    (e.UserOperation.Sender ?? "").Equals(SenderA, StringComparison.OrdinalIgnoreCase));
                Assert.True(
                    !senderAInGroup.Any() || senderAInGroup.Count() == 2,
                    "Sender A's chain must be entirely aggregated or entirely non-aggregated, never split.");
            }

            AssertEachSenderKeyIsStrictlyAscending(bundle.SubmissionOrderedEntries);
        }

        private const string AggregatorAddress = "0x9999999999999999999999999999999999999999";

        private sealed class SingleAggregatorRegistry : IAggregatorRegistry
        {
            private readonly string _aggregatorAddress;
            private readonly IAggregator _aggregator;

            public SingleAggregatorRegistry(string aggregatorAddress, IAggregator aggregator)
            {
                _aggregatorAddress = aggregatorAddress;
                _aggregator = aggregator;
            }

            public bool SupportsAggregation => true;
            public IReadOnlyCollection<string> RegisteredAggregators => new[] { _aggregatorAddress };

            public void RegisterAggregator(string aggregatorAddress, IAggregator aggregator)
            {
            }

            public IAggregator? GetAggregator(string aggregatorAddress) =>
                aggregatorAddress.Equals(_aggregatorAddress, StringComparison.OrdinalIgnoreCase) ? _aggregator : null;

            public string? DetectAggregator(PackedUserOperation userOp) => _aggregatorAddress;
        }

        private sealed class PerOpAggregatorRegistry : IAggregatorRegistry
        {
            private readonly string _aggregatorAddress;
            private readonly IAggregator _aggregator;
            private readonly Func<PackedUserOperation, string?> _detect;

            public PerOpAggregatorRegistry(string aggregatorAddress, IAggregator aggregator, Func<PackedUserOperation, string?> detect)
            {
                _aggregatorAddress = aggregatorAddress;
                _aggregator = aggregator;
                _detect = detect;
            }

            public bool SupportsAggregation => true;
            public IReadOnlyCollection<string> RegisteredAggregators => new[] { _aggregatorAddress };

            public void RegisterAggregator(string aggregatorAddress, IAggregator aggregator)
            {
            }

            public IAggregator? GetAggregator(string aggregatorAddress) =>
                aggregatorAddress.Equals(_aggregatorAddress, StringComparison.OrdinalIgnoreCase) ? _aggregator : null;

            public string? DetectAggregator(PackedUserOperation userOp) => _detect(userOp);
        }

        private sealed class NoOpAggregator : IAggregator
        {
            public Task ValidateSignaturesAsync(PackedUserOperation[] userOps, byte[] signature) => Task.CompletedTask;

            public Task<byte[]> ValidateUserOpSignatureAsync(PackedUserOperation userOp) =>
                Task.FromResult(Array.Empty<byte>());

            public Task<byte[]> AggregateSignaturesAsync(PackedUserOperation[] userOps) =>
                Task.FromResult(new byte[] { 0x01 });
        }

        private static void AssertEachSenderKeyIsStrictlyAscending(MempoolEntry[] submissionOrdered)
        {
            var lastNonceBySenderKey = new Dictionary<(string Sender, BigInteger Key), BigInteger>();

            foreach (var entry in submissionOrdered)
            {
                var sender = (entry.UserOperation.Sender ?? "").ToLowerInvariant();
                var mapKey = (Sender: sender, Key: entry.UserOperation.Nonce >> 64);

                if (lastNonceBySenderKey.TryGetValue(mapKey, out var lastNonce))
                {
                    Assert.True(
                        entry.UserOperation.Nonce > lastNonce,
                        $"Sender {mapKey.Sender} key {mapKey.Key}: nonce {entry.UserOperation.Nonce} did not " +
                        $"strictly follow {lastNonce} in submission order - a bundle built this way would " +
                        "validate the higher nonce first and revert on-chain with AA25.");
                }

                lastNonceBySenderKey[mapKey] = entry.UserOperation.Nonce;
            }
        }

        private static BundlerService CreateService(IUserOpMempool mempool, IBundleExecutor executor)
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPoint },
                BeneficiaryAddress = "0x3333333333333333333333333333333333333333",
                AutoBundleIntervalMs = 0
            };

            var web3 = new Web3.Web3(new StubZeroNonceRpcClient());
            return new BundlerService(web3, config, mempool, validator: null, executor);
        }

        private static MempoolEntry CreateEntry(string sender, string hash, int nonce, int priority)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = EntryPoint,
                Priority = priority,
                UserOperation = new PackedUserOperation
                {
                    Sender = sender,
                    Nonce = nonce,
                    GasFees = PackGasFees(1_000_000_000, 30_000_000_000)
                }
            };
        }

        private static byte[] PackGasFees(BigInteger maxPriorityFeePerGas, BigInteger maxFeePerGas)
        {
            var packed = new byte[32];
            var priorityBytes = maxPriorityFeePerGas.ToByteArray(isUnsigned: true, isBigEndian: true);
            var maxBytes = maxFeePerGas.ToByteArray(isUnsigned: true, isBigEndian: true);
            Array.Copy(priorityBytes, 0, packed, 16 - priorityBytes.Length, priorityBytes.Length);
            Array.Copy(maxBytes, 0, packed, 32 - maxBytes.Length, maxBytes.Length);
            return packed;
        }

        private sealed class CapturingBundleExecutor : IBundleExecutor
        {
            public List<Bundle> BuiltBundles { get; } = new();

            public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries)
            {
                var bundle = new Bundle
                {
                    Entries = entries,
                    EntryPoint = EntryPoint,
                    Beneficiary = "0x3333333333333333333333333333333333333333"
                };
                BuiltBundles.Add(bundle);
                return Task.FromResult(bundle);
            }

            public Task<string> SubmitAsync(Bundle bundle) =>
                Task.FromResult("0x" + new string('1', 64));

            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash) =>
                Task.FromResult(new BundleExecutionResult { Success = true });

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) =>
                throw new NotSupportedException();

            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) =>
                throw new NotSupportedException();
        }
    }
}
