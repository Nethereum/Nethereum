using System.Numerics;
using Nethereum.CoreChain.Storage;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class StateKeysTests
    {
        [Theory]
        [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed")]
        [InlineData("5aaeb6053f3e94c9b9a09f33669435e7ef1beaed")]
        [InlineData("0x0000000000000000000000000000000000000001")]
        public void Given_AnyAddressString_When_AddressBytesAndAccountKeyAreComputed_Then_AccountKeyIsKeccakOfAddressBytes(string address)
        {
            var bytes = StateKeys.AddressBytes(address);

            Assert.Equal(20, bytes.Length);
            Assert.Equal(Sha3Keccack.Current.CalculateHash(bytes), StateKeys.AccountKey(address));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(255)]
        [InlineData(65536)]
        public void Given_ASlot_When_StorageSlotBytesAndStorageSlotKeyAreComputed_Then_TheKeyIsKeccakOfThe32BytePaddedSlot(int slot)
        {
            var bytes = StateKeys.StorageSlotBytes(new BigInteger(slot));

            Assert.Equal(32, bytes.Length);
            Assert.Equal(Sha3Keccack.Current.CalculateHash(bytes), StateKeys.StorageSlotKey(new BigInteger(slot)));
        }
    }
}
