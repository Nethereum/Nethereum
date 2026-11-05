using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.SecurityAudit71.S2.PartB
{
    public class S2BalFixTests
    {
        private sealed class Peer : IBlockAccessListPeer
        {
            private readonly Func<IReadOnlyList<byte[]>, IReadOnlyList<byte[]>> _serve;
            private int _calls;

            public Peer(string id, Func<IReadOnlyList<byte[]>, IReadOnlyList<byte[]>> serve)
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

        private sealed class Source : IBlockAccessListPeerSource
        {
            private readonly IBlockAccessListPeer[] _peers;
            public Source(params IBlockAccessListPeer[] peers) { _peers = peers; }
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

        [Fact]
        public async Task S2_101_Given_AnHonestPeerThatLacksOneBlocksAccessList_When_TheWindowIsFetched_Then_ItKeepsServingTheOthersAndIsNotExcluded()
        {
            var blocks = Enumerable.Range(1, 140).Select(Block).ToArray();
            var byHash = blocks.ToDictionary(b => Convert.ToHexString(b.Ref.BlockHash), b => b.Raw);
            var lackedHash = Convert.ToHexString(blocks[3].Ref.BlockHash);
            var lacking = new Peer("lacking", hashes => hashes
                .Select(h => Convert.ToHexString(h) == lackedHash ? Array.Empty<byte>() : byHash[Convert.ToHexString(h)]).ToList());
            var complete = new Peer("complete", hashes => hashes.Select(h => byHash[Convert.ToHexString(h)]).ToList());
            var fetcher = new BlockAccessListFetcher(new Source(lacking, complete), new BlockAccessListVerifier())
            {
                PeerWaitInterval = TimeSpan.FromMilliseconds(1)
            };

            var result = await fetcher.FetchAsync(blocks.Select(b => b.Ref).ToArray(), new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token);

            Assert.Equal(blocks.Length, result.Count);
            Assert.All(result, list => Assert.NotNull(list));
            Assert.True(lacking.Calls >= 3, $"the peer that lacks one list was asked only {lacking.Calls} times");
        }

        [Fact]
        public async Task S2_101_Given_APeerThatFaultsOnceThenRecovers_When_TheWindowIsFetched_Then_ItIsStillAsked()
        {
            var blocks = Enumerable.Range(1, 140).Select(Block).ToArray();
            var byHash = blocks.ToDictionary(b => Convert.ToHexString(b.Ref.BlockHash), b => b.Raw);
            var failedOnce = false;
            var flaky = new Peer("flaky", hashes =>
            {
                if (!failedOnce) { failedOnce = true; throw new TimeoutException(); }
                return hashes.Select(h => byHash[Convert.ToHexString(h)]).ToList();
            });
            var fetcher = new BlockAccessListFetcher(new Source(flaky), new BlockAccessListVerifier())
            {
                PeerWaitInterval = TimeSpan.FromMilliseconds(1)
            };

            var result = await fetcher.FetchAsync(blocks.Select(b => b.Ref).ToArray(), new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token);

            Assert.Equal(blocks.Length, result.Count);
        }
    }
}
