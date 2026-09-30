using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class WindowDirtyLayersTryGetLatestTests
    {
        private static readonly byte[] PathA = { 0xA0 };
        private static readonly byte[] PathB = { 0xB0 };
        private static readonly byte[] OwnerX = { 0x11, 0x22, 0x33 };

        private static LeafNode MakeLeaf(byte[] owner, byte[] path, byte tag)
            => new LeafNode { Owner = owner, Path = path, Nibbles = new byte[] { 1, 2, 3, 4 }, Value = new byte[] { tag, tag, tag } };

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
        public void EmptyLayers_ReturnsFalse()
        {
            var layers = new WindowDirtyLayers();
            Assert.False(layers.TryGetLatest(true, null, PathA, out var value, out var isAbsent));
            Assert.Null(value);
            Assert.False(isAbsent);
        }

        [Fact]
        public void UntouchedKey_ReturnsFalse_EvenWhenOtherKeysAreLayered()
        {
            var layers = new WindowDirtyLayers();
            layers.PushLayer(1, SetWithNode(MakeLeaf(null, PathA, 0x01)));

            Assert.False(layers.TryGetLatest(true, null, PathB, out var value, out var isAbsent));
            Assert.Null(value);
            Assert.False(isAbsent);
        }

        [Fact]
        public void SingleLayer_ReturnsThatLayersValue_NoForBlockBoundNeeded()
        {
            var layers = new WindowDirtyLayers();
            var v1 = MakeLeaf(null, PathA, 0x01).GetEncodedData();
            layers.PushLayer(1, SetWithNode(MakeLeaf(null, PathA, 0x01)));

            Assert.True(layers.TryGetLatest(true, null, PathA, out var value, out var isAbsent));
            Assert.False(isAbsent);
            Assert.Equal(v1, value);

            Assert.False(layers.TryGetPreImage(true, null, PathA, forBlock: 1, out _, out _));
        }

        [Fact]
        public void OverwriteChain_ResolvesToTheHighestLayer_RegardlessOfBlockNumber()
        {
            var layers = new WindowDirtyLayers();
            var v3 = MakeLeaf(null, PathA, 0x03).GetEncodedData();

            layers.PushLayer(1, SetWithNode(MakeLeaf(null, PathA, 0x01)));
            layers.PushLayer(2, SetWithNode(MakeLeaf(null, PathA, 0x02)));
            layers.PushLayer(3, SetWithNode(MakeLeaf(null, PathA, 0x03)));

            Assert.True(layers.TryGetLatest(true, null, PathA, out var value, out var isAbsent));
            Assert.False(isAbsent);
            Assert.Equal(v3, value);
        }

        [Fact]
        public void DeletedKey_ReturnsAbsent_NotAStaleValue()
        {
            var layers = new WindowDirtyLayers();
            layers.PushLayer(1, SetWithNode(MakeLeaf(null, PathA, 0x01)));
            layers.PushLayer(2, SetWithDelete(null, PathA, prevBlob: new byte[] { 0xDE, 0xAD }));

            Assert.True(layers.TryGetLatest(true, null, PathA, out var value, out var isAbsent));
            Assert.True(isAbsent);
            Assert.Null(value);
        }

        [Fact]
        public void OwnerWipe_PreWipeTouch_ResolvesAbsent()
        {
            var layers = new WindowDirtyLayers();
            layers.PushLayer(1, SetWithNode(MakeLeaf(OwnerX, PathA, 0x01)));
            layers.PushOwnerWipe(2, OwnerX);

            Assert.True(layers.TryGetLatest(false, OwnerX, PathA, out var value, out var isAbsent));
            Assert.True(isAbsent);
            Assert.Null(value);
        }

        [Fact]
        public void OwnerWipe_NoTouchAtAll_StillResolvesAbsent()
        {
            var layers = new WindowDirtyLayers();
            layers.PushOwnerWipe(1, OwnerX);

            Assert.True(layers.TryGetLatest(false, OwnerX, PathA, out var value, out var isAbsent));
            Assert.True(isAbsent);
            Assert.Null(value);
        }

        [Fact]
        public void OwnerWipe_PostWipeRepopulation_WinsNormally()
        {
            var layers = new WindowDirtyLayers();
            var v2 = MakeLeaf(OwnerX, PathA, 0x02).GetEncodedData();

            layers.PushLayer(1, SetWithNode(MakeLeaf(OwnerX, PathA, 0x01)));
            layers.PushOwnerWipe(2, OwnerX);
            layers.PushLayer(3, SetWithNode(MakeLeaf(OwnerX, PathA, 0x02)));

            Assert.True(layers.TryGetLatest(false, OwnerX, PathA, out var value, out var isAbsent));
            Assert.False(isAbsent);
            Assert.Equal(v2, value);
        }

        [Fact]
        public void AccountKey_IgnoresWipeBarrier_WipesAreStorageOnlyByConstruction()
        {
            var layers = new WindowDirtyLayers();
            var v1 = MakeLeaf(null, PathA, 0x01).GetEncodedData();
            layers.PushLayer(1, SetWithNode(MakeLeaf(null, PathA, 0x01)));

            Assert.True(layers.TryGetLatest(true, null, PathA, out var value, out var isAbsent));
            Assert.False(isAbsent);
            Assert.Equal(v1, value);
        }

        [Fact]
        public void DropThrough_RemovesLayers_TryGetLatestNoLongerSeesThem()
        {
            var layers = new WindowDirtyLayers();
            layers.PushLayer(1, SetWithNode(MakeLeaf(null, PathA, 0x01)));
            Assert.True(layers.TryGetLatest(true, null, PathA, out _, out _));

            layers.DropThrough(1);
            Assert.False(layers.TryGetLatest(true, null, PathA, out var value, out var isAbsent));
            Assert.Null(value);
            Assert.False(isAbsent);
        }

        [Fact]
        public void LookupCount_IncrementsOnEveryCall_MirroringTryGetPreImage()
        {
            var layers = new WindowDirtyLayers();
            Assert.Equal(0, layers.LookupCount);

            layers.TryGetLatest(true, null, PathA, out _, out _);
            Assert.Equal(1, layers.LookupCount);

            layers.TryGetLatest(true, null, PathB, out _, out _);
            Assert.Equal(2, layers.LookupCount);
        }
    }
}
