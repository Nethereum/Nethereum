using System;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class BlockHeaderEncoderRoundTripTests
    {
        private const string AmsterdamHeaderRlp =
            "f9027ca0ec46bdbfeabaa5b15c9aba90201649b4517df10c66fce862e4f68d029c89c8b2a01dcc4de8dec75d7aab85b567b6ccd41ad312451b948a" +
            "7413f0a142fd40d49347942adc25665018aa1fe0e6bc666dac8fc2697ff9baa0ee0be0e8b9f5743e1640e6936e23f13c9b60e0231f879ff5e46f9d" +
            "013d517d17a0f939b897627cd01d9c070acdf43e1182a11ae5d586269ad12b1580040945e97aa0fbc60ff5914e57011ac8725a35675318092280bf" +
            "87ce56f984403fc458f97e36b901000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000080018407270e008303b1ec0c80a000000000000000000000" +
            "0000000000000000000000000000000000000000000088000000000000000007a056e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc00162" +
            "2fb5e363b4218080a00000000000000000000000000000000000000000000000000000000000000000a0e3b0c44298fc1c149afbf4c8996fb92427" +
            "ae41e4649b934ca495991b7852b855a01afc5b7bd62a039506e780f11cf812b02172f8ea4c49a04bb73415dea59c625480";

        private const string AmsterdamShapeHeaderHash =
            "0x1353659f544d7740174159d6767ad6011511d9afde7388dbaf00099db4ed2613";

        private const string AmsterdamHeaderHash =
            "0xbeecf14159ca2bd57e4be9554239c7f0fdbda47efe4612e38906815b6a28b3a7";

        private static BlockHeader HeaderFor(string shape) => BlockHeaderShapes.For(shape);

        private static int FieldCount(byte[] encoded)
            => ((RLP.RLPCollection)RLP.RLP.Decode(encoded)).Count;

        [Theory]
        [InlineData("frontier", 15)]
        [InlineData("london", 16)]
        [InlineData("shanghai", 17)]
        [InlineData("cancun", 20)]
        [InlineData("prague", 21)]
        [InlineData("amsterdam", 23)]
        public void Given_AHeaderOfEachShape_When_Encoded_Then_ItCarriesExactlyThatManyFields(string shape, int expected)
        {
            Assert.Equal(expected, FieldCount(BlockHeaderEncoder.Current.Encode(HeaderFor(shape))));
        }

        [Theory]
        [InlineData("frontier")]
        [InlineData("london")]
        [InlineData("shanghai")]
        [InlineData("cancun")]
        [InlineData("prague")]
        [InlineData("amsterdam")]
        public void Given_AHeaderOfEachShape_When_RoundTripped_Then_TheBytesAreIdentical(string shape)
        {
            var first = BlockHeaderEncoder.Current.Encode(HeaderFor(shape));
            var second = BlockHeaderEncoder.Current.Encode(BlockHeaderEncoder.Current.Decode(first));

            Assert.Equal(first, second);
        }

        [Fact]
        public void Given_ARealAmsterdamHeader_When_RoundTripped_Then_ItStillHashesToTheSameBlock()
        {
            var wire = AmsterdamHeaderRlp.HexToByteArray();

            var reEncoded = BlockHeaderEncoder.Current.Encode(BlockHeaderEncoder.Current.Decode(wire));

            Assert.Equal(23, FieldCount(wire));
            Assert.Equal(wire.ToHex(), reEncoded.ToHex());
            Assert.Equal(
                AmsterdamHeaderHash,
                "0x" + new Sha3Keccack().CalculateHash(reEncoded).ToHex());
        }

        [Fact]
        public void Given_ASlotNumberWithoutABlockAccessList_When_Encoded_Then_ItIsRejected()
        {
            var header = HeaderFor("prague");
            header.SlotNumber = 42;

            var rejection = Assert.Throws<ArgumentException>(() => BlockHeaderEncoder.Current.Encode(header));
            Assert.Contains("a slot number without a block access list hash", rejection.Message);
        }

        [Fact]
        public void Given_ABlockAccessListHashWithoutASlotNumber_When_Encoded_Then_ItIsRejected()
        {
            var header = HeaderFor("prague");
            header.BlockAccessListHash = new byte[32];

            var rejection = Assert.Throws<ArgumentException>(() => BlockHeaderEncoder.Current.Encode(header));
            Assert.Contains("a block access list hash without a slot number", rejection.Message);
        }

        [Fact]
        public void Given_APragueHeader_When_Encoded_Then_NoAmsterdamFieldsAreEmitted()
        {
            Assert.Equal(21, FieldCount(BlockHeaderEncoder.Current.Encode(HeaderFor("prague"))));
        }

        private static byte[] RlpNumber(System.Numerics.BigInteger value)
            => Nethereum.RLP.ConvertorForRLPEncodingExtensions.ToBytesForRLPEncoding(value);

        private static byte[] SlotNumberField(BlockHeader header)
        {
            var fields = (RLP.RLPCollection)RLP.RLP.Decode(BlockHeaderEncoder.Current.Encode(header));
            return fields[22].RLPData ?? new byte[0];
        }

        private static BlockHeader AmsterdamHeaderWithSlotNumber(ulong slotNumber)
        {
            var header = HeaderFor("amsterdam");
            header.SlotNumber = slotNumber;
            return header;
        }

        /// <summary>
        /// EIP-7843: <i>"slotNumber is a uint64 in big endian encoding."</i>
        /// </summary>
        [Fact]
        public void Given_ASlotNumberAtMaxUint64_When_TheHeaderIsEncoded_Then_TheBytesAre88FollowedByEightFF()
        {
            var header = AmsterdamHeaderWithSlotNumber(ulong.MaxValue);

            var encoded = BlockHeaderEncoder.Current.Encode(header);

            Assert.EndsWith("88ffffffffffffffff", encoded.ToHex());
            Assert.Equal(ulong.MaxValue, BlockHeaderEncoder.Current.Decode(encoded).SlotNumber);
        }

        [Fact]
        public void Given_AnOrdinarySlotNumber_When_TheHeaderIsHashed_Then_TheHashIsUnchanged()
        {
            var encoded = BlockHeaderEncoder.Current.Encode(HeaderFor("amsterdam"));

            Assert.Equal(
                AmsterdamShapeHeaderHash,
                "0x" + new Sha3Keccack().CalculateHash(encoded).ToHex());
        }

        public static TheoryData<ulong> SlotNumbersAcrossTheUint64Range => new TheoryData<ulong>
        {
            0, 1, 0x1000, 255, 256, 4_294_967_296UL, long.MaxValue,
            9_223_372_036_854_775_808UL, ulong.MaxValue - 1, ulong.MaxValue
        };

        [Theory]
        [MemberData(nameof(SlotNumbersAcrossTheUint64Range))]
        public void Given_EverySlotNumberAcrossTheUint64Range_When_EncodedTheOldAndNewWay_Then_TheBytesAgree(ulong slotNumber)
        {
            var field = SlotNumberField(AmsterdamHeaderWithSlotNumber(slotNumber));

            Assert.Equal(RlpNumber(slotNumber), field);
            if (slotNumber <= long.MaxValue)
                Assert.Equal(RlpNumber((long)slotNumber), field);
        }

        private static byte[] AmsterdamHeaderCarrying(byte[] slotNumberField)
        {
            var decoded = (RLP.RLPCollection)RLP.RLP.Decode(
                BlockHeaderEncoder.Current.Encode(HeaderFor("amsterdam")));
            var fields = new byte[decoded.Count][];
            for (var i = 0; i < decoded.Count; i++)
                fields[i] = decoded[i].RLPData ?? new byte[0];
            fields[22] = slotNumberField;
            return RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields);
        }

        /// <summary>
        /// EIP-7843: <i>"slotNumber is a uint64 in big endian encoding."</i>
        /// Nine significant bytes is not a uint64, and a decoder that took the
        /// low eight would agree with nobody about the block it had just read.
        /// </summary>
        [Fact]
        public void Given_ASlotNumberFieldOfNineSignificantBytes_When_TheHeaderIsDecoded_Then_ItIsRejected()
        {
            var overWide = AmsterdamHeaderCarrying(Enumerable.Repeat((byte)0xFF, 9).ToArray());

            Assert.Throws<OverflowException>(() => BlockHeaderEncoder.Current.Decode(overWide));
        }

        [Fact]
        public void Given_ASlotNumberEncodedWithALeadingZero_When_Decoded_Then_ItIsCurrentlyAccepted_SeeAmsRlp01()
        {
            var nonCanonical = AmsterdamHeaderCarrying(
                new byte[] { 0x00 }.Concat(Enumerable.Repeat((byte)0xFF, 8)).ToArray());

            Assert.Equal(ulong.MaxValue, BlockHeaderEncoder.Current.Decode(nonCanonical).SlotNumber);
        }
    }
}
