using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Sync
{
    public class FollowerServiceValidatingRewindTests
    {
        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static byte[] FillBytes(byte v) => Enumerable.Repeat(v, 32).ToArray();

        private static byte[] RootForBlock(ulong blockNumber) => FillBytes((byte)blockNumber);
        private static byte[] HashForBlock(ulong blockNumber) => FillBytes((byte)(blockNumber + 0x80));

        private static BlockBundle MakeBundleFor(ulong blockNumber)
            => new BlockBundle(
                Header: new BlockHeader { BlockNumber = blockNumber, StateRoot = RootForBlock(blockNumber) },
                Transactions: new List<ISignedTransaction>(),
                Uncles: new List<BlockHeader>(),
                Withdrawals: null,
                HeaderHash: HashForBlock(blockNumber));

        private static async Task SaveHeaderAsync(IBlockStore blocks, ulong number)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = new byte[32],
                StateRoot = RootForBlock(number),
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                ExtraData = System.Array.Empty<byte>(),
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = 0,
                GasLimit = 0,
                GasUsed = 0,
                Timestamp = 0,
                MixHash = new byte[32],
                Nonce = new byte[8],
            };
            await blocks.SaveAsync(header, HashForBlock(number));
        }

        private static async Task SeedJournalledChainAsync(
            InMemoryChainStoreBundle bundle, ulong fromBlock, ulong toBlock)
        {
            var journal = (IHistoricalStateProvider)bundle.State;
            await SaveHeaderAsync(bundle.Blocks, fromBlock);
            await bundle.State.SaveAccountAsync(AddrA, new Account { Balance = 100, Nonce = 0 });
            bundle.Metadata.Commit(fromBlock, HashForBlock(fromBlock));

            for (ulong n = fromBlock + 1; n <= toBlock; n++)
            {
                await SaveHeaderAsync(bundle.Blocks, n);
                journal.SetCurrentBlockNumber((long)n);
                await bundle.State.SaveAccountAsync(AddrA,
                    new Account { Balance = 100 + (int)n, Nonce = n });
                await journal.ClearCurrentBlockNumberAsync();
                bundle.Metadata.Commit(n, HashForBlock(n));
            }
        }

        private sealed class ScriptedExecutor : IBlockExecutor
        {
            private readonly Dictionary<ulong, bool> _matchByBlock;
            public int CallCount { get; private set; }
            public List<ulong> CallSequence { get; } = new();

            public ScriptedExecutor(Dictionary<ulong, bool> matchByBlock)
            {
                _matchByBlock = matchByBlock;
            }

            public Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header,
                IList<ISignedTransaction> transactions,
                IList<BlockHeader> uncles,
                IList<WithdrawalEntry> withdrawals,
                CancellationToken ct)
            {
                CallCount++;
                var bn = (ulong)header.BlockNumber;
                CallSequence.Add(bn);
                var match = !_matchByBlock.TryGetValue(bn, out var v) || v;
                return Task.FromResult(new BlockImporterResult
                {
                    ComputedStateRoot = match ? header.StateRoot : FillBytes(0xFF),
                    ExpectedStateRoot = header.StateRoot,
                    StateRootMismatch = !match,
                });
            }
        }

        private sealed class RewindOncePolicy : IValidationPolicy
        {
            private readonly ulong _rewindAt;
            private int _rewindsIssued;

            public RewindOncePolicy(ulong rewindAt) { _rewindAt = rewindAt; }
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber)
            {
                if (blockNumber == _rewindAt && _rewindsIssued == 0)
                {
                    _rewindsIssued++;
                    return ValidationAction.RewindAndRetry;
                }
                return ValidationAction.Continue;
            }
        }

        private sealed class AlwaysRewindPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber)
                => ValidationAction.RewindAndRetry;
        }

        private sealed class StubCanonicalSource : ICanonicalStateRootSource
        {
            private readonly Dictionary<ulong, byte[]> _byBlock;
            private readonly HashSet<ulong> _unavailable;
            private readonly bool _throwOnCall;

            public List<ulong> Queries { get; } = new();
            public string Name => "Stub";

            public StubCanonicalSource(
                Dictionary<ulong, byte[]> byBlock = null,
                HashSet<ulong> unavailable = null,
                bool throwOnCall = false)
            {
                _byBlock = byBlock ?? new Dictionary<ulong, byte[]>();
                _unavailable = unavailable ?? new HashSet<ulong>();
                _throwOnCall = throwOnCall;
            }

            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(
                ulong blockNumber, CancellationToken ct)
            {
                Queries.Add(blockNumber);
                if (_throwOnCall) throw new InvalidOperationException("stub canonical: simulated transport failure");
                if (_unavailable.Contains(blockNumber)) return Task.FromResult<(byte[], byte[])>((null, null));
                if (!_byBlock.TryGetValue(blockNumber, out var root)) return Task.FromResult<(byte[], byte[])>((null, null));
                return Task.FromResult<(byte[], byte[])>((root, HashForBlock(blockNumber)));
            }
        }

        // ----------------------------------------------------------------------
        // Test 1: canonical agrees at N-1 — 1 cycle, resume from N.
        // ----------------------------------------------------------------------
        [Fact]
        public async Task ValidatingRewind_CanonicalAgreesAtNMinusOne_RewindsOneCycle_ResumesAtN()
        {
            using var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            await SeedJournalledChainAsync(bundle, fromBlock: 0, toBlock: 5);

            var deliveries = new List<BlockBundle> { MakeBundleFor(6) };
            var source = new LocalReplayBlockSource(deliveries);
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>
            {
                { 6, false },
            });

            var canonical = new StubCanonicalSource(new Dictionary<ulong, byte[]>
            {
                { 5, RootForBlock(5) },
            });
            var policy = new RewindOncePolicy(rewindAt: 6);
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: policy,
                canonical: canonical,
                options: new FollowerOptions(StartBlock: 6, CheckpointEvery: 0, AnchorEvery: 0, MaxConsecutiveDivergences: 5, MaxRewindCycles: 5),
                ct: default);

            Assert.Equal(1UL, result.RewindCyclesUsed);
            // canonical is queried by the verdict diagnostic at the failing
            // block (6) AND by the validating rewind at the prior head (5).
            Assert.Contains(6UL, canonical.Queries);
            Assert.Contains(5UL, canonical.Queries);
            Assert.Equal(5UL, bundle.Metadata.GetLastBlock());
        }

        // ----------------------------------------------------------------------
        // Test 2: canonical disagrees at N-1, N-2, N-3, agrees at N-4 — 4 cycles.
        // After rewind to block 2 (canonical matches), resume from block 3.
        // ----------------------------------------------------------------------
        [Fact]
        public async Task ValidatingRewind_CanonicalAgreesAtNMinusFour_RewindsFourCycles_ResumesAtCorrectHead()
        {
            using var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            await SeedJournalledChainAsync(bundle, fromBlock: 0, toBlock: 5);

            var deliveries = new List<BlockBundle>
            {
                MakeBundleFor(6),
            };
            var source = new LocalReplayBlockSource(deliveries);
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>
            {
                { 6, false },
            });

            // Canonical disagrees at 5, 4, 3, agrees at 2.
            var canonical = new StubCanonicalSource(new Dictionary<ulong, byte[]>
            {
                { 5, FillBytes(0xCC) },
                { 4, FillBytes(0xCC) },
                { 3, FillBytes(0xCC) },
                { 2, RootForBlock(2) },
            });
            var policy = new RewindOncePolicy(rewindAt: 6);
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: policy,
                canonical: canonical,
                options: new FollowerOptions(StartBlock: 6, CheckpointEvery: 0, AnchorEvery: 0, MaxConsecutiveDivergences: 8, MaxRewindCycles: 8),
                ct: default);

            Assert.Equal(4UL, result.RewindCyclesUsed);
            // canonical queried by diagnostic at failing block 6, then by
            // validating rewind at 5, 4, 3, 2. Possibly again at 6 after
            // resume if the policy returns Continue on the second mismatch.
            Assert.Contains(5UL, canonical.Queries);
            Assert.Contains(4UL, canonical.Queries);
            Assert.Contains(3UL, canonical.Queries);
            Assert.Contains(2UL, canonical.Queries);
            // After rewind to block 2, the metadata head is 2.
            Assert.Equal(2UL, bundle.Metadata.GetLastBlock());
        }

        // ----------------------------------------------------------------------
        // Test 3: canonical throws during cycle — falls back to single-shot
        // (i.e. the first rewind succeeds without further canonical checks).
        // ----------------------------------------------------------------------
        [Fact]
        public async Task ValidatingRewind_CanonicalThrows_FallsBackToSingleShot()
        {
            using var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            await SeedJournalledChainAsync(bundle, fromBlock: 0, toBlock: 3);

            var deliveries = new List<BlockBundle> { MakeBundleFor(4), MakeBundleFor(4) };
            var source = new LocalReplayBlockSource(deliveries);
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>
            {
                { 4, false },
            });

            var canonical = new StubCanonicalSource(throwOnCall: true);
            var policy = new RewindOncePolicy(rewindAt: 4);
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: policy,
                canonical: canonical,
                options: new FollowerOptions(StartBlock: 4, CheckpointEvery: 0, AnchorEvery: 0, MaxConsecutiveDivergences: 5, MaxRewindCycles: 5),
                ct: default);

            Assert.Equal(1UL, result.RewindCyclesUsed);
            // canonical is queried first by the diagnostic at the failing
            // block (4, throws → caught as SourceUnavailable verdict), then
            // by the validating rewind at currentHead (3, throws → falls
            // back to single-shot, no further canonical queries).
            Assert.Contains(4UL, canonical.Queries);
            Assert.Contains(3UL, canonical.Queries);
            Assert.DoesNotContain(2UL, canonical.Queries);
        }

        // ----------------------------------------------------------------------
        // Test 4: MaxRewindCycles exhausted without finding a match.
        // ----------------------------------------------------------------------
        [Fact]
        public async Task ValidatingRewind_MaxRewindCyclesExceeded_ReturnsFatalVerdict()
        {
            using var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            await SeedJournalledChainAsync(bundle, fromBlock: 0, toBlock: 9);

            var deliveries = Enumerable.Range(0, 5).Select(_ => MakeBundleFor(10)).ToList();
            var source = new LocalReplayBlockSource(deliveries);
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>
            {
                { 10, false },
            });

            // Canonical disagrees everywhere we ask (full coverage so source
            // never returns null and fallback is not triggered).
            var canonical = new StubCanonicalSource(new Dictionary<ulong, byte[]>
            {
                { 10, FillBytes(0xCC) },
                { 9, FillBytes(0xCC) },
                { 8, FillBytes(0xCC) },
                { 7, FillBytes(0xCC) },
                { 6, FillBytes(0xCC) },
                { 5, FillBytes(0xCC) },
                { 4, FillBytes(0xCC) },
                { 3, FillBytes(0xCC) },
                { 2, FillBytes(0xCC) },
                { 1, FillBytes(0xCC) },
                { 0, FillBytes(0xCC) },
            });
            var policy = new AlwaysRewindPolicy();
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: policy,
                canonical: canonical,
                options: new FollowerOptions(StartBlock: 10, CheckpointEvery: 0, AnchorEvery: 0, MaxConsecutiveDivergences: 20, MaxRewindCycles: 3),
                ct: default);

            Assert.Equal(FollowerExitReason.FatalVerdict, result.ExitReason);
            Assert.Equal(4UL, result.RewindCyclesUsed);
            Assert.Contains("MaxRewindCycles", result.Detail);
        }
    }
}
