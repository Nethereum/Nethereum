using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PathKeyedSelfDestructJournalTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private readonly RocksDbChainStoreBundle _bundle;

        public PathKeyedSelfDestructJournalTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-sdj-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = _dir,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 64,
                TrieNodeHistoryIndex = true,
            });
            _bundle = RocksDbChainStoreBundle.FromManager(_mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
        }

        [Fact]
        public async Task SelfDestruct_JournaledWipe_LatestGone_ButStorageSubtreeRewindable()
        {
            var calc = new IncrementalStateRootCalculator(
                _bundle.State, _bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(_bundle.StateTrieNodes, _bundle.TrieNodes));

            const string CA = "0x00000000000000000000000000000000000000ca";
            var owner = Owner(CA);

            _bundle.NodeCommitBlockSource.Arm(1);
            await _bundle.State.SaveAccountAsync(CA, new Account { Balance = 0, Nonce = 1 });
            for (int i = 0; i < 24; i++)
                await _bundle.State.SaveStorageAsync(CA, i, ValueBytes(i + 1));
            await calc.ComputeStateRootAsync();
            _bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)_bundle).FlushBlockAsync(null, 1, new byte[32]);
            await ((IAtomicBlockFlush)_bundle).DrainAsync();

            var preStorageRoot = (await _bundle.State.GetAccountAsync(CA)).StateRoot;
            Assert.False(ByteUtil.AreEqual(preStorageRoot, DefaultValues.EMPTY_TRIE_HASH));
            Assert.True(CountOwnerKeys(owner) > 0);

            _bundle.NodeCommitBlockSource.Arm(2);
            await _bundle.State.ClearStorageAsync(CA);
            await _bundle.State.DeleteAccountAsync(CA);
            await calc.ComputeStateRootAsync();
            _bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)_bundle).FlushBlockAsync(null, 2, new byte[32]);
            await ((IAtomicBlockFlush)_bundle).DrainAsync();

            Assert.Equal(0, CountOwnerKeys(owner));

            var restored = _bundle.NodeServing.Journal.FindBlobAsOf(owner, Array.Empty<byte>(), 1);
            Assert.NotNull(restored);
            Assert.Equal(preStorageRoot.ToHex(), new Sha3Keccack().CalculateHash(restored).ToHex());
        }

        private static byte[] Owner(string address)
            => new Sha3Keccack().CalculateHash(AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray());

        private int CountOwnerKeys(byte[] owner)
        {
            using var it = _mgr.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            it.Seek(owner);
            int n = 0;
            while (it.Valid())
            {
                if (!ByteUtil.StartsWith(it.Key(), owner)) break;
                n++;
                it.Next();
            }
            return n;
        }

        private static byte[] ValueBytes(int v)
        {
            var b = new System.Numerics.BigInteger(v).ToByteArray(isUnsigned: true, isBigEndian: true);
            return b.Length == 0 ? new byte[] { 0 } : b;
        }

        public void Dispose()
        {
            _bundle?.Dispose();
            _mgr?.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }
    }
}
