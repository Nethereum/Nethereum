using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockProver.Server;
using Nethereum.CoreChain.Proving;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain;
using Nethereum.DevChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    public class BlockProverServiceTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _pk = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly LegacyTransactionSigner _signer = new();

        public BlockProverServiceTests(ITestOutputHelper output) { _output = output; }

        private BlockProverProcessingService CreateService(
            IWitnessStore witnessStore,
            IBlockProver prover,
            IBlockProgressRepository progressRepo = null,
            IProofRequestQueue requestQueue = null,
            ProofCadence cadence = null,
            WitnessRetentionPolicy retention = null,
            BlockProver.Server.Metrics.BlockProverMetrics metrics = null,
            int pollIntervalMs = 100,
            int maxRetries = 3)
        {
            var options = Options.Create(new BlockProverOptions
            {
                Enabled = true,
                PollIntervalMs = pollIntervalMs,
                MaxRetries = maxRetries,
                RetryDelayMs = 100
            });

            return new BlockProverProcessingService(
                NullLogger<BlockProverProcessingService>.Instance,
                witnessStore,
                prover,
                progressRepo ?? new InMemoryBlockchainProgressRepository(),
                requestQueue ?? new InMemoryProofRequestQueue(),
                options,
                cadence,
                retention,
                metrics);
        }

        private async Task<DevChainNode> CreateNodeWithBlocks(
            InMemoryWitnessStore witnessStore, int blockCount)
        {
            BigInteger chainId = 31337;
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = witnessStore;
            node.ProofCadence = ProofCadence.Off;
            await node.StartAsync(new[] { _sender });

            ulong nonce = 0;
            for (int b = 0; b < blockCount; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                        $"0x{(b + 1):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                await node.SendTransactionAsync(tx);
                await node.MineBlockAsync();
            }

            return node;
        }

        [Fact]
        public async Task ShouldProcessQueuedWitnesses()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateNodeWithBlocks(witnessStore, 5);

            var unproven = await witnessStore.GetUnprovenBlockNumbersAsync();
            Assert.Equal(5, unproven.Count);
            _output.WriteLine($"Queued {unproven.Count} blocks for proving");

            var progressRepo = new InMemoryBlockchainProgressRepository();
            var service = CreateService(witnessStore, new MockBlockProver(), progressRepo);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var task = Task.Run(() => service.ExecuteAsync(cts.Token));

            while (!cts.IsCancellationRequested)
            {
                var remaining = await witnessStore.GetUnprovenBlockNumbersAsync();
                if (remaining.Count == 0) break;
                await Task.Delay(50);
            }
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { }

            for (int b = 1; b <= 5; b++)
            {
                var proof = await witnessStore.GetProofAsync(b);
                Assert.NotNull(proof);
                Assert.Equal("Mock", proof.ProverMode);
                Assert.NotNull(proof.ElfHash);
                _output.WriteLine($"Block {b}: proved, elfHash={proof.ElfHash.ToHex(true).Substring(0, 18)}...");
            }

            var lastProcessed = await progressRepo.GetLastBlockNumberProcessedAsync();
            Assert.Equal(5, lastProcessed);
            Assert.Equal(5, service.LastProvenBlock);
            _output.WriteLine($"Progress tracked: last proven block = {lastProcessed}");

            node.Dispose();
        }

        [Fact]
        public async Task ShouldTrackProgressAcrossRestarts()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateNodeWithBlocks(witnessStore, 3);

            var progressRepo = new InMemoryBlockchainProgressRepository();
            var service1 = CreateService(witnessStore, new MockBlockProver(), progressRepo);

            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var task1 = Task.Run(() => service1.ExecuteAsync(cts1.Token));
            while (!cts1.IsCancellationRequested)
            {
                var remaining = await witnessStore.GetUnprovenBlockNumbersAsync();
                if (remaining.Count == 0) break;
                await Task.Delay(50);
            }
            cts1.Cancel();
            try { await task1; } catch (OperationCanceledException) { }

            var lastAfterFirst = await progressRepo.GetLastBlockNumberProcessedAsync();
            Assert.Equal(3, lastAfterFirst);

            ulong nonce = 3;
            for (int b = 0; b < 2; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), (BigInteger)31337,
                        $"0x{(b + 10):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                await node.SendTransactionAsync(tx);
                await node.MineBlockAsync();
            }

            var service2 = CreateService(witnessStore, new MockBlockProver(), progressRepo);
            using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var task2 = Task.Run(() => service2.ExecuteAsync(cts2.Token));
            while (!cts2.IsCancellationRequested)
            {
                var remaining = await witnessStore.GetUnprovenBlockNumbersAsync();
                if (remaining.Count == 0) break;
                await Task.Delay(50);
            }
            cts2.Cancel();
            try { await task2; } catch (OperationCanceledException) { }

            var lastAfterSecond = await progressRepo.GetLastBlockNumberProcessedAsync();
            Assert.Equal(5, lastAfterSecond);
            _output.WriteLine($"After restart: progress resumed from {lastAfterFirst} → {lastAfterSecond}");

            node.Dispose();
        }

        [Fact]
        public async Task ShouldRespectCadenceInService()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateNodeWithBlocks(witnessStore, 6);

            var service = CreateService(witnessStore, new MockBlockProver(),
                cadence: ProofCadence.Periodic(3));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var task = Task.Run(() => service.ExecuteAsync(cts.Token));
            await Task.Delay(1500);
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { }

            for (int b = 1; b <= 6; b++)
            {
                var proof = await witnessStore.GetProofAsync(b);
                if (b % 3 == 0)
                {
                    Assert.NotNull(proof);
                    _output.WriteLine($"Block {b}: proved (periodic hit)");
                }
                else
                {
                    Assert.Null(proof);
                    _output.WriteLine($"Block {b}: skipped (periodic miss)");
                }
            }

            node.Dispose();
        }

        [Fact]
        public async Task ShouldApplyRetentionAfterProving()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateNodeWithBlocks(witnessStore, 5);

            var service = CreateService(witnessStore, new MockBlockProver(),
                retention: WitnessRetentionPolicy.UntilProven);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var task = Task.Run(() => service.ExecuteAsync(cts.Token));
            while (!cts.IsCancellationRequested)
            {
                var remaining = await witnessStore.GetUnprovenBlockNumbersAsync();
                if (remaining.Count == 0) break;
                await Task.Delay(50);
            }
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { }

            for (int b = 1; b <= 5; b++)
            {
                var proof = await witnessStore.GetProofAsync(b);
                Assert.NotNull(proof);

                var witness = await witnessStore.GetWitnessAsync(b);
                Assert.Null(witness);
                _output.WriteLine($"Block {b}: proved, witness purged (UntilProven)");
            }

            node.Dispose();
        }

        [Fact]
        public async Task ShouldRetryOnProverFailure()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateNodeWithBlocks(witnessStore, 1);

            var failingProver = new FailNTimesProver(2, new MockBlockProver());

            var service = CreateService(witnessStore, failingProver, maxRetries: 3);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var task = Task.Run(() => service.ExecuteAsync(cts.Token));
            while (!cts.IsCancellationRequested)
            {
                var remaining = await witnessStore.GetUnprovenBlockNumbersAsync();
                if (remaining.Count == 0) break;
                await Task.Delay(50);
            }
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { }

            var proof = await witnessStore.GetProofAsync(1);
            Assert.NotNull(proof);
            Assert.Equal(2, failingProver.FailureCount);
            _output.WriteLine($"Block 1: proved after {failingProver.FailureCount} failures + 1 success");

            node.Dispose();
        }

        [Fact]
        public async Task ShouldGiveUpAfterMaxRetries()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateNodeWithBlocks(witnessStore, 1);

            var alwaysFailProver = new FailNTimesProver(100, new MockBlockProver());

            var service = CreateService(witnessStore, alwaysFailProver, maxRetries: 2);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var task = Task.Run(() => service.ExecuteAsync(cts.Token));
            await Task.Delay(2000);
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { }

            var proof = await witnessStore.GetProofAsync(1);
            Assert.Null(proof);
            Assert.Equal(2, alwaysFailProver.FailureCount);
            _output.WriteLine($"Block 1: gave up after {alwaysFailProver.FailureCount} attempts");

            node.Dispose();
        }

        [Fact]
        public async Task ShouldProcessExplicitProofRequests()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateNodeWithBlocks(witnessStore, 5);

            var requestQueue = new InMemoryProofRequestQueue();
            var service = CreateService(witnessStore, new MockBlockProver(),
                requestQueue: requestQueue,
                cadence: ProofCadence.Off);

            await service.RequestProofAsync(2);
            await service.RequestProofAsync(4);

            var pending = await requestQueue.GetPendingAsync();
            Assert.Equal(2, pending.Count);
            _output.WriteLine($"Queued {pending.Count} explicit requests");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var task = Task.Run(() => service.ExecuteAsync(cts.Token));

            while (!cts.IsCancellationRequested)
            {
                var status2 = await requestQueue.GetStatusAsync(2);
                var status4 = await requestQueue.GetStatusAsync(4);
                if (status2?.Status == ProofRequestStatus.Completed &&
                    status4?.Status == ProofRequestStatus.Completed)
                    break;
                await Task.Delay(50);
            }
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { }

            var proof2 = await witnessStore.GetProofAsync(2);
            var proof4 = await witnessStore.GetProofAsync(4);
            Assert.NotNull(proof2);
            Assert.NotNull(proof4);

            var proof1 = await witnessStore.GetProofAsync(1);
            var proof3 = await witnessStore.GetProofAsync(3);
            var proof5 = await witnessStore.GetProofAsync(5);
            Assert.Null(proof1);
            Assert.Null(proof3);
            Assert.Null(proof5);

            var status = await requestQueue.GetStatusAsync(2);
            Assert.Equal(ProofRequestStatus.Completed, status!.Status);
            _output.WriteLine("Block 2: proved (explicit request)");
            _output.WriteLine("Block 4: proved (explicit request)");
            _output.WriteLine("Blocks 1,3,5: not proved (no request, cadence=Off)");

            node.Dispose();
        }

        [Fact]
        public async Task ShouldTrackFailedRequestStatus()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateNodeWithBlocks(witnessStore, 1);

            var requestQueue = new InMemoryProofRequestQueue();
            var alwaysFailProver = new FailNTimesProver(100, new MockBlockProver());
            var service = CreateService(witnessStore, alwaysFailProver,
                requestQueue: requestQueue,
                cadence: ProofCadence.Off,
                maxRetries: 2);

            await service.RequestProofAsync(1);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var task = Task.Run(() => service.ExecuteAsync(cts.Token));
            await Task.Delay(1500);
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { }

            var status = await requestQueue.GetStatusAsync(1);
            Assert.NotNull(status);
            Assert.Equal(ProofRequestStatus.Failed, status!.Status);
            Assert.NotNull(status.LastError);
            _output.WriteLine($"Block 1: status={status.Status}, error={status.LastError}");

            node.Dispose();
        }

        [Fact]
        public async Task ShouldRecordMetrics()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateNodeWithBlocks(witnessStore, 3);

            using var metrics = new BlockProver.Server.Metrics.BlockProverMetrics("test");
            var failingProver = new FailNTimesProver(1, new MockBlockProver());
            var service = CreateService(witnessStore, failingProver, metrics: metrics);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var task = Task.Run(() => service.ExecuteAsync(cts.Token));
            while (!cts.IsCancellationRequested)
            {
                var remaining = await witnessStore.GetUnprovenBlockNumbersAsync();
                if (remaining.Count == 0) break;
                await Task.Delay(50);
            }
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { }

            Assert.Equal(3, metrics.LastProvenBlock);
            _output.WriteLine($"Metrics: LastProvenBlock={metrics.LastProvenBlock}");
            _output.WriteLine($"Metrics: QueueDepth={metrics.QueueDepth}");
            _output.WriteLine($"Metrics: FailedCount={metrics.FailedCount}");

            node.Dispose();
        }

        private class FailNTimesProver : IBlockProver
        {
            private readonly int _failCount;
            private readonly IBlockProver _inner;
            private int _attempts;

            public FailNTimesProver(int failCount, IBlockProver inner)
            {
                _failCount = failCount;
                _inner = inner;
            }

            public int FailureCount => System.Math.Min(_attempts, _failCount);

            public Task<BlockProofResult> ProveBlockAsync(byte[] witnessBytes,
                byte[] preStateRoot, byte[] postStateRoot, long blockNumber)
            {
                if (Interlocked.Increment(ref _attempts) <= _failCount)
                    throw new System.Exception($"Simulated failure #{_attempts}");
                return _inner.ProveBlockAsync(witnessBytes, preStateRoot, postStateRoot, blockNumber);
            }
        }
    }
}
