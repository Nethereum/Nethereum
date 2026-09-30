using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Nodes.Rlp;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PathHealNodeSinkTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"healsink_{Guid.NewGuid():N}");
        private readonly RocksDbManager _mgr;

        public PathHealNodeSinkTests()
        {
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
        }

        public void Dispose()
        {
            _mgr.Dispose();
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        [Fact]
        public void HealWrittenTrie_IsByteIdentical_ToImporterKeying_AndOverwritesStaleLocations()
        {
            var src = new InMemoryContentNodeStore();
            var acctA = Hash32(0xAA);
            var acctB = Hash32(0x55);

            var storageSrc = new InMemoryContentNodeStore();
            var storageTrie = new PatriciaTrie(storageSrc);
            storageTrie.Put(Hash32(0x11), Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x09 }));
            storageTrie.Put(Hash32(0x22), Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x0A }));
            storageTrie.SaveNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var enc = new AccountEncoder();
            var stateTrie = new PatriciaTrie(src);
            stateTrie.Put(acctA, enc.Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)0,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            stateTrie.Put(acctB, enc.Encode(new Account
            {
                Nonce = (EvmUInt256)2,
                Balance = (EvmUInt256)5,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            stateTrie.SaveNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();

            var pathStore = new RocksDbPathTrieNodeStore(_mgr);
            var sink = new PathHealNodeSink(pathStore, _mgr);

            var junk = new byte[40];
            for (int i = 0; i < junk.Length; i++) junk[i] = 0xEE;
            var junkHash = Sha3Keccack.Current.CalculateHash(junk);
            sink.PutNode(false, null, Array.Empty<byte>(), junkHash, junk);
            sink.Flush();
            Assert.False(sink.HasNode(false, null, Array.Empty<byte>(), stateRoot),
                "a stale occupant at the right location must not satisfy an existence check");
            Assert.False(sink.HasRoot(stateRoot));

            HealCopy(stateRoot, src, storageSrc, storageRoot, acctA, sink);

            Assert.True(sink.HasRoot(stateRoot));

            var loadedState = PatriciaTrie.LoadFromStorage(stateRoot, pathStore);
            Assert.Equal(stateRoot, loadedState.Root.GetHash());
            Assert.NotNull(loadedState.Get(acctA));
            Assert.NotNull(loadedState.Get(acctB));

            var loadedStorage = PatriciaTrie.LoadFromStorage(storageRoot, pathStore, acctA);
            Assert.Equal(Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x09 }), loadedStorage.Get(Hash32(0x11)));
            Assert.Equal(Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x0A }), loadedStorage.Get(Hash32(0x22)));
        }

        [Fact]
        public void WipeStorage_RemovesStaleOwnerRange_AndBufferedPuts()
        {
            var pathStore = new RocksDbPathTrieNodeStore(_mgr);
            var sink = new PathHealNodeSink(pathStore, _mgr);
            var owner = Hash32(0x77);
            var other = Hash32(0x88);

            var stale = new byte[36];
            for (int i = 0; i < stale.Length; i++) stale[i] = 0xBB;
            var staleHash = Sha3Keccack.Current.CalculateHash(stale);

            sink.PutNode(true, owner, Array.Empty<byte>(), staleHash, stale);
            sink.Flush();
            sink.PutNode(true, owner, new byte[] { 0x01 }, staleHash, stale);
            sink.PutNode(true, other, Array.Empty<byte>(), staleHash, stale);

            sink.WipeStorage(owner);
            sink.Flush();

            Assert.False(sink.HasNode(true, owner, Array.Empty<byte>(), staleHash), "flushed stale root must be wiped");
            Assert.False(sink.HasNode(true, owner, new byte[] { 0x01 }, staleHash), "buffered put must not resurrect after wipe");
            Assert.True(sink.HasNode(true, other, Array.Empty<byte>(), staleHash), "other owners' ranges are untouched");
        }

        [Fact]
        public void ForceWipeStorage_RemovesRootAbsentOrphanChildren()
        {
            var pathStore = new RocksDbPathTrieNodeStore(_mgr);
            var sink = new PathHealNodeSink(pathStore, _mgr);
            var owner = Hash32(0x99);
            var blob = new byte[36];
            for (int i = 0; i < blob.Length; i++) blob[i] = 0xCE;
            var hash = Sha3Keccack.Current.CalculateHash(blob);
            var childPath = new byte[] { 0x01, 0x02 };

            sink.PutNode(true, owner, childPath, hash, blob);
            sink.Flush();

            sink.WipeStorage(owner);
            sink.Flush();
            Assert.True(sink.HasNode(true, owner, childPath, hash),
                "cheap wipe remains occupancy-gated when the storage root is absent");

            sink.ForceWipeStorage(owner);
            sink.Flush();
            Assert.False(sink.HasNode(true, owner, childPath, hash),
                "forced damage repair must remove rootless orphan children");
        }
        [Fact]
        public void HasNode_SeesBufferedWrites_BeforeFlush()
        {
            var pathStore = new RocksDbPathTrieNodeStore(_mgr);
            var sink = new PathHealNodeSink(pathStore, _mgr);
            var blob = new byte[36];
            for (int i = 0; i < blob.Length; i++) blob[i] = 0x42;
            var hash = Sha3Keccack.Current.CalculateHash(blob);

            sink.PutNode(false, null, new byte[] { 0x02, 0x03 }, hash, blob);

            Assert.True(sink.HasNode(false, null, new byte[] { 0x02, 0x03 }, hash),
                "existence checks must observe the write buffer within a round");
        }

        private static byte[] Hash32(byte first)
        {
            var h = new byte[32];
            h[0] = first;
            for (int i = 1; i < 32; i++) h[i] = (byte)(i * 3);
            return h;
        }

        private static void HealCopy(
            byte[] stateRoot, InMemoryContentNodeStore accountSrc, InMemoryContentNodeStore storageSrc,
            byte[] storageRoot, byte[] storageOwner, PathHealNodeSink sink)
        {
            var decoder = new NodeDecoder();
            var q = new Queue<(bool IsStorage, byte[] Owner, byte[] Path, byte[] Hash)>();
            q.Enqueue((false, null, Array.Empty<byte>(), stateRoot));

            while (q.Count > 0)
            {
                var (isStorage, owner, path, hash) = q.Dequeue();
                var src = isStorage ? storageSrc : accountSrc;
                var blob = src.Get(hash);
                Assert.NotNull(blob);
                sink.PutNode(isStorage, owner, path, hash, blob);

                var node = decoder.DecodeFromRlpData(blob, null, new byte[0], decodeHashNodes: false,
                    ContentAddressedNodeStore.Wrap(src));
                switch (node)
                {
                    case BranchNode branch:
                        for (int i = 0; i < 16; i++)
                            if (branch.Children[i] is HashNode bh)
                                q.Enqueue((isStorage, owner, ByteUtil.AppendByte(path, (byte)i), bh.Hash));
                        break;
                    case ExtendedNode ext:
                        if (ext.InnerNode is HashNode eh)
                            q.Enqueue((isStorage, owner, ByteUtil.Merge(path, ext.Nibbles), eh.Hash));
                        break;
                    case LeafNode leaf when !isStorage:
                        Account acc = null;
                        try { acc = new AccountEncoder().Decode(leaf.Value); } catch { }
                        if (acc?.StateRoot != null && ByteUtil.AreEqual(acc.StateRoot, storageRoot))
                            q.Enqueue((true, storageOwner, Array.Empty<byte>(), storageRoot));
                        break;
                }
            }
            sink.Flush();
        }
    }
}
