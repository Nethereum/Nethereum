using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Freezer")]
    public class RocksDbLogStoreKeyTests : IDisposable
    {
        private static readonly string Address = "0x" + new string('a', 40);
        private static readonly byte[] BlockHash = Enumerable.Repeat((byte)0xBB, 32).ToArray();
        private static readonly byte[] TxHash = Enumerable.Repeat((byte)0xCC, 32).ToArray();
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"logkeys_{Guid.NewGuid():N}");
        private readonly RocksDbManager _manager;
        private readonly RocksDbLogStore _store;

        public RocksDbLogStoreKeyTests()
        {
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            _store = new RocksDbLogStore(_manager);
        }

        public void Dispose()
        {
            _manager.Dispose();
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static List<Log> OneLog() => new List<Log>
        {
            new Log { Address = Address, Topics = new List<byte[]> { Enumerable.Repeat((byte)0x11, 32).ToArray() }, Data = new byte[] { 1 } }
        };

        private List<byte[]> Keys(string cf)
        {
            var keys = new List<byte[]>();
            using var it = _manager.CreateIterator(cf);
            for (it.SeekToFirst(); it.Valid(); it.Next()) keys.Add(it.Key());
            return keys;
        }

        [Fact]
        public async Task Given_ALogAtBlockNumberAbove2To64_When_KeysAreBuilt_Then_BlockPaddingIsAllZeroAsBefore()
        {
            var above64 = BigInteger.Pow(2, 64) + 1;
            var above256 = BigInteger.Pow(2, 256) + 5;

            await _store.SaveLogsAsync(OneLog(), TxHash, BlockHash, above64, 0);
            await _store.SaveBlockBloomAsync(above64, new byte[256]);
            await _store.SaveBlockBloomAsync(above256, new byte[256]);

            var logKey = Assert.Single(Keys(RocksDbManager.CF_LOGS));
            var expectedPadded = new byte[32];
            var bigEndian = above64.ToByteArray(isUnsigned: true, isBigEndian: true);
            Buffer.BlockCopy(bigEndian, 0, expectedPadded, 32 - bigEndian.Length, bigEndian.Length);
            Assert.Equal(expectedPadded, logKey.Take(32).ToArray());
            var bloomKeys = Keys(RocksDbManager.CF_BLOCK_BLOOMS);
            Assert.Equal(2, bloomKeys.Count);
            Assert.Contains(bloomKeys, key => key.SequenceEqual(new byte[32]));
            Assert.Contains(bloomKeys, key => key.SequenceEqual(expectedPadded));
        }

        [Fact]
        public async Task Given_TheSameLogSavedByHashAddressAndBlock_When_QueriedByEach_Then_AllThreeIndexKeysResolveToTheLog()
        {
            await _store.SaveLogsAsync(OneLog(), TxHash, BlockHash, 12, 3);

            var byTx = await _store.GetLogsByTxHashAsync(TxHash);
            var byBlockHash = await _store.GetLogsByBlockHashAsync(BlockHash);
            var byBlockNumber = await _store.GetLogsByBlockNumberAsync(12);

            Assert.Single(byTx);
            Assert.Single(byBlockHash);
            Assert.Single(byBlockNumber);
            Assert.Equal(byTx[0].LogIndex, byBlockHash[0].LogIndex);
            Assert.Equal(3, byTx[0].TransactionIndex);
            var logKey = Assert.Single(Keys(RocksDbManager.CF_LOGS));
            var addressKey = Assert.Single(Keys(RocksDbManager.CF_LOG_BY_ADDRESS));
            Assert.Equal(Address.HexToByteArray().Concat(logKey).ToArray(), addressKey);
            Assert.Equal(logKey, _manager.Get(RocksDbManager.CF_LOG_BY_ADDRESS, addressKey));
            Assert.Equal(logKey, _manager.Get(RocksDbManager.CF_LOG_BY_TX, Assert.Single(Keys(RocksDbManager.CF_LOG_BY_TX))));
            Assert.Equal(logKey, _manager.Get(RocksDbManager.CF_LOG_BY_BLOCK, Assert.Single(Keys(RocksDbManager.CF_LOG_BY_BLOCK))));
        }

        [Fact]
        public async Task Given_ABlockNumberOfOne_When_CreateBlockNumberKeyIsCalled_Then_ItIs31ZeroBytesThen01()
        {
            await _store.SaveBlockBloomAsync(1, new byte[256]);

            var key = Assert.Single(Keys(RocksDbManager.CF_BLOCK_BLOOMS));
            var expected = new byte[32];
            expected[31] = 0x01;
            Assert.Equal(expected, key);
        }
    }
}
