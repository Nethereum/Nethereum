using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RootIdentityWithTrimTests : IDisposable
    {
        private readonly RocksDbTestFixture _paddedFixture;
        private readonly RocksDbTestFixture _trimmedFixture;
        private const string Address = "0x00000000000000000000000000000000000099";

        public RootIdentityWithTrimTests()
        {
            _paddedFixture = new RocksDbTestFixture();
            _trimmedFixture = new RocksDbTestFixture();
        }

        public void Dispose()
        {
            _paddedFixture.Dispose();
            _trimmedFixture.Dispose();
        }

        [Fact]
        public async Task ComputeStateRootAsync_LeadingZeroSlot_IdenticalRoot_PaddedFlatBytesVsTrimmedFlatBytes()
        {
            var slot = new BigInteger(5);
            var account = new Account { Balance = (EvmUInt256)1000, Nonce = (EvmUInt256)1 };

            await _paddedFixture.StateStore.SaveAccountAsync(Address, account);
            var paddedValue = new byte[32];
            paddedValue[31] = 0x2A;
            var rawKey = RawStorageKey(Address, slot);
            _paddedFixture.Manager.Put(
                Nethereum.CoreChain.RocksDB.RocksDbManager.CF_STATE_STORAGE, rawKey, paddedValue);

            await _trimmedFixture.StateStore.SaveAccountAsync(Address, account);
            await _trimmedFixture.StateStore.SaveStorageAsync(Address, slot, paddedValue);

            var confirmTrimmed = await _trimmedFixture.StateStore.GetStorageAsync(Address, slot);
            Assert.Equal(new byte[] { 0x2A }, confirmTrimmed);

            var calcPadded = new IncrementalStateRootCalculator(_paddedFixture.StateStore, _paddedFixture.TrieNodeStore);
            var calcTrimmed = new IncrementalStateRootCalculator(_trimmedFixture.StateStore, _trimmedFixture.TrieNodeStore);

            var rootFromPaddedFlat = await calcPadded.ComputeStateRootAsync();
            var rootFromTrimmedFlat = await calcTrimmed.ComputeStateRootAsync();

            Assert.Equal(rootFromPaddedFlat, rootFromTrimmedFlat);
        }

        private static byte[] RawStorageKey(string address, BigInteger slot)
        {
            var addressKey = Nethereum.CoreChain.Storage.StateKeys.AccountKey(address);
            var slotKey = Nethereum.CoreChain.Storage.StateKeys.StorageSlotKey(slot);
            var key = new byte[addressKey.Length + slotKey.Length];
            Buffer.BlockCopy(addressKey, 0, key, 0, addressKey.Length);
            Buffer.BlockCopy(slotKey, 0, key, addressKey.Length, slotKey.Length);
            return key;
        }
    }
}
