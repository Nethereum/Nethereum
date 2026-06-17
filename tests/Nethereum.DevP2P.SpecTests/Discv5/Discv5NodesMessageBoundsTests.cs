using System;
using Nethereum.DevP2P.Discv5;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    /// <summary>
    /// Per discv5-wire.md §"NODES message", <c>total</c> is a <c>uint8</c>.
    /// Decoding a NODES payload whose total field is encoded as a multi-byte
    /// integer (i.e. &gt; 255) must be rejected, since such a packet would
    /// not have been produced by any spec-conformant peer.
    /// </summary>
    public class Discv5NodesMessageBoundsTests
    {
        [Fact]
        public void Given_NodesPayloadWithTotalOver255_When_Decoded_Then_Rejected()
        {
            // Hand-craft a NODES body: [reqId, total=256 (two bytes), [empty records]]
            var reqId = RLP.RLP.EncodeElement(new byte[] { 0x01 });
            var total = RLP.RLP.EncodeElement(new byte[] { 0x01, 0x00 }); // 256 BE
            var records = RLP.RLP.EncodeList(new byte[0][]);
            var body = RLP.RLP.EncodeList(reqId, total, records);

            Assert.Throws<ArgumentException>(() => Discv5MessageEncoder.DecodeNodes(body));
        }

        [Fact]
        public void Given_NodesPayloadWithTotal255_When_Decoded_Then_Accepted()
        {
            var msg = new Discv5NodesMessage
            {
                RequestId = new byte[] { 0x01 },
                Total = 255,
            };
            var encoded = Discv5MessageEncoder.EncodeNodes(msg);
            var (_, body) = Discv5MessageEncoder.Unpack(encoded);
            var decoded = Discv5MessageEncoder.DecodeNodes(body);
            Assert.Equal((byte)255, decoded.Total);
        }
    }
}
