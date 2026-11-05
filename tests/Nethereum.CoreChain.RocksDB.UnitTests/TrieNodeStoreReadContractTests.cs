using System;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class TrieNodeStoreReadContractTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"readcontract_{Guid.NewGuid():N}");
        private readonly RocksDbManager _manager;
        private readonly NodeCommitBlockContext _context = new NodeCommitBlockContext();

        public TrieNodeStoreReadContractTests()
        {
            Directory.CreateDirectory(_dir);
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
        }

        public void Dispose()
        {
            _manager.Dispose();
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        public static TheoryData<string> Flavours => new TheoryData<string>
        {
            "rocksdb", "capturing-buffered", "capturing-passthrough", "proof"
        };

        private ITrieNodeStore Build(string flavour)
        {
            var inner = new RocksDbPathTrieNodeStore(_manager);
            if (flavour == "rocksdb") return inner;
            if (flavour == "proof") return new PathKeyedProofNodeStore(inner);
            var journal = new RocksDbNodeReverseDiffStore(_manager, buildKeyMajorIndex: false);
            var journaling = new JournalingPathNodeStore(inner, journal, _context, new FixedWindowFloorPolicy(0), 1000);
            return new CapturingJournalingPathNodeStore(journaling, _context);
        }

        private void Commit(string flavour, ITrieNodeStore store, TrieNodeSet set)
        {
            if (flavour == "capturing-buffered")
            {
                _context.Arm(7);
                store.Commit(set);
                return;
            }
            if (flavour == "proof")
            {
                new RocksDbPathTrieNodeStore(_manager).Commit(set);
                return;
            }
            store.Commit(set);
        }

        [Theory]
        [MemberData(nameof(Flavours))]
        public void Given_APathStoreHoldingATamperedBlob_When_ReadByReference_Then_AnInvalidOperationExceptionNamesTheHashMismatch(string flavour)
        {
            var node = PathNodeFixtures.ReferenceableNode(null, new byte[] { 4, 5 }, seed: 11);
            var store = Build(flavour);
            Commit(flavour, store, PathNodeFixtures.SetOf(node));
            var tamperedReference = PathNodeFixtures.ReferenceAt(new byte[32], null, new byte[] { 4, 5 });

            var refusal = Assert.Throws<InvalidOperationException>(() => store.Get(tamperedReference));

            Assert.Contains("hash mismatch", refusal.Message);
            Assert.Equal(node.GetEncodedData(), store.Get(PathNodeFixtures.ReferenceTo(node)));
        }

        [Theory]
        [MemberData(nameof(Flavours))]
        public void Given_AMissingNode_When_ReadByReference_Then_NullIsReturnedWithoutThrowing(string flavour)
        {
            var store = Build(flavour);

            var missing = PathNodeFixtures.ReferenceAt(new byte[32], null, new byte[] { 7 });

            Assert.Null(store.Get(missing));
            Assert.False(store.Contains(missing));
            Assert.Null(store.Get(null));
        }

        [Theory]
        [MemberData(nameof(Flavours))]
        public void Given_AStoreWhoseRootBlobDoesNotHashToTheRequestedRoot_When_ContainsKeyIsAsked_Then_ItIsFalseForEveryStoreFlavour(string flavour)
        {
            var root = PathNodeFixtures.ReferenceableNode(Array.Empty<byte>(), Array.Empty<byte>(), seed: 12);
            var store = Build(flavour);
            Commit(flavour, store, PathNodeFixtures.SetOf(root));

            Assert.True(store.ContainsKey(root.GetHash()));
            Assert.False(store.ContainsKey(new byte[32]));
            var other = root.GetHash();
            other[0] ^= 0xFF;
            Assert.False(store.ContainsKey(other));
        }

        [Theory]
        [MemberData(nameof(Flavours))]
        public void Given_ARootThatIsNot32Bytes_When_ContainsKeyIsAsked_Then_ItIsFalse(string flavour)
        {
            var root = PathNodeFixtures.ReferenceableNode(Array.Empty<byte>(), Array.Empty<byte>(), seed: 13);
            var store = Build(flavour);
            Commit(flavour, store, PathNodeFixtures.SetOf(root));

            Assert.False(store.ContainsKey(null));
            Assert.False(store.ContainsKey(Array.Empty<byte>()));
            Assert.False(store.ContainsKey(new byte[31]));
            Assert.False(store.ContainsKey(new byte[33]));
        }

        [Fact]
        public void Given_ABufferedRootThatMatches_When_ContainsKeyIsAsked_Then_CapturingReturnsTrueWithoutReadingTheInnerStore()
        {
            var root = PathNodeFixtures.ReferenceableNode(Array.Empty<byte>(), Array.Empty<byte>(), seed: 14);
            var capturing = Build("capturing-buffered");
            var inner = new RocksDbPathTrieNodeStore(_manager);
            Assert.False(inner.ContainsKey(root.GetHash()));

            Commit("capturing-buffered", capturing, PathNodeFixtures.SetOf(root));

            Assert.False(inner.ContainsKey(root.GetHash()));
            Assert.True(capturing.ContainsKey(root.GetHash()));
        }

        [Fact]
        public void Given_AHealSinkWhoseRootBlobDoesNotHashToTheRequestedRoot_When_HasRootIsAsked_Then_ItIsFalse()
        {
            var root = PathNodeFixtures.ReferenceableNode(Array.Empty<byte>(), Array.Empty<byte>(), seed: 15);
            var inner = new RocksDbPathTrieNodeStore(_manager);
            inner.Commit(PathNodeFixtures.SetOf(root));
            var sink = new PathHealNodeSink(inner, _manager);

            Assert.True(sink.HasRoot(root.GetHash()));
            Assert.False(sink.HasRoot(new byte[32]));
            Assert.False(sink.HasRoot(null));
            Assert.False(sink.HasRoot(new byte[31]));
        }
    }
}
