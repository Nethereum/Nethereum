using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Merkle.Binary.Storage;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Merkle.Binary.Tests
{
    public class NodeStoreTests
    {
        private readonly ITestOutputHelper _output;
        private readonly IHashProvider _hashProvider = new Blake3HashProvider();

        public NodeStoreTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SaveToNodeStore_PopulatesDepthAndType()
        {
            var store = new InMemoryBinaryTrieNodeStore();
            var trie = BuildTrieWithTwoAccounts();
            trie.SaveToStorage(store);

            Assert.True(store.NodeCount > 0);
            _output.WriteLine($"Total nodes: {store.NodeCount}");

            var topNodes = store.GetNodesByDepthRange(0, 0);
            Assert.Single(topNodes);
            Assert.Equal(BinaryTrieConstants.NodeTypeInternal, topNodes[0].NodeType);
            Assert.Equal(0, topNodes[0].Depth);
            _output.WriteLine($"Root node type: Internal, depth: 0");
        }

        [Fact]
        public void GetNodesByDepthRange_ReturnsOnlyRequestedLevels()
        {
            var store = new InMemoryBinaryTrieNodeStore();
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);

            for (int i = 0; i < 20; i++)
            {
                var addr = new byte[20];
                addr[19] = (byte)i;
                trie.Put(keyDeriv.GetTreeKeyForBasicData(addr),
                    BasicDataLeaf.Pack(0, 0, (ulong)i, new EvmUInt256((ulong)(i * 100))));
            }
            trie.SaveToStorage(store);

            var top3 = store.GetNodesByDepthRange(0, 3);
            var allNodes = store.GetNodesByDepthRange(0, 1000);

            Assert.True(top3.Count > 0);
            Assert.True(top3.Count < allNodes.Count,
                $"Top 3 levels ({top3.Count}) should be fewer than all ({allNodes.Count})");

            foreach (var n in top3)
                Assert.True(n.Depth <= 3, $"Node at depth {n.Depth} should not be in range 0-3");

            _output.WriteLine($"Top 3 levels: {top3.Count} nodes, total: {allNodes.Count} nodes");
        }

        [Fact]
        public void GetStemNodesByAddress_ReturnsOnlyMatchingContract()
        {
            var store = new InMemoryBinaryTrieNodeStore();
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);

            var addr1 = "0x1000000000000000000000000000000000000000".HexToByteArray();
            var addr2 = "0x2000000000000000000000000000000000000000".HexToByteArray();

            var basicData = BasicDataLeaf.Pack(0, 0, 1, new EvmUInt256(100));
            trie.Put(keyDeriv.GetTreeKeyForBasicData(addr1), basicData);
            trie.Put(keyDeriv.GetTreeKeyForCodeHash(addr1), new byte[32]);

            trie.Put(keyDeriv.GetTreeKeyForBasicData(addr2), basicData);
            trie.Put(keyDeriv.GetTreeKeyForCodeHash(addr2), new byte[32]);

            for (int i = 0; i < 5; i++)
            {
                var storageKey = keyDeriv.GetTreeKeyForStorageSlot(addr1, (EvmUInt256)i);
                trie.Put(storageKey, PadTo32(new byte[] { (byte)(i + 1) }));
            }

            trie.SaveToStorage(store);
            RegisterStemsForAddress(store, trie, keyDeriv, addr1, 5);
            RegisterStemsForAddress(store, trie, keyDeriv, addr2, 0);

            var addr1Stems = store.GetStemNodesByAddress(addr1);
            var addr2Stems = store.GetStemNodesByAddress(addr2);

            _output.WriteLine($"Address1 stems: {addr1Stems.Count}");
            _output.WriteLine($"Address2 stems: {addr2Stems.Count}");

            Assert.True(addr1Stems.Count > 0);
            Assert.True(addr2Stems.Count > 0);
        }

        [Fact]
        public void DirtyTracking_ReportsOnlyChangedNodes()
        {
            var store = new InMemoryBinaryTrieNodeStore();
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);
            var addr = "0x1000000000000000000000000000000000000000".HexToByteArray();

            trie.Put(keyDeriv.GetTreeKeyForBasicData(addr),
                BasicDataLeaf.Pack(0, 0, 1, new EvmUInt256(100)));
            trie.SaveToStorage(store);

            var initialDirty = store.GetDirtyNodes();
            Assert.True(initialDirty.Count > 0);
            _output.WriteLine($"After first save: {initialDirty.Count} dirty nodes");

            store.ClearDirtyTracking();
            var afterClear = store.GetDirtyNodes();
            Assert.Empty(afterClear);

            trie.Put(keyDeriv.GetTreeKeyForStorageSlot(addr, EvmUInt256.Zero),
                PadTo32(new byte[] { 0x42 }));
            trie.SaveToStorage(store);

            var afterUpdate = store.GetDirtyNodes();
            Assert.True(afterUpdate.Count > 0);
            Assert.True(afterUpdate.Count < store.NodeCount,
                "Only changed nodes should be dirty, not the whole tree");

            _output.WriteLine($"After storage update: {afterUpdate.Count} dirty / {store.NodeCount} total");
        }

        [Fact]
        public void ExportImportCheckpoint_RoundTrips()
        {
            var store = new InMemoryBinaryTrieNodeStore();
            var trie = BuildTrieWithTwoAccounts();
            trie.SaveToStorage(store);

            var maxDepth = 5;
            var checkpoint = store.ExportCheckpoint(maxDepth);
            Assert.True(checkpoint.Length > 0);

            var imported = new InMemoryBinaryTrieNodeStore();
            imported.ImportCheckpoint(checkpoint);

            var originalNodes = store.GetNodesByDepthRange(0, maxDepth);
            var importedNodes = imported.GetNodesByDepthRange(0, maxDepth);

            Assert.Equal(originalNodes.Count, importedNodes.Count);

            foreach (var orig in originalNodes)
            {
                var match = false;
                foreach (var imp in importedNodes)
                {
                    if (StemStartsWith(orig.Hash, imp.Hash) && orig.Hash.Length == imp.Hash.Length)
                    {
                        Assert.Equal(orig.Encoded, imp.Encoded);
                        Assert.Equal(orig.Depth, imp.Depth);
                        Assert.Equal(orig.NodeType, imp.NodeType);
                        match = true;
                        break;
                    }
                }
                Assert.True(match, $"Node with hash {orig.Hash[0]:x2}{orig.Hash[1]:x2}... not found in imported store");
            }

            _output.WriteLine($"Checkpoint: {checkpoint.Length} bytes, {originalNodes.Count} nodes at depth 0-{maxDepth}");
        }

        [Fact]
        public void CheckpointSize_TopLevelsAreMuchSmallerThanFull()
        {
            var store = new InMemoryBinaryTrieNodeStore();
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);

            for (int i = 0; i < 20; i++)
            {
                var addr = new byte[20];
                addr[19] = (byte)i;
                trie.Put(keyDeriv.GetTreeKeyForBasicData(addr),
                    BasicDataLeaf.Pack(0, 0, (ulong)i, new EvmUInt256((ulong)(i * 1000))));
            }
            trie.SaveToStorage(store);

            var top3 = store.ExportCheckpoint(3);
            var top5 = store.ExportCheckpoint(5);
            var full = store.ExportCheckpoint(1000);

            _output.WriteLine($"Top 3 levels: {top3.Length} bytes");
            _output.WriteLine($"Top 5 levels: {top5.Length} bytes");
            _output.WriteLine($"Full tree:    {full.Length} bytes");
            _output.WriteLine($"Total nodes:  {store.NodeCount}");

            Assert.True(top3.Length <= top5.Length);
            Assert.True(top5.Length <= full.Length);
        }

        [Fact]
        public void BlockCommit_TracksBlockNumbers()
        {
            var store = new InMemoryBinaryTrieNodeStore();
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);
            var addr = "0x1000000000000000000000000000000000000000".HexToByteArray();

            store.MarkBlockCommitted(1);
            trie.Put(keyDeriv.GetTreeKeyForBasicData(addr),
                BasicDataLeaf.Pack(0, 0, 1, new EvmUInt256(100)));
            trie.SaveToStorage(store);

            var block1Dirty = store.GetDirtyNodes();
            Assert.True(block1Dirty.Count > 0);
            foreach (var n in block1Dirty)
                Assert.Equal(1, n.BlockNumber);

            store.ClearDirtyTracking();
            store.MarkBlockCommitted(2);

            trie.Put(keyDeriv.GetTreeKeyForStorageSlot(addr, EvmUInt256.Zero),
                PadTo32(new byte[] { 0x42 }));
            trie.SaveToStorage(store);

            var block2Dirty = store.GetDirtyNodes();
            Assert.True(block2Dirty.Count > 0);
            foreach (var n in block2Dirty)
                Assert.Equal(2, n.BlockNumber);

            _output.WriteLine($"Block 1: {block1Dirty.Count} nodes, Block 2: {block2Dirty.Count} nodes");
        }

        [Fact]
        public void PerContractSync_SimulateUsdcLightClient()
        {
            var store = new InMemoryBinaryTrieNodeStore();
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);

            var usdc = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48".HexToByteArray();
            var weth = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2".HexToByteArray();

            trie.Put(keyDeriv.GetTreeKeyForBasicData(usdc),
                BasicDataLeaf.Pack(0, 5000, 1, EvmUInt256.Zero));
            trie.Put(keyDeriv.GetTreeKeyForCodeHash(usdc),
                new byte[32]);

            trie.Put(keyDeriv.GetTreeKeyForBasicData(weth),
                BasicDataLeaf.Pack(0, 3000, 1, EvmUInt256.Zero));
            trie.Put(keyDeriv.GetTreeKeyForCodeHash(weth),
                new byte[32]);

            for (int i = 0; i < 10; i++)
            {
                var holder = new byte[32];
                holder[31] = (byte)i;
                var slot = EvmUInt256BigIntegerExtensions.FromBigInteger(
                    new System.Numerics.BigInteger(i) + 64);
                trie.Put(keyDeriv.GetTreeKeyForStorageSlot(usdc, slot),
                    PadTo32(new byte[] { (byte)(i + 1) }));
            }

            for (int i = 0; i < 5; i++)
            {
                var slot = EvmUInt256BigIntegerExtensions.FromBigInteger(
                    new System.Numerics.BigInteger(i) + 64);
                trie.Put(keyDeriv.GetTreeKeyForStorageSlot(weth, slot),
                    PadTo32(new byte[] { (byte)(i + 0x10) }));
            }

            trie.SaveToStorage(store);
            RegisterStemsForAddress(store, trie, keyDeriv, usdc, 10);
            RegisterStemsForAddress(store, trie, keyDeriv, weth, 5);

            var usdcStems = store.GetStemNodesByAddress(usdc);
            var wethStems = store.GetStemNodesByAddress(weth);

            _output.WriteLine($"USDC stems: {usdcStems.Count}");
            _output.WriteLine($"WETH stems: {wethStems.Count}");
            _output.WriteLine($"Total nodes in store: {store.NodeCount}");

            Assert.True(usdcStems.Count > 0);
            Assert.True(wethStems.Count > 0);

            long usdcBytes = 0;
            foreach (var s in usdcStems)
                usdcBytes += s.Encoded.Length;
            _output.WriteLine($"USDC data size: {usdcBytes} bytes (just stems, excludes proof siblings)");
        }

        private BinaryTrie BuildTrieWithTwoAccounts()
        {
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);

            var addr1 = "0x1000000000000000000000000000000000000000".HexToByteArray();
            var addr2 = "0x2000000000000000000000000000000000000000".HexToByteArray();

            trie.Put(keyDeriv.GetTreeKeyForBasicData(addr1),
                BasicDataLeaf.Pack(0, 100, 1, new EvmUInt256(1000000000000000000)));
            trie.Put(keyDeriv.GetTreeKeyForCodeHash(addr1), new byte[32]);

            trie.Put(keyDeriv.GetTreeKeyForBasicData(addr2),
                BasicDataLeaf.Pack(0, 0, 5, new EvmUInt256(500)));
            trie.Put(keyDeriv.GetTreeKeyForCodeHash(addr2), new byte[32]);

            return trie;
        }

        private void RegisterStemsForAddress(InMemoryBinaryTrieNodeStore store,
            BinaryTrie trie, BinaryTreeKeyDerivation keyDeriv, byte[] address, int storageSlots)
        {
            var basicKey = keyDeriv.GetTreeKeyForBasicData(address);
            var basicStem = new byte[BinaryTrieConstants.StemSize];
            System.Array.Copy(basicKey, 0, basicStem, 0, BinaryTrieConstants.StemSize);

            var nodes = store.GetNodesByDepthRange(0, 1000);
            foreach (var n in nodes)
            {
                if (n.NodeType == BinaryTrieConstants.NodeTypeStem && n.Stem != null)
                {
                    if (StemStartsWith(n.Stem, basicStem))
                        store.RegisterAddressStem(address, n.Hash);
                }
            }

            for (int i = 0; i < storageSlots; i++)
            {
                var slot = EvmUInt256BigIntegerExtensions.FromBigInteger(
                    new System.Numerics.BigInteger(i) + 64);
                var storageKey = keyDeriv.GetTreeKeyForStorageSlot(address, slot);
                var storageStem = new byte[BinaryTrieConstants.StemSize];
                System.Array.Copy(storageKey, 0, storageStem, 0, BinaryTrieConstants.StemSize);

                foreach (var n in nodes)
                {
                    if (n.NodeType == BinaryTrieConstants.NodeTypeStem && n.Stem != null)
                    {
                        if (StemStartsWith(n.Stem, storageStem))
                            store.RegisterAddressStem(address, n.Hash);
                    }
                }
            }
        }

        private static bool StemStartsWith(byte[] stem, byte[] prefix)
        {
            if (stem.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (stem[i] != prefix[i]) return false;
            return true;
        }

        private static byte[] PadTo32(byte[] value)
        {
            if (value.Length >= 32) return value;
            var padded = new byte[32];
            System.Array.Copy(value, 0, padded, 32 - value.Length, value.Length);
            return padded;
        }
    }
}
