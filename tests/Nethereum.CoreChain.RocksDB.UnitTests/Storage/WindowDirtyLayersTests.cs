using System;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class WindowDirtyLayersTests
    {
        private static readonly byte[] PathA = { 0xA0 };
        private static readonly byte[] PathB = { 0xB0 };
        private static readonly byte[] PathC = { 0xC0 };
        private static readonly byte[] PathD = { 0xD0 };
        private static readonly byte[] PathE = { 0xE0 };

        private static LeafNode MakeLeaf(byte[] owner, byte[] path, byte tag)
        {
            return new LeafNode
            {
                Owner = owner,
                Path = path,
                Nibbles = new byte[] { 1, 2, 3, 4 },
                Value = new byte[] { tag, tag, tag },
            };
        }

        private static TrieNodeSet SetWithNode(Node node)
        {
            var set = new TrieNodeSet();
            set.Add(node);
            return set;
        }

        private static TrieNodeSet SetWithDelete(byte[] owner, byte[] path, byte[] prevBlob = null)
        {
            var set = new TrieNodeSet();
            set.AddDelete(owner, path, prevBlob);
            return set;
        }

        [Fact]
        public void UntouchedKey_ReturnsFalse_TouchedKeyResolvesToHighestLowerLayer()
        {
            var layers = new WindowDirtyLayers();
            var v1 = MakeLeaf(null, PathA, 0x01).GetEncodedData();
            var v2 = MakeLeaf(null, PathA, 0x02).GetEncodedData();

            layers.PushLayer(1, SetWithNode(MakeLeaf(null, PathA, 0x01)));
            layers.PushLayer(2, SetWithNode(MakeLeaf(null, PathA, 0x02)));

            Assert.True(layers.TryGetPreImage(true, null, PathA, forBlock: 3, out var preImageA, out var absentA));
            Assert.False(absentA);
            Assert.Equal(v2, preImageA);

            Assert.False(layers.TryGetPreImage(true, null, PathB, forBlock: 3, out var preImageB, out var absentB));
            Assert.Null(preImageB);
            Assert.False(absentB);
        }

        [Fact]
        public void WrittenOnlyAtOrAboveForBlock_ReturnsFalse_NotTheAtBlockValue()
        {
            var layers = new WindowDirtyLayers();

            layers.PushLayer(3, SetWithNode(MakeLeaf(null, PathC, 0x03)));

            Assert.False(layers.TryGetPreImage(true, null, PathC, forBlock: 3, out var preImage, out var isAbsent));
            Assert.Null(preImage);
            Assert.False(isAbsent);

            Assert.False(layers.TryGetPreImage(true, null, PathC, forBlock: 2, out _, out _));
        }

        [Fact]
        public void DeleteThenRecreate_PreImageIsAbsent_NotADiskValue()
        {
            var layers = new WindowDirtyLayers();

            layers.PushLayer(1, SetWithDelete(null, PathD, prevBlob: new byte[] { 0xDE, 0xAD }));
            layers.PushLayer(2, SetWithNode(MakeLeaf(null, PathD, 0x22)));

            Assert.True(layers.TryGetPreImage(true, null, PathD, forBlock: 2, out var preImage, out var isAbsent));
            Assert.True(isAbsent);
            Assert.Null(preImage);
        }

        [Fact]
        public void OverwriteChain_EachForBlockResolvesToHighestLowerVersion()
        {
            var layers = new WindowDirtyLayers();
            var v1 = MakeLeaf(null, PathE, 0x01).GetEncodedData();
            var v2 = MakeLeaf(null, PathE, 0x02).GetEncodedData();

            layers.PushLayer(1, SetWithNode(MakeLeaf(null, PathE, 0x01)));
            layers.PushLayer(2, SetWithNode(MakeLeaf(null, PathE, 0x02)));

            Assert.True(layers.TryGetPreImage(true, null, PathE, forBlock: 2, out var preImageAt2, out var absentAt2));
            Assert.False(absentAt2);
            Assert.Equal(v1, preImageAt2);

            Assert.True(layers.TryGetPreImage(true, null, PathE, forBlock: 3, out var preImageAt3, out var absentAt3));
            Assert.False(absentAt3);
            Assert.Equal(v2, preImageAt3);
        }

        [Fact]
        public void DropAbove_DropThrough_RemoveLayersAndTheirContributions()
        {
            var layers = new WindowDirtyLayers();
            var v1 = MakeLeaf(null, PathA, 0x01).GetEncodedData();
            var v2 = MakeLeaf(null, PathA, 0x02).GetEncodedData();
            var v3 = MakeLeaf(null, PathA, 0x03).GetEncodedData();

            layers.PushLayer(1, SetWithNode(MakeLeaf(null, PathA, 0x01)));
            layers.PushLayer(2, SetWithNode(MakeLeaf(null, PathA, 0x02)));
            layers.PushLayer(3, SetWithNode(MakeLeaf(null, PathA, 0x03)));
            Assert.Equal(3, layers.LayerCount);

            layers.DropAbove(1);
            Assert.Equal(1, layers.LayerCount);
            Assert.True(layers.TryGetPreImage(true, null, PathA, forBlock: 4, out var afterDropAbove, out _));
            Assert.Equal(v1, afterDropAbove);

            var v2b = MakeLeaf(null, PathA, 0x22).GetEncodedData();
            var v3b = MakeLeaf(null, PathA, 0x33).GetEncodedData();
            layers.PushLayer(2, SetWithNode(MakeLeaf(null, PathA, 0x22)));
            layers.PushLayer(3, SetWithNode(MakeLeaf(null, PathA, 0x33)));
            Assert.Equal(3, layers.LayerCount);

            layers.DropThrough(2);
            Assert.Equal(1, layers.LayerCount);

            Assert.False(layers.TryGetPreImage(true, null, PathA, forBlock: 3, out _, out _));

            Assert.True(layers.TryGetPreImage(true, null, PathA, forBlock: 4, out var afterDropThrough, out _));
            Assert.Equal(v3b, afterDropThrough);
        }

        [Fact]
        public void ApproxBytes_MonotonicIncreaseOnPush_DecreaseOnDrop_RoughlySumOfKeyPlusValueBytes()
        {
            var layers = new WindowDirtyLayers();
            Assert.Equal(0, layers.ApproxBytes);

            var node1 = MakeLeaf(null, PathA, 0x01);
            var encoded1 = node1.GetEncodedData();
            var key1Len = WindowDirtyLayers.Key(true, null, PathA).Length;

            layers.PushLayer(1, SetWithNode(node1));
            var afterFirstPush = layers.ApproxBytes;
            Assert.Equal(key1Len + encoded1.Length, afterFirstPush);
            Assert.True(afterFirstPush > 0);

            var node2 = MakeLeaf(null, PathB, 0x02);
            var encoded2 = node2.GetEncodedData();
            var key2Len = WindowDirtyLayers.Key(true, null, PathB).Length;

            layers.PushLayer(2, SetWithNode(node2));
            var afterSecondPush = layers.ApproxBytes;
            Assert.True(afterSecondPush > afterFirstPush);
            Assert.Equal(key1Len + encoded1.Length + key2Len + encoded2.Length, afterSecondPush);

            layers.DropAbove(1);
            Assert.Equal(afterFirstPush, layers.ApproxBytes);

            layers.DropThrough(1);
            Assert.Equal(0, layers.ApproxBytes);
        }

        [Fact]
        public void Constructor_RequiresNoDependencies_PurelyInMemory()
        {
            var layers = new WindowDirtyLayers();
            Assert.Equal(0, layers.LayerCount);
            Assert.Equal(0, layers.ApproxBytes);
        }

        private static readonly byte[] OwnerX = { 0x11, 0x22, 0x33 };
        private static readonly byte[] OwnerY = { 0x44, 0x55, 0x66 };

        private static LeafNode MakeStorageLeaf(byte[] owner, byte[] path, byte tag)
            => new LeafNode { Owner = owner, Path = path, Nibbles = new byte[] { 5, 6, 7, 8 }, Value = new byte[] { tag, tag, tag } };

        [Fact]
        public void EnumerateOwner_ReturnsHighestLowerLayerPerPath_TombstoneMarkedNotValued()
        {
            var layers = new WindowDirtyLayers();
            var v1 = MakeStorageLeaf(OwnerX, PathA, 0x01).GetEncodedData();
            var v2 = MakeStorageLeaf(OwnerX, PathA, 0x02).GetEncodedData();
            var vB = MakeStorageLeaf(OwnerX, PathB, 0x0B).GetEncodedData();

            var set1 = new TrieNodeSet();
            set1.Add(MakeStorageLeaf(OwnerX, PathA, 0x01));
            set1.Add(MakeStorageLeaf(OwnerX, PathB, 0x0B));
            layers.PushLayer(1, set1);

            var set2 = new TrieNodeSet();
            set2.Add(MakeStorageLeaf(OwnerX, PathA, 0x02));
            set2.AddDelete(OwnerX, PathC, prevBlob: new byte[] { 0xDE, 0xAD });
            layers.PushLayer(2, set2);

            var touches = layers.EnumerateOwner(OwnerX, belowBlock: 3);

            Assert.Equal(3, touches.Count);
            Assert.False(touches[PathA].IsTombstone);
            Assert.Equal(v2, touches[PathA].Value);
            Assert.False(touches[PathB].IsTombstone);
            Assert.Equal(vB, touches[PathB].Value);
            Assert.True(touches[PathC].IsTombstone);
            Assert.Null(touches[PathC].Value);
        }

        [Fact]
        public void EnumerateOwner_ExcludesOtherOwners_AndLayersAtOrAboveBelowBlock()
        {
            var layers = new WindowDirtyLayers();

            var combined = new TrieNodeSet();
            combined.Add(MakeStorageLeaf(OwnerX, PathA, 0x01));
            combined.Add(MakeStorageLeaf(OwnerY, PathA, 0x99));
            layers.PushLayer(1, combined);

            var setAt5 = new TrieNodeSet();
            setAt5.Add(MakeStorageLeaf(OwnerX, PathB, 0x05));
            layers.PushLayer(5, setAt5);

            var touches = layers.EnumerateOwner(OwnerX, belowBlock: 5);

            Assert.Single(touches);
            Assert.True(touches.ContainsKey(PathA));
            Assert.False(touches.ContainsKey(PathB));
        }

        [Fact]
        public void EnumerateOwner_NoQualifyingLayer_ReturnsEmpty_TheK1Shape()
        {
            var layers = new WindowDirtyLayers();

            var set = new TrieNodeSet();
            set.Add(MakeStorageLeaf(OwnerX, PathA, 0x01));
            layers.PushLayer(7, set);

            var touches = layers.EnumerateOwner(OwnerX, belowBlock: 7);
            Assert.Empty(touches);

            var freshLayers = new WindowDirtyLayers();
            Assert.Empty(freshLayers.EnumerateOwner(OwnerX, belowBlock: 100));
        }

        [Fact]
        public void EnumerateOwner_NullOrEmptyOwner_ReturnsEmpty()
        {
            var layers = new WindowDirtyLayers();
            var set = new TrieNodeSet();
            set.Add(MakeStorageLeaf(OwnerX, PathA, 0x01));
            layers.PushLayer(1, set);

            Assert.Empty(layers.EnumerateOwner(null, belowBlock: 5));
            Assert.Empty(layers.EnumerateOwner(Array.Empty<byte>(), belowBlock: 5));
        }
    }
}
