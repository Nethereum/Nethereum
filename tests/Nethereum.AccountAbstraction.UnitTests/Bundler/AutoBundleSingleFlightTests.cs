using System.Collections.Concurrent;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Bundler
{
    public class AutoBundleSingleFlightTests
    {
        private const string EntryPoint = "0x0000000000000000000000000000000000000007";
        private const string Beneficiary = "0x3333333333333333333333333333333333333333";

        [Fact]
        public async Task OverlappingTicks_DoNotDoubleSubmit_WhileABundleIsInFlight()
        {
            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(CreateEntry("0x1111111111111111111111111111111111111111", "0xaa", priority: 2));
            await mempool.AddAsync(CreateEntry("0x2222222222222222222222222222222222222222", "0xbb", priority: 1));

            var executor = new BlockingBundleExecutor();

            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPoint },
                BeneficiaryAddress = Beneficiary,
                AutoBundleIntervalMs = 25,
                SkipUnderpricedOpsInAutoBundle = false
            };

            var web3 = new Web3.Web3(new StubZeroNonceRpcClient());
            using var service = new BundlerService(web3, config, mempool, validator: null, executor);

            Assert.True(executor.EnteredSubmit.Wait(TimeSpan.FromSeconds(5)),
                "the auto-bundle timer never submitted a bundle");
            Assert.Equal(1, executor.SubmitCallCount);

            await Task.Delay(200);
            Assert.Equal(1, executor.SubmitCallCount);

            executor.ReleaseSubmit.Set();
            service.SetBundlingMode(BundlingMode.Manual);
            await Task.Delay(100);
        }

        [Fact]
        public async Task FailingTick_LogsError_InsteadOfSwallowingSilently()
        {
            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(CreateEntry("0x1111111111111111111111111111111111111111", "0xaa", priority: 1));

            var executor = new ThrowingBundleExecutor();
            var logger = new CapturingLogger();

            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPoint },
                BeneficiaryAddress = Beneficiary,
                AutoBundleIntervalMs = 25,
                SkipUnderpricedOpsInAutoBundle = false
            };

            var web3 = new Web3.Web3(new StubZeroNonceRpcClient());
            using var service = new BundlerService(
                web3, config, mempool, validator: null, executor, reputationService: null, logger);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (logger.ErrorCount == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.True(logger.ErrorCount > 0, "the failed auto-bundle tick was not logged");
            Assert.Contains(logger.Errors, e => e is InvalidOperationException);

            service.SetBundlingMode(BundlingMode.Manual);
            await Task.Delay(100);
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

        private sealed class BlockingBundleExecutor : IBundleExecutor
        {
            public readonly ManualResetEventSlim EnteredSubmit = new(false);
            public readonly ManualResetEventSlim ReleaseSubmit = new(false);
            private int _submitCallCount;

            public int SubmitCallCount => Volatile.Read(ref _submitCallCount);

            public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries) =>
                Task.FromResult(new Bundle { Entries = entries, EntryPoint = EntryPoint, Beneficiary = Beneficiary });

            public Task<string> SubmitAsync(Bundle bundle)
            {
                Interlocked.Increment(ref _submitCallCount);
                EnteredSubmit.Set();
                ReleaseSubmit.Wait();
                return Task.FromResult("0x" + new string('1', 64));
            }

            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash) =>
                Task.FromResult(new BundleExecutionResult { Success = true });

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) => throw new NotSupportedException();
            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) => throw new NotSupportedException();
        }

        private sealed class ThrowingBundleExecutor : IBundleExecutor
        {
            public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries) =>
                throw new InvalidOperationException("boom while building the bundle");

            public Task<string> SubmitAsync(Bundle bundle) => throw new NotSupportedException();
            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash) => throw new NotSupportedException();
            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) => throw new NotSupportedException();
            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) => throw new NotSupportedException();
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly ConcurrentQueue<Exception> _errors = new();

            public IEnumerable<Exception> Errors => _errors;
            public int ErrorCount => _errors.Count;

            IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Error && exception != null)
                {
                    _errors.Enqueue(exception);
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }
    }
}
