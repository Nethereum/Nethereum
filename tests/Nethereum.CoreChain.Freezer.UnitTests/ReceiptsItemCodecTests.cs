using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.Documentation;
using Nethereum.Model;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.CoreChain.Freezer.UnitTests
{
    public class ReceiptsItemCodecTests
    {
        private readonly ReceiptsItemCodec _codec = new();

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Round-trip trimmed storage receipts")]
        public void Given_PostByzantiumStatusReceipts_When_RoundTrip_Then_Identity()
        {
            var receipts = new[]
            {
                new ReceiptForStorage(new byte[] { 0x01 }, 21000, new List<Log>()),
                new ReceiptForStorage(Array.Empty<byte>(), 42000, new List<Log>()),
            };

            var encoded = _codec.Encode(receipts);
            var decoded = _codec.Decode(encoded);

            Assert.Equal(2, decoded.Count);
            Assert.Equal(new byte[] { 0x01 }, decoded[0].PostStateOrStatus);
            Assert.Equal((BigInteger)21000, decoded[0].CumulativeGasUsed);
            Assert.Empty(decoded[1].PostStateOrStatus);
            Assert.Equal((BigInteger)42000, decoded[1].CumulativeGasUsed);
            Assert.Equal(encoded, _codec.Encode(decoded));
        }

        [Fact]
        public void Given_RealGethReceiptsItems_When_DecodeThenReEncode_Then_ByteIdenticalToGeth()
        {
            var count = CorpusFixture.ItemCount(CorpusFixture.TxsSliceDirectory, "receipts", compressed: true);

            for (var item = 0L; item < count; item++)
            {
                var raw = CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "receipts", compressed: true, item);

                var receipts = _codec.Decode(raw);
                var reencoded = _codec.Encode(receipts);

                Assert.Equal(raw, reencoded);
            }
        }

        [Fact]
        public void Given_RealGethReceiptsCorpus_When_ScanningForLogs_Then_SomeBlockDecodesLogsWithTopics()
        {
            var count = CorpusFixture.ItemCount(CorpusFixture.TxsSliceDirectory, "receipts", compressed: true);

            for (var item = 0L; item < count; item++)
            {
                var raw = CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "receipts", compressed: true, item);
                var receipts = _codec.Decode(raw);
                var withLogs = receipts.FirstOrDefault(r => r.Logs.Count > 0);
                if (withLogs == null) continue;

                Assert.NotEmpty(withLogs.Logs[0].Topics);
                Assert.NotNull(withLogs.Logs[0].Address);
                return;
            }

            Assert.Fail("expected at least one block with logs in the geth-ancient-txs corpus slice");
        }

        [Fact]
        public void Given_ConsensusFourFieldShape_When_ComparedToGethStorageBytes_Then_TheyDiffer()
        {
            var raw = CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "receipts", compressed: true, itemNumber: 0);
            var stored = _codec.Decode(raw);
            Assert.NotEmpty(stored);

            var consensusShaped = EncodeWithBloomField(stored);

            Assert.NotEqual(raw, consensusShaped);
        }

        private static byte[] EncodeWithBloomField(System.Collections.Generic.IReadOnlyList<ReceiptForStorage> receipts)
        {
            var encoded = new byte[receipts.Count][];
            for (var i = 0; i < receipts.Count; i++)
            {
                var r = receipts[i];
                var encodedLogs = new byte[r.Logs.Count][];
                for (var j = 0; j < r.Logs.Count; j++)
                    encodedLogs[j] = Nethereum.Model.LogEncoder.Current.Encode(r.Logs[j]);

                encoded[i] = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(r.PostStateOrStatus),
                    RLP.RLP.EncodeElement(r.CumulativeGasUsed.ToBytesForRLPEncoding()),
                    RLP.RLP.EncodeElement(new byte[256]),
                    RLP.RLP.EncodeList(encodedLogs));
            }
            return RLP.RLP.EncodeList(encoded);
        }
    }
}
