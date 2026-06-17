using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P.Les;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Les
{
    public class LesMessageRoundTripTests
    {
        [Fact]
        public void Status_KeyValueList_RoundTrip()
        {
            var msg = new LesStatusMessage
            {
                ProtocolVersion = 4,
                NetworkId = 1,
                HeadHash = Make32(0xAB),
                HeadNumber = 22_500_000,
                GenesisHash = Make32(0xCD)
            };
            msg.Entries["forkID"] = Encoding.ASCII.GetBytes("test");
            msg.Entries["recentTxLookup"] = new byte[] { 0x10 };

            var bytes = LesStatusMessageEncoder.Encode(msg);
            var decoded = LesStatusMessageEncoder.Decode(bytes);

            Assert.Equal(4, decoded.ProtocolVersion);
            Assert.Equal(1ul, decoded.NetworkId);
            Assert.Equal(msg.HeadHash.ToHex(), decoded.HeadHash.ToHex());
            Assert.Equal(22_500_000ul, decoded.HeadNumber);
            Assert.Equal(msg.GenesisHash.ToHex(), decoded.GenesisHash.ToHex());
            Assert.Equal("test", Encoding.ASCII.GetString(decoded.Entries["forkID"]));
            Assert.Equal(0x10, decoded.Entries["recentTxLookup"][0]);
        }

        [Fact]
        public void Announce_RoundTrip_PreservesAllFields()
        {
            var msg = new LesAnnounceMessage
            {
                HeadHash = Make32(0x11),
                HeadNumber = 100_000,
                HeadTd = BigInteger.Parse("12345678901234567890"),
                ReorgDepth = 3
            };
            msg.Auxiliary["protocolVersion"] = new byte[] { 4 };

            var bytes = LesAnnounceMessageEncoder.Encode(msg);
            var decoded = LesAnnounceMessageEncoder.Decode(bytes);

            Assert.Equal(msg.HeadHash.ToHex(), decoded.HeadHash.ToHex());
            Assert.Equal(msg.HeadNumber, decoded.HeadNumber);
            Assert.Equal(msg.HeadTd, decoded.HeadTd);
            Assert.Equal(msg.ReorgDepth, decoded.ReorgDepth);
            Assert.Equal(4, decoded.Auxiliary["protocolVersion"][0]);
        }

        [Fact]
        public void GetProofsV2_RoundTrip_PreservesRequests()
        {
            var msg = new GetProofsV2Message
            {
                RequestId = 0xDEADul,
                Requests = new List<GetProofsV2Message.ProofRequest>
                {
                    new() { BlockHash = Make32(0x01), AccountKey = Make32(0x02), StorageKey = new byte[0], FromLevel = 0 },
                    new() { BlockHash = Make32(0x03), AccountKey = Make32(0x04), StorageKey = Make32(0x05), FromLevel = 1 }
                }
            };

            var bytes = GetProofsV2MessageEncoder.Encode(msg);
            var decoded = GetProofsV2MessageEncoder.Decode(bytes);

            Assert.Equal(msg.RequestId, decoded.RequestId);
            Assert.Equal(2, decoded.Requests.Count);
            Assert.Equal(msg.Requests[0].BlockHash.ToHex(), decoded.Requests[0].BlockHash.ToHex());
            Assert.Equal(msg.Requests[1].StorageKey.ToHex(), decoded.Requests[1].StorageKey.ToHex());
            Assert.Equal(1u, decoded.Requests[1].FromLevel);
        }

        [Fact]
        public void ProofsV2_RoundTrip_PreservesAllNodes()
        {
            var msg = new ProofsV2Message
            {
                RequestId = 7,
                BufferValue = 100_000,
                Nodes = new List<byte[]>
                {
                    new byte[] { 0xA1, 0xB2, 0xC3 },
                    new byte[] { 0xD4, 0xE5, 0xF6, 0x00 }
                }
            };

            var bytes = ProofsV2MessageEncoder.Encode(msg);
            var decoded = ProofsV2MessageEncoder.Decode(bytes);

            Assert.Equal(msg.RequestId, decoded.RequestId);
            Assert.Equal(msg.BufferValue, decoded.BufferValue);
            Assert.Equal(2, decoded.Nodes.Count);
            Assert.Equal(msg.Nodes[0].ToHex(), decoded.Nodes[0].ToHex());
            Assert.Equal(msg.Nodes[1].ToHex(), decoded.Nodes[1].ToHex());
        }

        private static byte[] Make32(byte fill)
        {
            var b = new byte[32];
            for (int i = 0; i < 32; i++) b[i] = (byte)(fill ^ i);
            return b;
        }
    }
}
