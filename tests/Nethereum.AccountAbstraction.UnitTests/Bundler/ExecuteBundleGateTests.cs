using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Bundler
{
    public class ExecuteBundleGateTests
    {
        private const string EntryPoint = "0x0000000000000000000000000000000000000007";
        private const string Beneficiary = "0x3333333333333333333333333333333333333333";

        [Fact]
        public async Task ConcurrentPublicCalls_AreSerialized_AndDoNotDoubleSubmitTheSameOp()
        {
            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(CreateEntry("0x1111111111111111111111111111111111111111", "0xaa", priority: 1));

            var executor = new ConcurrencyDetectingBundleExecutor();
            var service = CreateService(mempool, executor);

            var task1 = service.ExecuteBundleAsync();
            var task2 = service.ExecuteBundleAsync(minBaseFee: null);

            var results = await Task.WhenAll(task1, task2);

            Assert.False(executor.ConcurrencyViolationDetected,
                "both public ExecuteBundleAsync overloads must serialize under _bundleGate, " +
                "not run the bundle core concurrently");

            Assert.Equal(1, executor.SubmitCallCount);
            Assert.Equal(1, results.Count(r => r != null && r.Success));
            Assert.Equal(1, results.Count(r => r == null));

            Assert.Equal(MempoolEntryState.Included, (await mempool.GetAsync("0xaa"))!.State);
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

        private static byte[] PackGasFees(BigInteger maxPriorityFeePerGas, BigInteger maxFeePerGas)
        {
            var packed = new byte[32];
            var priorityBytes = maxPriorityFeePerGas.ToByteArray(isUnsigned: true, isBigEndian: true);
            var maxBytes = maxFeePerGas.ToByteArray(isUnsigned: true, isBigEndian: true);
            Array.Copy(priorityBytes, 0, packed, 16 - priorityBytes.Length, priorityBytes.Length);
            Array.Copy(maxBytes, 0, packed, 32 - maxBytes.Length, maxBytes.Length);
            return packed;
        }

        private sealed class ConcurrencyDetectingBundleExecutor : IBundleExecutor
        {
            private int _current;
            private int _submitCallCount;

            public bool ConcurrencyViolationDetected { get; private set; }
            public int SubmitCallCount => Volatile.Read(ref _submitCallCount);

            public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries) =>
                Task.FromResult(new Bundle { Entries = entries, EntryPoint = EntryPoint, Beneficiary = Beneficiary });

            public async Task<string> SubmitAsync(Bundle bundle)
            {
                Interlocked.Increment(ref _submitCallCount);
                if (Interlocked.Increment(ref _current) > 1)
                {
                    ConcurrencyViolationDetected = true;
                }

                await Task.Delay(50);

                Interlocked.Decrement(ref _current);
                return "0x" + new string('1', 64);
            }

            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash) =>
                Task.FromResult(new BundleExecutionResult { Success = true });

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) => throw new NotSupportedException();
            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) => throw new NotSupportedException();
        }
    }
}
