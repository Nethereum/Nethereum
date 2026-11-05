using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PathHealNodeSinkCaptureTests : IDisposable
    {
        private static readonly byte[] JournalRootKey = { 0x01 };
        private readonly List<string> _dirs = new();
        private readonly List<IDisposable> _owned = new();

        public void Dispose()
        {
            foreach (var owned in _owned) owned.Dispose();
            foreach (var dir in _dirs)
                try { Directory.Delete(dir, recursive: true); } catch { }
        }

        private RocksDbChainStoreBundle OpenPathKeyedBundle()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"healcapture_{Guid.NewGuid():N}");
            _dirs.Add(dir);
            var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { PathKeyedState = true });
            _owned.Add(bundle);
            return bundle;
        }

        private static void StartJournal(RocksDbChainStoreBundle bundle)
            => bundle.Rocks.Put(RocksDbManager.CF_FLAT_GENERATION, JournalRootKey, Sha3Keccack.Current.CalculateHash(new byte[] { 0x42 }));

        private static byte[] Node(byte fill) => Enumerable.Repeat(fill, 40).Prepend((byte)0xE8).ToArray();

        private static byte[] HashOf(byte[] blob) => Sha3Keccack.Current.CalculateHash(blob);

        private static byte[] AccountCaptureKey(byte[] path) => new byte[] { 0x03, 0x00 }.Concat(path).ToArray();

        private static byte[] Owner => Sha3Keccack.Current.CalculateHash(new byte[] { 0x07 });

        private static List<byte[]> JournalKeysAfterTheRoot(RocksDbChainStoreBundle bundle)
        {
            var keys = new List<byte[]>();
            using var it = bundle.Rocks.CreateIterator(RocksDbManager.CF_FLAT_GENERATION);
            for (it.Seek(new byte[] { 0x02 }); it.Valid(); it.Next()) keys.Add(it.Key());
            return keys;
        }

        private static IHealNodeSink Sink(RocksDbChainStoreBundle bundle) => ((IHealNodeSinkProvider)bundle).CreateHealSink();

        [Fact]
        public void Given_AJournal_When_ANodeIsOverwrittenTwiceAcrossFlushes_Then_OnlyTheFirstPreImageIsCaptured()
        {
            var bundle = OpenPathKeyedBundle();
            var path = new byte[] { 0x3 };
            bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path, Node(0xA1));
            StartJournal(bundle);
            var sink = Sink(bundle);

            sink.PutNode(false, null, path, HashOf(Node(0xB2)), Node(0xB2));
            sink.Flush();
            sink.PutNode(false, null, path, HashOf(Node(0xC3)), Node(0xC3));
            sink.Flush();

            Assert.Equal(Node(0xC3), bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path));
            Assert.Equal(Node(0xA1), bundle.Rocks.Get(RocksDbManager.CF_FLAT_GENERATION, AccountCaptureKey(path)));
        }

        [Fact]
        public void Given_AJournal_When_ANodeIsWrittenAtAnEmptyPath_Then_ItsPreImageIsCapturedAsAbsent()
        {
            var bundle = OpenPathKeyedBundle();
            var path = new byte[] { 0x4, 0x1 };
            StartJournal(bundle);
            var sink = Sink(bundle);

            sink.PutNode(false, null, path, HashOf(Node(0xB2)), Node(0xB2));
            sink.Flush();

            Assert.Equal(new byte[] { 0x00 }, bundle.Rocks.Get(RocksDbManager.CF_FLAT_GENERATION, AccountCaptureKey(path)));
        }

        [Fact]
        public void Given_AJournal_When_ANodeIsDeleted_Then_ItsPreImageIsCapturedInTheSameBatch()
        {
            var bundle = OpenPathKeyedBundle();
            var path = new byte[] { 0x5 };
            var storagePath = new byte[] { 0x9, 0x2 };
            bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path, Node(0xA1));
            bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, Owner.Concat(storagePath).ToArray(), Node(0xD4));
            StartJournal(bundle);
            var sink = Sink(bundle);

            sink.DeleteNode(false, null, path);
            sink.DeleteNode(true, Owner, storagePath);
            sink.Flush();

            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path));
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, Owner.Concat(storagePath).ToArray()));
            Assert.Equal(Node(0xA1), bundle.Rocks.Get(RocksDbManager.CF_FLAT_GENERATION, AccountCaptureKey(path)));
            Assert.Equal(Node(0xD4), bundle.Rocks.Get(
                RocksDbManager.CF_FLAT_GENERATION, new byte[] { 0x03, 0x01 }.Concat(Owner).Concat(storagePath).ToArray()));
        }

        [Fact]
        public void Given_AJournal_When_AnOwnerIsWiped_Then_TheWipeIsCaptured()
        {
            var bundle = OpenPathKeyedBundle();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, Owner, Node(0xD4));
            StartJournal(bundle);
            var sink = Sink(bundle);

            sink.ForceWipeStorage(Owner);
            sink.Flush();

            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, Owner));
            Assert.NotNull(bundle.Rocks.Get(RocksDbManager.CF_FLAT_GENERATION, new byte[] { 0x04 }.Concat(Owner).ToArray()));
        }

        [Fact]
        public void Given_NoJournal_When_TheSinkFlushes_Then_NothingIsCaptured()
        {
            var bundle = OpenPathKeyedBundle();
            var path = new byte[] { 0x3 };
            bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path, Node(0xA1));
            var sink = Sink(bundle);

            sink.PutNode(false, null, path, HashOf(Node(0xB2)), Node(0xB2));
            sink.ForceWipeStorage(Owner);
            sink.Flush();

            Assert.Equal(Node(0xB2), bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path));
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_FLAT_GENERATION, JournalRootKey));
            Assert.Empty(JournalKeysAfterTheRoot(bundle));
        }

        private static readonly Account Funded = new Account
        {
            Nonce = 3, Balance = 99, StateRoot = DefaultValues.EMPTY_TRIE_HASH, CodeHash = DefaultValues.EMPTY_DATA_HASH,
        };

        private static async Task WriteFlatThroughTheHealSinkAsync(IHealNodeSink sink, byte[] account, byte[] ghost)
        {
            var writer = Assert.IsAssignableFrom<ISnapFlatStateWriter>(sink);
            await writer.SaveAccountByHashAsync(account, Funded);
            await writer.SaveStorageByHashAsync(account, HashOf(new byte[] { 0x01 }), new byte[] { 0x2A });
            await writer.DeleteAccountByHashAsync(ghost);
        }

        [Fact]
        public async Task Given_AJournal_When_TheHealWritesFlat_Then_NoFlatRowChanges()
        {
            var bundle = OpenPathKeyedBundle();
            var account = HashOf(new byte[] { 0x10 });
            var ghost = HashOf(new byte[] { 0x11 });
            var flat = new RocksDbStateStore(bundle.Rocks);
            await flat.SaveAccountByHashAsync(ghost, Funded);
            StartJournal(bundle);

            await WriteFlatThroughTheHealSinkAsync(Sink(bundle), account, ghost);

            Assert.Null(await flat.GetAccountByHashAsync(account));
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, account.Concat(HashOf(new byte[] { 0x01 })).ToArray()));
            Assert.NotNull(await flat.GetAccountByHashAsync(ghost));
        }

        private static List<(byte[] Key, byte[] Value)> FlatRows(RocksDbChainStoreBundle bundle)
        {
            var rows = new List<(byte[], byte[])>();
            foreach (var cf in new[] { RocksDbManager.CF_STATE_ACCOUNTS, RocksDbManager.CF_STATE_STORAGE })
            {
                using var it = bundle.Rocks.CreateIterator(cf);
                for (it.SeekToFirst(); it.Valid(); it.Next()) rows.Add((it.Key(), it.Value()));
            }
            return rows;
        }

        [Fact]
        public async Task Given_NoJournal_When_TheHealWritesFlat_Then_ItReachesTheBundleState()
        {
            var throughSink = OpenPathKeyedBundle();
            var throughState = OpenPathKeyedBundle();
            var account = HashOf(new byte[] { 0x10 });
            var ghost = HashOf(new byte[] { 0x11 });
            foreach (var bundle in new[] { throughSink, throughState })
                await new RocksDbStateStore(bundle.Rocks).SaveAccountByHashAsync(ghost, Funded);

            await WriteFlatThroughTheHealSinkAsync(Sink(throughSink), account, ghost);
            var state = Assert.IsAssignableFrom<ISnapFlatStateWriter>(throughState.State);
            await state.SaveAccountByHashAsync(account, Funded);
            await state.SaveStorageByHashAsync(account, HashOf(new byte[] { 0x01 }), new byte[] { 0x2A });
            await state.DeleteAccountByHashAsync(ghost);

            var expected = FlatRows(throughState);
            var actual = FlatRows(throughSink);
            Assert.Contains(expected, r => r.Key.SequenceEqual(account));
            Assert.Equal(expected.Select(r => r.Key), actual.Select(r => r.Key), ByteArrayComparer.Current);
            Assert.Equal(expected.Select(r => r.Value), actual.Select(r => r.Value), ByteArrayComparer.Current);
            Assert.NotNull(await new RocksDbStateStore(throughSink.Rocks).GetAccountByHashAsync(account));
        }

        [Fact]
        public async Task Given_ASinkBuiltBeforeTheJournalStarts_When_ItFlushesThenWritesFlatRows_Then_TheFlatRowsAreDropped()
        {
            var bundle = OpenPathKeyedBundle();
            var account = HashOf(new byte[] { 0x10 });
            var sink = Sink(bundle);
            var path = new byte[] { 0x3 };
            StartJournal(bundle);

            sink.PutNode(false, null, path, HashOf(Node(0xB2)), Node(0xB2));
            sink.Flush();
            var writer = Assert.IsAssignableFrom<ISnapFlatStateWriter>(sink);
            await writer.SaveAccountByHashAsync(account, Funded);
            await writer.SaveStorageByHashAsync(account, HashOf(new byte[] { 0x01 }), new byte[] { 0x2A });

            Assert.Equal(Node(0xB2), bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path));
            Assert.Null(await new RocksDbStateStore(bundle.Rocks).GetAccountByHashAsync(account));
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, account.Concat(HashOf(new byte[] { 0x01 })).ToArray()));
        }
    }
}
