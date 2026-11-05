using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.SecurityAudit71.S2.PartB
{
    public class S2BalFetchPeerFaultTests
    {
        private sealed class ScriptedPeer : IBlockAccessListPeer
        {
            private readonly Func<IReadOnlyList<byte[]>, IReadOnlyList<byte[]>> _serve;
            private int _calls;

            public ScriptedPeer(string id, Func<IReadOnlyList<byte[]>, IReadOnlyList<byte[]>> serve)
            {
                Id = id;
                _serve = serve;
            }

            public string Id { get; }

            public int Calls => Volatile.Read(ref _calls);

            public Task<IReadOnlyList<byte[]>> RequestBlockAccessListsAsync(
                IReadOnlyList<byte[]> blockHashes, ulong responseBytes, CancellationToken ct)
            {
                Interlocked.Increment(ref _calls);
                return Task.FromResult(_serve(blockHashes));
            }
        }

        private sealed class FixedSource : IBlockAccessListPeerSource
        {
            private readonly IBlockAccessListPeer[] _peers;
            public FixedSource(params IBlockAccessListPeer[] peers) { _peers = peers; }
            public IReadOnlyList<IBlockAccessListPeer> GetServiceablePeers() => _peers;
        }

        private static (BalBlockRef Ref, byte[] Raw) Block(int index)
        {
            var account = new AccountChanges("0x" + index.ToString("x4") + new string('0', 36));
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256((ulong)(index + 1))));
            var bal = new List<AccountChanges> { account };
            var blockHash = new byte[32];
            blockHash[0] = (byte)(index >> 8);
            blockHash[1] = (byte)index;
            blockHash[2] = 0x5a;
            return (new BalBlockRef(blockHash, BlockAccessListRLPEncoder.Current.Hash(bal)), BlockAccessListRLPEncoder.Current.Encode(bal));
        }

        private static Func<IReadOnlyList<byte[]>, IReadOnlyList<byte[]>> HonestServing(IEnumerable<(BalBlockRef Ref, byte[] Raw)> blocks)
        {
            var byHash = blocks.ToDictionary(b => Convert.ToHexString(b.Ref.BlockHash), b => b.Raw);
            return hashes => hashes.Select(h => byHash[Convert.ToHexString(h)]).ToList();
        }

        [Fact]
        public async Task S2_101_Given_APeerWhoseEveryRequestFaults_When_AnHonestPeerServesTheWindowInSeveralBatches_Then_TheFaultingPeerIsNotAskedAgainEveryRound()
        {
            var blocks = Enumerable.Range(1, 140).Select(Block).ToArray();
            var faulting = new ScriptedPeer("faulting", _ => throw new TimeoutException("peer never answers"));
            var honest = new ScriptedPeer("honest", HonestServing(blocks));
            var fetcher = new BlockAccessListFetcher(new FixedSource(faulting, honest), new BlockAccessListVerifier())
            {
                PeerWaitInterval = TimeSpan.FromMilliseconds(1)
            };

            var result = await fetcher.FetchAsync(blocks.Select(b => b.Ref).ToArray(), new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token);

            Assert.Equal(blocks.Length, result.Count);
            Assert.True(honest.Calls >= 5);
            Assert.True(faulting.Calls <= 2, $"the faulting peer was asked {faulting.Calls} times while the honest peer needed {honest.Calls} requests; with the 60 s request timeout of SyncPeerSession each ask stalls the window by a minute");
        }

        [Fact]
        public async Task S2_101_Given_APeerThatReturnsUndecodableBytes_When_AnHonestPeerServesTheWindowInSeveralBatches_Then_TheMalformedPeerIsNotAskedAgainEveryRound()
        {
            var blocks = Enumerable.Range(1, 140).Select(Block).ToArray();
            var malformed = new ScriptedPeer("malformed", _ => throw new InvalidOperationException("RLP list holds more than 128 items"));
            var honest = new ScriptedPeer("honest", HonestServing(blocks));
            var fetcher = new BlockAccessListFetcher(new FixedSource(malformed, honest), new BlockAccessListVerifier())
            {
                PeerWaitInterval = TimeSpan.FromMilliseconds(1)
            };

            await fetcher.FetchAsync(blocks.Select(b => b.Ref).ToArray(), new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token);

            Assert.True(malformed.Calls <= 2, $"the malformed-reply peer was asked {malformed.Calls} times");
        }
    }
}
