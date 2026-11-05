using System;
using System.Text;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.RLP.UnitTests
{
    public class FoundationsRlpDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-reader", "Read an RLP list field by field with RlpReader")]
        public void Given_AnEncodedList_When_ReadWithRlpReader_Then_EachFieldIsReadInOrderAndTheEndIsChecked()
        {
            var encoded = RLP.EncodeList(
                RLP.EncodeElement("cat".ToBytesForRLPEncoding()),
                RLP.EncodeElement(1000.ToBytesForRLPEncoding()),
                RLP.EncodeElement(new byte[] { 0x01 }));

            var reader = new RlpReader(encoded);
            var list = reader.ReadListHeader();
            var name = Encoding.UTF8.GetString(reader.ReadBytes());
            var amount = reader.ReadUInt64();
            var flag = reader.ReadBool();
            reader.EnsureEnd(list);
            reader.EnsureEnd();

            Assert.Equal("cat", name);
            Assert.Equal(1000UL, amount);
            Assert.True(flag);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-reader-strict", "RlpReader refuses non-canonical integers and wrong-width fixed fields")]
        public void Given_NonCanonicalOrWrongWidthFields_When_ReadWithRlpReader_Then_ItThrows()
        {
            var leadingZero = new RlpReader("820001".HexToByteArray());
            Assert.Throws<InvalidOperationException>(() => leadingZero.ReadUInt64());

            var fixedField = new RlpReader("8301020304".HexToByteArray());
            Assert.Throws<InvalidOperationException>(() => fixedField.ReadFixed(32));

            var list = new RlpReader("c0".HexToByteArray());
            Assert.Equal(RlpKind.List, list.PeekKind());
            Assert.Throws<InvalidOperationException>(() => list.ReadBytes());
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-untrusted-budget", "Decode untrusted RLP under an element budget")]
        public void Given_APayloadWithMoreElementsThanTheBudget_When_Decoded_Then_ItIsRejectedBeforeTheTreeIsBuilt()
        {
            var payload = new byte[3 + 1000];
            payload[0] = 0xf9;
            payload[1] = 0x03;
            payload[2] = 0xe8;
            for (var i = 3; i < payload.Length; i++) payload[i] = 0x80;

            var budget = RLP.UntrustedElementBudget(payload.Length);

            Assert.Equal(RLP.MinUntrustedElementBudget, RLP.UntrustedElementBudget(100));
            Assert.Throws<Exception>(() => RLP.Decode(payload, 100));
            var decoded = (RLPCollection)RLP.Decode(payload, budget);
            Assert.Equal(1000, decoded.Count);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-canonical-decode", "Decode rejects non-canonical RLP and over-deep nesting")]
        public void Given_NonCanonicalOrOverDeepRlp_When_Decoded_Then_AnExceptionIsThrown()
        {
            Assert.Throws<Exception>(() => RLP.Decode("8100".HexToByteArray()));
            Assert.Throws<Exception>(() => RLP.Decode("b80101".HexToByteArray()));
            Assert.Throws<Exception>(() => RLP.Decode("c00000".HexToByteArray()));

            var nested = new byte[] { 0xc0 };
            for (var depth = 0; depth < RLP.MaxDecodeDepth + 1; depth++)
                nested = RLP.EncodeList(nested);
            Assert.Throws<Exception>(() => RLP.Decode(nested));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-uint64-bits", "ToLongBitsFromRLPDecoded reads a full uint64")]
        public void Given_AUint64AboveTheLongRange_When_ReadWithToLongBitsFromRLPDecoded_Then_TheSameBitsComeBackAsALong()
        {
            var bytes = "ffffffffffffffff".HexToByteArray();

            Assert.Equal(-1L, bytes.ToLongBitsFromRLPDecoded());
            Assert.Equal(0x8000000000000000UL, (ulong)"8000000000000000".HexToByteArray().ToLongBitsFromRLPDecoded());
        }
    }
}
