using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Bundler
{
    public class BundlerStatsAndFailedOpEvictionTests
    {
        private const string EntryPoint = "0x0000000000000000000000000000000000000007";
        private const string Beneficiary = "0x3333333333333333333333333333333333333333";

        [Fact]
        public async Task GetStatsAsync_ReportsRealPendingAndSubmittedCounts()
        {
            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(CreateEntry("0x1111111111111111111111111111111111111111", "0xaa", priority: 3));
            await mempool.AddAsync(CreateEntry("0x2222222222222222222222222222222222222222", "0xbb", priority: 2));
            await mempool.AddAsync(CreateEntry("0x4444444444444444444444444444444444444444", "0xcc", priority: 1));

            await mempool.MarkSubmittedAsync(new[] { "0xcc" }, "0x" + new string('1', 64));

            var service = CreateService(mempool, new CapturingBundleExecutor());

            var stats = await service.GetStatsAsync();

            Assert.Equal(2, stats.PendingCount);
            Assert.Equal(1, stats.SubmittedCount);
        }

        [Fact]
        public async Task OutOfRangeFailedOpIndex_EvictsAttempt_WithoutThrowingOrLooping()
        {
            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(CreateEntry("0x1111111111111111111111111111111111111111", "0xaa", priority: 2));
            await mempool.AddAsync(CreateEntry("0x2222222222222222222222222222222222222222", "0xbb", priority: 1));

            var executor = new ScriptedBundleExecutor((callIndex, bundle) =>
                throw new BundleFailedOpException(99, "AA24", "FailedOp(99, AA24 signature error)"));
            var service = CreateService(mempool, executor);

            var result = await service.ExecuteBundleAsync();

            Assert.Equal(1, executor.SubmitCallCount);
            Assert.NotNull(result);
            Assert.False(result!.Success);

            Assert.Equal(MempoolEntryState.Failed, (await mempool.GetAsync("0xaa"))!.State);
            Assert.Equal(MempoolEntryState.Failed, (await mempool.GetAsync("0xbb"))!.State);
            Assert.Empty(await mempool.GetPendingAsync(10));
        }

        [Fact]
        public async Task OutOfRangeFailedOpIndex_AfterHalving_CascadesOrphanedSuccessor_LeavingItPendingUnpenalized()
        {
            const string sender = "0x1111111111111111111111111111111111111111";
            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(CreateChainEntry(sender, "0xn0", nonce: 0, priority: 2));
            await mempool.AddAsync(CreateChainEntry(sender, "0xn1", nonce: 1, priority: 2));

            var executor = new ScriptedBundleExecutor((callIndex, bundle) =>
            {
                if (callIndex == 0)
                {
                    throw new BundleSimulationRevertedException("0x", "simulation reverted", new Exception("boom"));
                }
                throw new BundleFailedOpException(99, "", "FailedOp(99, out of range)");
            });
            var service = CreateService(mempool, executor);

            var result = await service.ExecuteBundleAsync();

            Assert.Equal(2, executor.SubmitCallCount);
            Assert.NotNull(result);
            Assert.False(result!.Success);

            Assert.Equal(MempoolEntryState.Failed, (await mempool.GetAsync("0xn0"))!.State);
            Assert.Equal(MempoolEntryState.Pending, (await mempool.GetAsync("0xn1"))!.State);
        }

        [Fact]
        public async Task ValidFailedOpIndex_EvictsOnlyTheOffender_AndBundlesSurvivor()
        {
            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(CreateEntry("0x1111111111111111111111111111111111111111", "0xaa", priority: 2));
            await mempool.AddAsync(CreateEntry("0x2222222222222222222222222222222222222222", "0xbb", priority: 1));

            var executor = new ScriptedBundleExecutor((callIndex, bundle) =>
            {
                if (callIndex == 0) throw new BundleFailedOpException(0, "AA24", "FailedOp(0, AA24 signature error)");
                return "0x" + new string('2', 64);
            });
            var service = CreateService(mempool, executor);

            var result = await service.ExecuteBundleAsync();

            Assert.Equal(2, executor.SubmitCallCount);
            Assert.NotNull(result);
            Assert.True(result!.Success);

            Assert.Equal(MempoolEntryState.Failed, (await mempool.GetAsync("0xaa"))!.State);
            Assert.Equal(MempoolEntryState.Included, (await mempool.GetAsync("0xbb"))!.State);
        }

        private static BundlerService CreateService(IUserOpMempool mempool, IBundleExecutor executor)
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPoint },
                BeneficiaryAddress = Beneficiary,
                AutoBundleIntervalMs = 0
            };

            var web3 = new Web3.Web3(new StubZeroNonceRpcClient());
            return new BundlerService(web3, config, mempool, validator: null, executor);
        }

        private static MempoolEntry CreateEntry(string sender, string hash, BigInteger priority)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = EntryPoint,
                Priority = priority,
                UserOperation = new PackedUserOperation
                {
                    Sender = sender,
                    Nonce = 0,
                    GasFees = PackGasFees(priority, priority)
                }
            };
        }

        private static MempoolEntry CreateChainEntry(string sender, string hash, int nonce, BigInteger priority)
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
                    GasFees = PackGasFees(priority, priority)
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
            public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries) =>
                Task.FromResult(new Bundle { Entries = entries, EntryPoint = EntryPoint, Beneficiary = Beneficiary });

            public Task<string> SubmitAsync(Bundle bundle) => Task.FromResult("0x" + new string('1', 64));

            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash) =>
                Task.FromResult(new BundleExecutionResult { Success = true });

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) => throw new NotSupportedException();
            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) => throw new NotSupportedException();
        }

        private sealed class ScriptedBundleExecutor : IBundleExecutor
        {
            private readonly Func<int, Bundle, string> _submit;
            public int SubmitCallCount { get; private set; }

            public ScriptedBundleExecutor(Func<int, Bundle, string> submit) => _submit = submit;

            public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries) =>
                Task.FromResult(new Bundle { Entries = entries, EntryPoint = EntryPoint, Beneficiary = Beneficiary });

            public Task<string> SubmitAsync(Bundle bundle)
            {
                var callIndex = SubmitCallCount;
                SubmitCallCount++;
                return Task.FromResult(_submit(callIndex, bundle));
            }

            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash) =>
                Task.FromResult(new BundleExecutionResult { Success = true });

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) => throw new NotSupportedException();
            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) => throw new NotSupportedException();
        }
    }
}
