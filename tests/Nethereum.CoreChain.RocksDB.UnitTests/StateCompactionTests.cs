using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class StateCompactionTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"statecompact_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        [Fact]
        public async Task CompactState_PreservesEveryRow()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
            var bundle = RocksDbChainStoreBundle.FromManager(manager, _dir);

            var flat = new RocksDbStateStore(manager);
            var accountHash = Enumerable32(0x42);
            var slotHash = Enumerable32(0x11);
            await flat.SaveAccountByHashAsync(accountHash, new Account
            {
                Nonce = (EvmUInt256)7,
                Balance = (EvmUInt256)700,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            });
            await flat.SaveStorageByHashAsync(accountHash, slotHash, new byte[] { 0x0B });

            var compactor = Assert.IsAssignableFrom<IStateCompaction>(bundle);
            var messages = 0;
            await compactor.CompactStateAsync(_ => Interlocked.Increment(ref messages), CancellationToken.None);

            Assert.True(messages >= 8, "expected start+done progress per state CF");
            Assert.NotNull(manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, accountHash));
            var slotKey = new byte[64];
            Buffer.BlockCopy(accountHash, 0, slotKey, 0, 32);
            Buffer.BlockCopy(slotHash, 0, slotKey, 32, 32);
            Assert.Equal(new byte[] { 0x0B }, manager.Get(RocksDbManager.CF_STATE_STORAGE, slotKey));
        }

        private static byte[] Enumerable32(byte fill)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = fill;
            return h;
        }
    }
}
