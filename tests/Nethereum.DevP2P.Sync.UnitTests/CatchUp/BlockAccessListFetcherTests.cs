using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.CatchUp
{
    public class BlockAccessListFetcherTests
    {
        private static (BalBlockRef Ref, byte[] Raw, EvmUInt256 Balance) MakeBlock(byte seed)
        {
            var balance = new EvmUInt256((ulong)(seed + 1));
            var account = new AccountChanges("0x" + seed.ToString("x2") + new string('0', 38));
            account.BalanceChanges.Add(new BalanceChange(0, balance));
            var bal = new List<AccountChanges> { account };
            var raw = BlockAccessListRLPEncoder.Current.Encode(bal);
            var balHash = BlockAccessListRLPEncoder.Current.Hash(bal);
            var blockHash = new byte[32];
            blockHash[0] = seed;
            blockHash[1] = seed;
            return (new BalBlockRef(blockHash, balHash), raw, balance);
        }

        private static BlockAccessListFetcher Fetcher(params IBlockAccessListPeer[] peers)
            => new BlockAccessListFetcher(new FakeSource(peers), new BlockAccessListVerifier())
            { PeerWaitInterval = TimeSpan.FromMilliseconds(1) };

        private static BlockAccessListFetcher FetcherOver(FakeSource source)
            => new BlockAccessListFetcher(source, new BlockAccessListVerifier())
            { PeerWaitInterval = TimeSpan.FromMilliseconds(1) };

        private static CancellationToken ShortDeadline()
            => new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;

        [Fact]
        public async Task FetchAsync_OnePeerServesAll_ReturnsAccessListsInOrder()
        {
            var a = MakeBlock(0x10);
            var b = MakeBlock(0x20);
            var peer = new FakePeer("p1", a, b);

            var result = await Fetcher(peer).FetchAsync(new[] { a.Ref, b.Ref }, CancellationToken.None);

            Assert.Equal(a.Balance, Assert.Single(result[0]).BalanceChanges[0].PostBalance);
            Assert.Equal(b.Balance, Assert.Single(result[1]).BalanceChanges[0].PostBalance);
        }

        [Fact]
        public async Task FetchAsync_BlockMissingFromFirstPeer_RetriesOnAnotherPeer()
        {
            var a = MakeBlock(0x10);
            var b = MakeBlock(0x20);
            var peer1 = new FakePeer("p1", a);
            var peer2 = new FakePeer("p2", a, b);

            var result = await Fetcher(peer1, peer2).FetchAsync(new[] { a.Ref, b.Ref }, CancellationToken.None);

            Assert.Equal(a.Balance, Assert.Single(result[0]).BalanceChanges[0].PostBalance);
            Assert.Equal(b.Balance, Assert.Single(result[1]).BalanceChanges[0].PostBalance);
        }

        [Fact]
        public async Task FetchAsync_BlockUnavailableFromEveryPeer_Throws()
        {
            var a = MakeBlock(0x10);
            var peer1 = new FakePeer("p1");
            var peer2 = new FakePeer("p2");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => Fetcher(peer1, peer2).FetchAsync(new[] { a.Ref }, CancellationToken.None));
        }

        [Fact]
        public async Task Given_NoPeersAreConnectedYet_When_APeerJoinsLater_Then_TheFetchWaitsAndCompletes()
        {
            var a = MakeBlock(0x10);
            var joiner = new FakePeer("p1", a);
            var source = new FakeSource(Array.Empty<IBlockAccessListPeer>()) { JoinAfterPolls = 3, Joiner = joiner };

            var result = await FetcherOver(source).FetchAsync(new[] { a.Ref }, ShortDeadline());

            Assert.True(source.Polls >= 3);
            Assert.Equal(a.Balance, Assert.Single(result[0]).BalanceChanges[0].PostBalance);
        }

        [Fact]
        public async Task Given_NoPeerEverJoins_When_TheCallerCancels_Then_TheFetchIsCancelledRatherThanReportingSuccess()
        {
            var a = MakeBlock(0x10);
            var source = new FakeSource(Array.Empty<IBlockAccessListPeer>());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => FetcherOver(source).FetchAsync(new[] { a.Ref }, new CancellationTokenSource(200).Token));
        }

        [Fact]
        public async Task Given_ARoundWhereEveryRequestFails_When_TheNextRoundSucceeds_Then_TheFetchCompletes()
        {
            var a = MakeBlock(0x10);
            var peer = new FakePeer("p1", a) { ThrowFirstCalls = 2 };

            var result = await Fetcher(peer).FetchAsync(new[] { a.Ref }, ShortDeadline());

            Assert.Equal(3, peer.Calls);
            Assert.Equal(a.Balance, Assert.Single(result[0]).BalanceChanges[0].PostBalance);
        }

        [Fact]
        public async Task Given_APeerWhoseRequestsAlwaysFail_When_TheCallerCancels_Then_TheFetchIsCancelledRatherThanReportingSuccess()
        {
            var a = MakeBlock(0x10);
            var peer = new FakePeer("p1", a) { ThrowFirstCalls = int.MaxValue };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => Fetcher(peer).FetchAsync(new[] { a.Ref }, new CancellationTokenSource(200).Token));
        }

        [Fact]
        public async Task Given_APeerSendsOneBalThatFailsTheHashCheck_When_LaterBlocksRemain_Then_ItIsNotAskedAgainThisCycle()
        {
            var blocks = Enumerable.Range(1, 30).Select(i => MakeBlock((byte)i)).ToArray();
            var liar = new FakePeer("liar", blocks) { CorruptHash = blocks[0].Ref.BlockHash.ToHex() };
            var honest = new FakePeer("honest", blocks) { TruncateTo = 1 };

            var result = await Fetcher(liar, honest).FetchAsync(blocks.Select(x => x.Ref).ToArray(), ShortDeadline());

            Assert.Equal(1, liar.Calls);
            for (var i = 0; i < blocks.Length; i++)
                Assert.Equal(blocks[i].Balance, Assert.Single(result[i]).BalanceChanges[0].PostBalance);
        }

        [Fact]
        public async Task Given_APeerOnlyRefusesOneBlock_When_LaterBlocksRemain_Then_ItIsStillAskedRatherThanExcluded()
        {
            var blocks = Enumerable.Range(1, 30).Select(i => MakeBlock((byte)i)).ToArray();
            var partial = new FakePeer("partial", blocks.Skip(1).ToArray());
            var honest = new FakePeer("honest", blocks) { TruncateTo = 1 };

            var result = await Fetcher(partial, honest).FetchAsync(blocks.Select(x => x.Ref).ToArray(), ShortDeadline());

            Assert.True(partial.Calls >= 2);
            for (var i = 0; i < blocks.Length; i++)
                Assert.Equal(blocks[i].Balance, Assert.Single(result[i]).BalanceChanges[0].PostBalance);
        }

        [Fact]
        public async Task Given_TheOnlyPeerSendsABalThatFailsTheHashCheck_When_NoOtherPeerExists_Then_TheFetchFailsAsPeersExhausted()
        {
            var a = MakeBlock(0x10);
            var liar = new FakePeer("liar", a) { CorruptEveryEntry = true };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => Fetcher(liar).FetchAsync(new[] { a.Ref }, ShortDeadline()));

            Assert.Contains("excluded for misbehaviour", ex.Message);
        }

        [Fact]
        public async Task FetchAsync_MoreBlocksThanRequestCap_BatchesAtMost28PerRequest()
        {
            var blocks = Enumerable.Range(1, 30).Select(i => MakeBlock((byte)i)).ToArray();
            var peer = new FakePeer("p1", blocks);

            var result = await Fetcher(peer).FetchAsync(blocks.Select(x => x.Ref).ToArray(), CancellationToken.None);

            Assert.Equal(30, result.Count);
            Assert.All(result, r => Assert.NotNull(r));
            Assert.Equal(BlockAccessListFetcher.MaxHashesPerRequest, peer.MaxBatchSeen);
        }

        [Fact]
        public async Task FetchAsync_PeerTruncatesResponse_ReServesTailUntilComplete()
        {
            var blocks = Enumerable.Range(1, 5).Select(i => MakeBlock((byte)i)).ToArray();
            var peer = new FakePeer("p1", blocks) { TruncateTo = 2 };

            var result = await Fetcher(peer).FetchAsync(blocks.Select(x => x.Ref).ToArray(), CancellationToken.None);

            Assert.Equal(5, result.Count);
            for (var i = 0; i < 5; i++)
                Assert.Equal(blocks[i].Balance, Assert.Single(result[i]).BalanceChanges[0].PostBalance);
        }

        [Fact]
        public async Task Given_APeerReturnsMoreBalsThanRequested_When_LaterBlocksRemain_Then_ItIsExcludedAsAProtocolViolationNotMerelyRefused()
        {
            var blocks = Enumerable.Range(1, 30).Select(i => MakeBlock((byte)i)).ToArray();
            var flooder = new FakePeer("flooder", blocks) { ExtraEntries = 3 };
            var honest = new FakePeer("honest", blocks) { TruncateTo = 1 };

            var result = await Fetcher(flooder, honest).FetchAsync(blocks.Select(x => x.Ref).ToArray(), ShortDeadline());

            Assert.Equal(1, flooder.Calls);
            for (var i = 0; i < blocks.Length; i++)
                Assert.Equal(blocks[i].Balance, Assert.Single(result[i]).BalanceChanges[0].PostBalance);
        }

        [Fact]
        public async Task Given_TheOnlyPeerReturnsMoreBalsThanRequested_When_NoOtherPeerExists_Then_TheFetchFailsRatherThanReportingSuccess()
        {
            var a = MakeBlock(0x10);
            var flooder = new FakePeer("flooder", a) { ExtraEntries = 3 };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => Fetcher(flooder).FetchAsync(new[] { a.Ref }, ShortDeadline()));

            Assert.Contains("excluded for misbehaviour", ex.Message);
        }

        [Fact]
        public async Task Given_APeerReturnsAWhollyEmptyResponse_When_AnotherPeerHasTheData_Then_TheRefusingPeerIsMarkedStatelessAndTheBatchIsServedElsewhere()
        {
            var a = MakeBlock(0x10);
            var refusing = new FakePeer("refusing", a) { WholeResponseEmpty = true };
            var honest = new FakePeer("honest", a);

            var result = await Fetcher(refusing, honest).FetchAsync(new[] { a.Ref }, ShortDeadline());

            Assert.Equal(a.Balance, Assert.Single(result[0]).BalanceChanges[0].PostBalance);
            Assert.Equal(1, refusing.Calls);
        }

        [Fact]
        public async Task Given_TheOnlyPeerReturnsAWhollyEmptyResponse_When_NoOtherPeerExists_Then_TheFetchFailsRatherThanRetryingTheSamePeerForever()
        {
            var a = MakeBlock(0x10);
            var refusing = new FakePeer("refusing", a) { WholeResponseEmpty = true };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => Fetcher(refusing).FetchAsync(new[] { a.Ref }, ShortDeadline()));

            Assert.Contains("excluded for misbehaviour", ex.Message);
            Assert.Equal(1, refusing.Calls);
        }

        [Fact]
        public async Task Given_APeerReturnsFewerEntriesThanRequested_When_ItIsAskedAgain_Then_ItIsNotConflatedWithAWhollyEmptyResponse()
        {
            var blocks = Enumerable.Range(1, 5).Select(i => MakeBlock((byte)i)).ToArray();
            var partial = new FakePeer("p1", blocks) { TruncateTo = 2 };

            var result = await Fetcher(partial).FetchAsync(blocks.Select(x => x.Ref).ToArray(), ShortDeadline());

            Assert.True(partial.Calls >= 2);
            for (var i = 0; i < blocks.Length; i++)
                Assert.Equal(blocks[i].Balance, Assert.Single(result[i]).BalanceChanges[0].PostBalance);
        }

        [Fact]
        public async Task Given_APeerReturnsPerPositionUnavailableEntries_When_TheArrayLengthMatchesTheBatch_Then_ItIsRefusedPerBlockNotMarkedWhollyStateless()
        {
            var a = MakeBlock(0x10);
            var b = MakeBlock(0x20);
            var partial = new FakePeer("p1", a);
            var honest = new FakePeer("honest", a, b);

            var result = await Fetcher(partial, honest).FetchAsync(new[] { a.Ref, b.Ref }, ShortDeadline());

            Assert.Equal(a.Balance, Assert.Single(result[0]).BalanceChanges[0].PostBalance);
            Assert.Equal(b.Balance, Assert.Single(result[1]).BalanceChanges[0].PostBalance);
            Assert.True(partial.Calls >= 1);
        }

        private sealed class FakePeer : IBlockAccessListPeer
        {
            private readonly Dictionary<string, byte[]> _available = new();
            public int MaxBatchSeen;
            public int TruncateTo;
            public int ExtraEntries;
            public int ThrowFirstCalls;
            public bool CorruptEveryEntry;
            public string CorruptHash;
            public bool WholeResponseEmpty;
            public int Calls;
            public readonly List<string> RequestedHashes = new();

            public FakePeer(string id, params (BalBlockRef Ref, byte[] Raw, EvmUInt256 Balance)[] blocks)
            {
                Id = id;
                foreach (var block in blocks) _available[block.Ref.BlockHash.ToHex()] = block.Raw;
            }

            public string Id { get; }

            public Task<IReadOnlyList<byte[]>> RequestBlockAccessListsAsync(
                IReadOnlyList<byte[]> blockHashes, ulong responseBytes, CancellationToken ct)
            {
                Calls++;
                foreach (var h in blockHashes) RequestedHashes.Add(h.ToHex());
                if (Calls <= ThrowFirstCalls) throw new IOException("peer request failed");
                if (WholeResponseEmpty) return Task.FromResult<IReadOnlyList<byte[]>>(Array.Empty<byte[]>());

                MaxBatchSeen = Math.Max(MaxBatchSeen, blockHashes.Count);
                var entries = blockHashes
                    .Select(h => _available.TryGetValue(h.ToHex(), out var v)
                        ? (CorruptEveryEntry || h.ToHex() == CorruptHash ? Corrupt(v) : v)
                        : new byte[0])
                    .ToList();
                if (TruncateTo > 0 && entries.Count > TruncateTo)
                    entries = entries.Take(TruncateTo).ToList();
                for (var i = 0; i < ExtraEntries; i++)
                    entries.Add(new byte[0]);
                return Task.FromResult<IReadOnlyList<byte[]>>(entries);
            }

            private static byte[] Corrupt(byte[] raw)
            {
                var copy = (byte[])raw.Clone();
                copy[copy.Length - 1] ^= 0xFF;
                return copy;
            }
        }

        private sealed class FakeSource : IBlockAccessListPeerSource
        {
            private readonly IReadOnlyList<IBlockAccessListPeer> _peers;
            public int Polls;
            public int JoinAfterPolls;
            public IBlockAccessListPeer Joiner;

            public FakeSource(IBlockAccessListPeer[] peers) => _peers = peers;

            public IReadOnlyList<IBlockAccessListPeer> GetServiceablePeers()
            {
                Polls++;
                if (Joiner != null && Polls >= JoinAfterPolls)
                    return new[] { Joiner };
                return _peers;
            }
        }
    }
}
