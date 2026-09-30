using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Numerics;
using Xunit;

namespace Nethereum.EVM.UnitTests.StateChanges
{
    public class AccountExecutionStateStorageHexTests
    {
        [Fact]
        public void GetContractStorageAsHex_SlotKey_RoundTripsToCorrectSlotNumberViaHexToBigInteger()
        {
            var slot = new EvmUInt256((ulong)100500);
            var value = new byte[32];
            value[31] = 0x2a;

            var accountState = new AccountExecutionState();
            accountState.Storage[slot] = value;

            var storageAsHex = accountState.GetContractStorageAsHex();

            var key = Assert.Single(storageAsHex).Key;

            Assert.NotEqual("100500", key);

            var parsedSlot = key.HexToBigInteger(false);
            Assert.Equal(new BigInteger(100500), parsedSlot);
        }

        [Fact]
        public void GetContractStorageAsHex_KeyAndValue_AreConsistentlyFormattedHex()
        {
            var slot = new EvmUInt256((ulong)7);
            var value = new byte[32];
            value[31] = 0x09;

            var accountState = new AccountExecutionState();
            accountState.Storage[slot] = value;

            var storageAsHex = accountState.GetContractStorageAsHex();
            var entry = Assert.Single(storageAsHex);

            Assert.Equal(64, entry.Key.Length);
            Assert.Equal(64, entry.Value.Length);
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000007", entry.Key);
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000009", entry.Value);
        }
    }
}
