using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class StorageProofKeyEncodingTests
    {
        private static readonly Sha3KeccackHashProvider _sha = new Sha3KeccackHashProvider();

        private static byte[] CanonicalKey(BigInteger slot)
            => _sha.ComputeHash(slot.ToBytesForRLPEncoding().PadTo32Bytes());

        [Theory]
        [InlineData(0x00)]
        [InlineData(0x01)]
        [InlineData(0x7f)]
        [InlineData(0x80)]
        [InlineData(0xff)]
        [InlineData(0x0100)]
        [InlineData(12345)]
        public void EncodeKeyForStorage_IsKeccakPad32OfSlot_AcrossSlotSizes(int slotInt)
        {
            var slot = (BigInteger)slotInt;
            var expected = CanonicalKey(slot);
            var actual = AccountStorage.EncodeKeyForStorage(slot.ToBytesForRLPEncoding(), _sha);
            Assert.Equal(expected.ToHex(), actual.ToHex());
        }

        [Fact]
        public void EncodeKeyForStorage_TrimmedAndPaddedSlot_ProduceSameKey()
        {
            var slot = (BigInteger)0x0100;
            var trimmed = slot.ToBytesForRLPEncoding();
            var padded = trimmed.PadTo32Bytes();

            var fromTrimmed = AccountStorage.EncodeKeyForStorage(trimmed, _sha);
            var fromPadded = AccountStorage.EncodeKeyForStorage(padded, _sha);

            Assert.Equal(CanonicalKey(slot).ToHex(), fromTrimmed.ToHex());
            Assert.Equal(fromTrimmed.ToHex(), fromPadded.ToHex());
        }
    }
}
