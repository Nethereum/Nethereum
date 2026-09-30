using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class BalChainFixtureTests
    {
        private const string AddressA = "0x1000000000000000000000000000000000000001";
        private const string AddressB = "0x1000000000000000000000000000000000000002";
        private const string AddressC = "0x1000000000000000000000000000000000000003";
        private const string AddressE = "0x1000000000000000000000000000000000000005";

        private static AccountChanges Balance(string address, ulong balance, ulong? nonce = null)
        {
            var change = new AccountChanges(address);
            change.BalanceChanges.Add(new BalanceChange(0, balance));
            if (nonce.HasValue) change.NonceChanges.Add(new NonceChange(0, nonce.Value));
            return change;
        }

        private static AccountChanges Storage(string address, params (ulong Slot, ulong Value)[] slots)
        {
            var change = new AccountChanges(address);
            foreach (var (slot, value) in slots)
            {
                var slotChanges = new SlotChanges(slot);
                slotChanges.Changes.Add(new StorageChange(0, value));
                change.StorageChanges.Add(slotChanges);
            }
            return change;
        }

        private static BalChainFixture BuildThreeBlockChain()
        {
            var contract = Storage(AddressC, (1, 5), (2, 7));
            contract.CodeChanges.Add(new CodeChange(0, new byte[] { 0x60, 0x00, 0x60, 0x00, 0xf3 }));
            contract.NonceChanges.Add(new NonceChange(0, 1));
            var genesis = new List<AccountChanges> { Balance(AddressA, 100, 1), contract, Balance(AddressE, 3) };

            var block1 = new List<AccountChanges> { Balance(AddressA, 90), Balance(AddressB, 10) };
            var block2 = new List<AccountChanges> { Storage(AddressC, (1, 0), (3, 9)), Balance(AddressA, 80, 2) };
            var block3 = new List<AccountChanges> { Balance(AddressE, 0), Balance(AddressB, 20, 1) };

            return BalChainFixture.Build(genesis, new List<IReadOnlyList<AccountChanges>> { block1, block2, block3 });
        }

        [Fact]
        public async Task Given_ABalChainFixtureWithThreeBlocks_When_Built_Then_EachHeaderLinksToItsParentAndEachBalVerifiesAgainstItsHeader()
        {
            var fixture = BuildThreeBlockChain();

            Assert.Equal(3UL, fixture.Tip);
            for (ulong n = 0; n <= fixture.Tip; n++)
                Assert.Equal(RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(fixture.HeaderAt(n)), fixture.HashAt(n));
            for (ulong n = 1; n <= fixture.Tip; n++)
                Assert.Equal(fixture.HashAt(n - 1), fixture.HeaderAt(n).ParentHash);

            var blocks = Enumerable.Range(1, 3).Select(n => (ulong)n).ToList();
            var peer = fixture.CreateBlockAccessListPeerSource().GetServiceablePeers().Single();
            var raw = await peer.RequestBlockAccessListsAsync(
                blocks.Select(fixture.HashAt).ToList(), BlockAccessListFetcher.ResponseByteBudget, CancellationToken.None);
            var verified = new BlockAccessListVerifier().Verify(
                blocks.Select(n => fixture.HeaderAt(n).BlockAccessListHash).ToList(), raw);

            Assert.All(verified, v => Assert.Equal(BlockAccessListStatus.Verified, v.Status));
            Assert.Equal(blocks.Select(n => fixture.AccessListAt(n).Count), verified.Select(v => v.AccessList.Count));

            var roots = Enumerable.Range(0, 4).Select(n => fixture.HeaderAt((ulong)n).StateRoot).ToList();
            Assert.Equal(4, roots.Distinct(ByteArrayComparer.Current).Count());
            var snapPeer = fixture.CreateSnapPeer();
            var accountCounts = new List<int>();
            foreach (var root in roots)
            {
                var range = await snapPeer.GetAccountRangeAsync(new GetAccountRangeMessage
                {
                    RootHash = root,
                    StartingHash = new byte[32],
                    LimitHash = Enumerable.Repeat((byte)0xff, 32).ToArray(),
                    ResponseBytes = 512 * 1024,
                });
                accountCounts.Add(range.Accounts.Count);
            }
            Assert.Equal(new[] { 3, 4, 4, 3 }, accountCounts);

            var dir = Path.Combine(Path.GetTempPath(), $"balfixture_{Guid.NewGuid():N}");
            try
            {
                using (var bundle = RocksDbChainStoreBundle.Open(dir))
                {
                    await fixture.LayCanonicalAsync(bundle);
                    for (ulong n = 0; n <= fixture.Tip; n++)
                    {
                        Assert.Equal(fixture.HashAt(n), await bundle.Blocks.GetHashByNumberAsync(new BigInteger(n)));
                        var stored = await bundle.Blocks.GetByHashAsync(fixture.HashAt(n));
                        Assert.Equal(fixture.HashAt(n), RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(stored));
                    }
                    Assert.Equal(fixture.Tip, HeaderSubchains.TrustedTip(bundle.Metadata.GetHeaderSyncState()));
                }
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }
}
