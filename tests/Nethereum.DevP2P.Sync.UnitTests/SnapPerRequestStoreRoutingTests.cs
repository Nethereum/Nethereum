using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapPerRequestStoreRoutingTests
    {
        private sealed class NullBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class FakeSelector : ISnapNodeStoreSelector
        {
            private readonly byte[] _match;
            private readonly ITrieNodeStore _store;
            public FakeSelector(byte[] matchRoot, ITrieNodeStore store) { _match = matchRoot; _store = store; }
            public Task<ITrieNodeStore> ResolveForRootAsync(byte[] stateRoot, CancellationToken ct = default)
                => Task.FromResult(ByteUtil.AreEqual(stateRoot, _match) ? _store : null);
        }

        private static readonly Sha3Keccack _keccak = new();
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();

        private static byte[] AddrHash(byte seed) => _keccak.CalculateHash(new byte[] { seed, 0xAA });

        private static byte[] EncodeEoa(int i)
            => new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)(uint)(i + 1),
                Balance = (EvmUInt256)((ulong)(i + 1) * 1000UL),
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH
            });

        private static (InMemoryContentNodeStore store, byte[] root, HashSet<string> hashes) BuildTrie(byte[] seeds)
        {
            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store, _hp);
            var hashes = new HashSet<string>();
            for (int i = 0; i < seeds.Length; i++)
            {
                var h = AddrHash(seeds[i]);
                trie.Put(h, EncodeEoa(i));
                hashes.Add(h.ToHex());
            }
            trie.SaveDirtyNodesToStorage();
            return (store, trie.Root.GetHash(), hashes);
        }

        [Fact]
        public async Task GetAccountRange_Routes_ByRoot_To_Selected_Store_Else_Default()
        {
            var (store1, r1, hashes1) = BuildTrie(new byte[] { 0x01, 0x02, 0x03 });
            var (store2, r2, hashes2) = BuildTrie(new byte[] { 0x11, 0x12, 0x13 });
            Assert.False(ByteUtil.AreEqual(r1, r2));

            var selector = new FakeSelector(r2, store2);
            var handler = new PatriciaSnapRequestHandler(store1, new NullBytecodes(), selector: selector);

            var respR1 = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 1,
                RootHash = r1,
                StartingHash = new byte[32],
                LimitHash = FilledHash(0xff),
                ResponseBytes = 1_000_000UL,
            });
            var respR2 = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 2,
                RootHash = r2,
                StartingHash = new byte[32],
                LimitHash = FilledHash(0xff),
                ResponseBytes = 1_000_000UL,
            });

            var gotR1 = respR1.Accounts.Select(a => a.Hash.ToHex()).ToHashSet();
            var gotR2 = respR2.Accounts.Select(a => a.Hash.ToHex()).ToHashSet();

            Assert.Equal(hashes1, gotR1);
            Assert.Empty(gotR1.Intersect(hashes2));

            Assert.Equal(hashes2, gotR2);
            Assert.Empty(gotR2.Intersect(hashes1));
        }

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }
    }
}
