using System;
using System.IO;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Freezer.FilterMaps;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbFilterMapsStoreTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;
        private readonly RocksDbFilterMapsStore _store;

        public RocksDbFilterMapsStoreTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_fm_test_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
            _store = new RocksDbFilterMapsStore(_manager);
        }

        public void Dispose()
        {
            _manager.Dispose();
            if (Directory.Exists(_dbPath)) { try { Directory.Delete(_dbPath, true); } catch { } }
        }

        [Fact]
        public void Given_TheManager_When_Opened_Then_TheLogFilterMapsCfIsRegistered()
        {
            Assert.True(_manager.HasColumnFamily(HistoryColumnFamilies.LogFilterMaps));
            Assert.Contains(HistoryColumnFamilies.LogFilterMaps, _manager.OpenColumnFamilyNames);
        }

        [Fact]
        public void Given_ARange_When_WriteThenReadRange_Then_TheFieldsAreByteIdentical()
        {
            var range = new FilterMapsRange(
                version: 2, headIndexed: true, headDelimiter: 999,
                blocksFirst: 10, blocksAfterLast: 20,
                mapsFirst: 0, mapsAfterLast: 3, tailPartialEpoch: 0);

            _store.WriteRange(range);
            var readBack = _store.ReadRange();

            Assert.NotNull(readBack);
            Assert.Equal(range.Version, readBack.Version);
            Assert.Equal(range.HeadIndexed, readBack.HeadIndexed);
            Assert.Equal(range.HeadDelimiter, readBack.HeadDelimiter);
            Assert.Equal(range.BlocksFirst, readBack.BlocksFirst);
            Assert.Equal(range.BlocksAfterLast, readBack.BlocksAfterLast);
            Assert.Equal(range.MapsFirst, readBack.MapsFirst);
            Assert.Equal(range.MapsAfterLast, readBack.MapsAfterLast);
            Assert.Equal(range.TailPartialEpoch, readBack.TailPartialEpoch);
        }

        [Fact]
        public void Given_NoRangeWritten_When_ReadRange_Then_ReturnsNull()
        {
            Assert.Null(_store.ReadRange());
        }

        [Fact]
        public void Given_ABaseRowGroupValue_When_WriteThenRead_Then_TheBytesAreIdentical()
        {
            var value = new byte[] { 0x01, 0x05, 0x00, 0x00 };

            _store.WriteBaseRowGroup(mapRowIndex: 4242, value);

            Assert.Equal(value, _store.ReadBaseRowGroup(4242));
        }

        [Fact]
        public void Given_ABaseRowGroupValue_When_ReadingTheExtRowKeyOrADifferentIndex_Then_ItIsAbsent()
        {
            var value = new byte[] { 0x01, 0x05, 0x00, 0x00 };
            _store.WriteBaseRowGroup(mapRowIndex: 4242, value);

            Assert.Null(_store.ReadBaseRowGroup(4243));
            Assert.Null(_store.ReadExtRow(4242));
        }

        [Fact]
        public void Given_BaseAndExtRowsWritten_When_ReadViaTheRawSchemaKeys_Then_EachLandsUnderItsGethConformantKey()
        {
            _store.WriteBaseRowGroup(mapRowIndex: 55, new byte[] { 0xAA });
            _store.WriteExtRow(mapRowIndex: 55, new byte[] { 0xBB });

            var baseKey = FilterMapsSchema.BaseRowKey(55);
            var extKey = FilterMapsSchema.ExtRowKey(55);
            Assert.Equal(13, baseKey.Length);
            Assert.Equal(12, extKey.Length);

            Assert.Equal(new byte[] { 0xAA }, _manager.Get(HistoryColumnFamilies.LogFilterMaps, baseKey));
            Assert.Equal(new byte[] { 0xBB }, _manager.Get(HistoryColumnFamilies.LogFilterMaps, extKey));
        }

        [Fact]
        public void Given_AnExtRowValue_When_WriteThenRead_Then_TheBytesAreIdentical()
        {
            var value = new byte[] { 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f };

            _store.WriteExtRow(mapRowIndex: 777, value);

            Assert.Equal(value, _store.ReadExtRow(777));
        }

        [Fact]
        public void Given_ANonEmptyBaseRowGroup_When_OverwrittenWithZeroLength_Then_TheKeyIsDeleted()
        {
            _store.WriteBaseRowGroup(mapRowIndex: 5, new byte[] { 0x01, 0x02, 0x03 });
            Assert.NotNull(_store.ReadBaseRowGroup(5));

            _store.WriteBaseRowGroup(mapRowIndex: 5, Array.Empty<byte>());

            Assert.Null(_store.ReadBaseRowGroup(5));
        }

        [Fact]
        public void Given_ANonEmptyBaseRowGroup_When_OverwrittenWithNull_Then_TheKeyIsDeleted()
        {
            _store.WriteBaseRowGroup(mapRowIndex: 6, new byte[] { 0x01 });
            Assert.NotNull(_store.ReadBaseRowGroup(6));

            _store.WriteBaseRowGroup(mapRowIndex: 6, null);

            Assert.Null(_store.ReadBaseRowGroup(6));
        }

        [Fact]
        public void Given_ANonEmptyExtRow_When_OverwrittenWithZeroLength_Then_TheKeyIsDeleted()
        {
            _store.WriteExtRow(mapRowIndex: 9, new byte[] { 0x01, 0x02 });
            Assert.NotNull(_store.ReadExtRow(9));

            _store.WriteExtRow(mapRowIndex: 9, Array.Empty<byte>());

            Assert.Null(_store.ReadExtRow(9));
        }

        [Fact]
        public void Given_ALastBlockOfMap_When_WriteThenRead_Then_TheBlockNumberAndIdAreByteIdentical()
        {
            var blockId = new byte[32];
            for (var i = 0; i < 32; i++) blockId[i] = (byte)(i + 1);

            _store.WriteLastBlockOfMap(mapIndex: 3, blockNumber: 123456, blockId);
            var readBack = _store.ReadLastBlockOfMap(3);

            Assert.NotNull(readBack);
            Assert.Equal(123456, readBack.Value.blockNumber);
            Assert.Equal(blockId, readBack.Value.blockId);
        }

        [Fact]
        public void Given_NoLastBlockOfMapWritten_When_Read_Then_ReturnsNull()
        {
            Assert.Null(_store.ReadLastBlockOfMap(999));
        }

        [Fact]
        public void Given_ABlockLvPointer_When_WriteThenRead_Then_TheValueIsIdentical()
        {
            _store.WriteBlockLvPointer(blockNumber: 42, lvPointer: 999999);

            Assert.Equal(999999, _store.ReadBlockLvPointer(42));
        }

        [Fact]
        public void Given_NoBlockLvPointerWritten_When_Read_Then_ReturnsNull()
        {
            Assert.Null(_store.ReadBlockLvPointer(12345));
        }

        [Fact]
        public void Given_WritesAcrossAllShapes_When_Reopened_Then_EveryValueSurvives()
        {
            var blockId = new byte[32];
            blockId[0] = 0xAB;
            _store.WriteRange(new FilterMapsRange(2, true, 1, 2, 3, 4, 5, 6));
            _store.WriteBaseRowGroup(1, new byte[] { 0x11 });
            _store.WriteExtRow(2, new byte[] { 0x22 });
            _store.WriteLastBlockOfMap(3, 100, blockId);
            _store.WriteBlockLvPointer(4, 200);
            _manager.Dispose();

            using var reopened = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
            var store = new RocksDbFilterMapsStore(reopened);

            Assert.NotNull(store.ReadRange());
            Assert.Equal(new byte[] { 0x11 }, store.ReadBaseRowGroup(1));
            Assert.Equal(new byte[] { 0x22 }, store.ReadExtRow(2));
            Assert.Equal(100, store.ReadLastBlockOfMap(3).Value.blockNumber);
            Assert.Equal(200, store.ReadBlockLvPointer(4));
        }
    }
}
