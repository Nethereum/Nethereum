using System;
using System.Linq;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class FoundationsModelDocExampleTests
    {
        private static byte[] Element(byte[] value) => RLP.RLP.EncodeElement(value);

        private static byte[] LegacyTransactionWithReceiver(int receiverBytes)
        {
            return RLP.RLP.EncodeList(
                Element(new byte[] { 0x09 }),
                Element(new byte[] { 0x3b, 0x9a, 0xca, 0x00 }),
                Element(new byte[] { 0x52, 0x08 }),
                Element(Enumerable.Repeat((byte)0x35, receiverBytes).ToArray()),
                Element(new byte[] { 0x0d, 0xe0, 0xb6, 0xb3, 0xa7, 0x64, 0x00, 0x00 }),
                Element(Array.Empty<byte>()),
                Element(new byte[] { 0x25 }),
                Element(Enumerable.Repeat((byte)0x11, 32).ToArray()),
                Element(Enumerable.Repeat((byte)0x22, 32).ToArray()));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "model-read-transaction", "Decode a peer transaction with TransactionFactory.ReadTransaction")]
        public void Given_ALegacyTransaction_When_ReadFromAnRlpReader_Then_ItEqualsTheTransactionCreatedFromTheBytes()
        {
            var rlp = LegacyTransactionWithReceiver(20);

            var fromBytes = TransactionFactory.CreateTransaction(rlp);
            var fromReader = TransactionFactory.ReadTransaction(new RlpReader(rlp), allowBlobNetworkWrapper: false);

            Assert.Equal(fromBytes.Hash.ToHex(), fromReader.Hash.ToHex());
            Assert.Equal(fromBytes.TransactionType, fromReader.TransactionType);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "model-transaction-field-format", "A malformed transaction field raises TransactionFieldFormatException")]
        public void Given_ATransactionWhoseReceiverIsNotTwentyBytes_When_Decoded_Then_ATransactionFieldFormatExceptionNamesTheField()
        {
            var rlp = LegacyTransactionWithReceiver(19);

            var exception = Assert.Throws<TransactionFieldFormatException>(() => TransactionFactory.CreateTransaction(rlp));

            Assert.Equal(LegacyTransactionField.ReceiveAddress, exception.Field);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "model-header-field-width", "Header decoders refuse a hash field of the wrong width")]
        public void Given_AHeaderWhoseMixHashIsShort_When_Decoded_Then_TheHeaderIsRefused()
        {
            var wire = BlockHeaderEncoder.Current.Encode(BlockHeaderShapes.For("london"));
            var fields = (RLPCollection)RLP.RLP.Decode(wire);
            var rebuilt = new byte[fields.Count][];
            for (var i = 0; i < rebuilt.Length; i++)
                rebuilt[i] = Element(i == 13 ? new byte[31] : fields[i].RLPData);

            Assert.ThrowsAny<Exception>(() => BlockHeaderEncoder.Current.Decode(RLP.RLP.EncodeList(rebuilt)));
            Assert.Equal(32, BlockHeaderEncoder.Current.Decode(wire).MixHash.Length);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "model-header-reader", "Decode a header from an RlpReader")]
        public void Given_AnEncodedHeader_When_DecodedFromAnRlpReader_Then_ItRoundTripsTheHash()
        {
            var header = BlockHeaderShapes.For("cancun");
            var wire = BlockHeaderEncoder.Current.Encode(header);

            var reader = new RlpReader(wire);
            var decoded = BlockHeaderEncoder.Current.Decode(reader);
            reader.EnsureEnd();

            Assert.Equal(wire.ToHex(), BlockHeaderEncoder.Current.Encode(decoded).ToHex());
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "model-block-range-validator", "Validate a peer's advertised block range")]
        public void Given_AnAdvertisedBlockRange_When_Validated_Then_AViolationNamesWhatIsWrong()
        {
            var hash = Enumerable.Repeat((byte)0xAB, 32).ToArray();

            Assert.Null(BlockRangeValidator.Violation(0, 100, hash));
            Assert.Equal("earliest > latest", BlockRangeValidator.Violation(101, 100, hash));
            Assert.Equal("zero latest hash", BlockRangeValidator.Violation(0, 100, new byte[32]));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "model-peer-serving-limits", "Serving and announcement limits")]
        public void Given_TheServingLimits_Then_TheyCarryTheValuesPeersAreHeldTo()
        {
            Assert.Equal(5000, PeerResponseBounds.MaxTransactionAnnouncements);
            Assert.Equal(1024, SnapServingLimits.MaxTrieNodeLookups);
            Assert.Equal(128, SnapServingLimits.MaxResponseProofNodes);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "model-enr-list-value", "An ENR list value is kept as its exact encoded bytes")]
        public void Given_AListValuedEnrEntry_When_EncodedAndDecoded_Then_TheListSurvivesVerbatim()
        {
            var record = new EnrRecord { Sequence = 1 };
            record.Pairs["ip"] = new byte[] { 8, 8, 4, 4 };
            var forkId = new byte[] { 0xc2, 0x01, 0x02 };
            record.SetListValue("eth", forkId);

            var decoded = EnrRecordEncoder.Decode(EnrRecordEncoder.EncodeRecord(record));

            Assert.True(decoded.IsListValue("eth"));
            Assert.False(decoded.IsListValue("ip"));
            Assert.Equal(forkId, decoded.Pairs["eth"]);
            Assert.Throws<ArgumentException>(() => record.SetListValue("eth", new byte[] { 0x01 }));
        }
    }
}
