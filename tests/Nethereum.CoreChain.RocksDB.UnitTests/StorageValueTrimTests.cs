using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class StorageValueTrimTests : IDisposable
    {
        private readonly RocksDbTestFixture _fixture;
        private const string Address = "0xabcdef0123456789abcdef0123456789abcdef01";

        public StorageValueTrimTests() => _fixture = new RocksDbTestFixture();
        public void Dispose() => _fixture.Dispose();

        private static byte[] StorageKey(string address, BigInteger slot)
        {
            var addressKey = StateKeys.AccountKey(address);
            var slotKey = StateKeys.StorageSlotKey(slot);
            var key = new byte[addressKey.Length + slotKey.Length];
            Buffer.BlockCopy(addressKey, 0, key, 0, addressKey.Length);
            Buffer.BlockCopy(slotKey, 0, key, addressKey.Length, slotKey.Length);
            return key;
        }

        private static byte[] Padded32(byte lastByte)
        {
            var padded = new byte[32];
            padded[31] = lastByte;
            return padded;
        }

        [Fact]
        public async Task SaveStorageAsync_LeadingZeroValue_DurableBytesAreTrimmed_NotPadded()
        {
            await _fixture.StateStore.SaveStorageAsync(Address, 7, Padded32(0x01));

            var raw = _fixture.Manager.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(Address, 7));

            Assert.NotNull(raw);
            Assert.Equal(new byte[] { 0x01 }, raw);
        }

        [Fact]
        public async Task SaveStorageAsync_AllZeroValue_StillDeletes()
        {
            await _fixture.StateStore.SaveStorageAsync(Address, 7, Padded32(0x01));
            await _fixture.StateStore.SaveStorageAsync(Address, 7, new byte[32]);

            var raw = _fixture.Manager.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(Address, 7));
            Assert.Null(raw);
        }

        [Fact]
        public async Task SaveStorageAsync_AlreadyTrimmedValue_IsIdempotent()
        {
            await _fixture.StateStore.SaveStorageAsync(Address, 7, new byte[] { 0x01 });

            var raw = _fixture.Manager.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(Address, 7));
            Assert.Equal(new byte[] { 0x01 }, raw);
        }

        [Fact]
        public async Task GetStorageAsync_ReturnsTheTrimmedDurableBytes()
        {
            await _fixture.StateStore.SaveStorageAsync(Address, 7, Padded32(0x01));

            var value = await _fixture.StateStore.GetStorageAsync(Address, 7);

            // The store hands back exactly what it durably persisted — trimmed. Callers that need a
            // fixed-width 32-byte value (SLOAD, eth_getStorageAt, EIP-2935/4788 system reads) pad on
            // their own side; this is confirmed separately for each of them.
            Assert.Equal(new byte[] { 0x01 }, value);
        }

        [Fact]
        public async Task SaveStorageByHashAsync_LeadingZeroValue_DurableBytesAreTrimmed()
        {
            var accountHash = new byte[32];
            accountHash[0] = 0xAA;
            var slotKeccak = new byte[32];
            slotKeccak[0] = 0xBB;

            var snapWriter = (ISnapFlatStateWriter)_fixture.StateStore;
            await snapWriter.SaveStorageByHashAsync(accountHash, slotKeccak, Padded32(0x02));

            var key = new byte[64];
            Buffer.BlockCopy(accountHash, 0, key, 0, 32);
            Buffer.BlockCopy(slotKeccak, 0, key, 32, 32);
            var raw = _fixture.Manager.Get(RocksDbManager.CF_STATE_STORAGE, key);

            Assert.Equal(new byte[] { 0x02 }, raw);
        }

        [Fact]
        public async Task SaveStorageByKeccakAsync_LeadingZeroValue_DurableBytesAreTrimmed()
        {
            var slotKeccak = new byte[32];
            slotKeccak[0] = 0xCC;

            await _fixture.StateStore.SaveStorageByKeccakAsync(Address, slotKeccak, Padded32(0x03));

            var addressKey = StateKeys.AccountKey(Address);
            var key = new byte[addressKey.Length + 32];
            Buffer.BlockCopy(addressKey, 0, key, 0, addressKey.Length);
            Buffer.BlockCopy(slotKeccak, 0, key, addressKey.Length, 32);
            var raw = _fixture.Manager.Get(RocksDbManager.CF_STATE_STORAGE, key);

            Assert.Equal(new byte[] { 0x03 }, raw);
        }

        [Fact]
        public async Task ApplyBatchAsync_NonZeroStorageEntry_LeadingZeroValue_DurableBytesAreTrimmed()
        {
            var rocksStore = (RocksDbStateStore)_fixture.StateStore;
            var batch = new FlatStateBatch(
                deletedAccountAddresses: Array.Empty<string>(),
                clearedStorageAddresses: Array.Empty<string>(),
                nonZeroStorage: new List<(string, BigInteger, byte[])> { (Address, 11, Padded32(0x04)) },
                deletedSlots: Array.Empty<(string, BigInteger)>(),
                accounts: Array.Empty<(string, Account)>(),
                code: Array.Empty<(byte[], byte[])>());

            await rocksStore.ApplyBatchAsync(batch);

            var raw = _fixture.Manager.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(Address, 11));
            Assert.Equal(new byte[] { 0x04 }, raw);
        }
    }
}
