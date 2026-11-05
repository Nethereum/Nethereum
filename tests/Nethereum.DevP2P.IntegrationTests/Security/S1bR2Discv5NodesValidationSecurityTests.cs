using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class S1bR2Discv5NodesValidationSecurityTests
    {
        private static byte[] Ping() => Discv5MessageEncoder.EncodePing(new Discv5PingMessage { RequestId = new byte[] { 1 }, EnrSeq = 1 });

        private static EthECKey KeyWithDistance(byte[] peerId, Func<uint, bool> accept)
        {
            while (true)
            {
                var key = EthECKey.GenerateKey();
                var id = Discv5Crypto.ComputeNodeId(key.GetPubKeyNoPrefix());
                if (accept(Discv5RoutingTable.LogDistance(peerId, id))) return key;
            }
        }

        private static async Task<List<EnrRecord>> FindNodeAnsweredWithAsync(Func<byte[], List<byte[]>> records)
        {
            var key = EthECKey.GenerateKey();
            await using var listener = new Discv5Listener(key);
            listener.Start(IPAddress.Loopback, 0);
            listener.LocalEnrEncoded = Discv5RawPeer.SignEnr(key, IPAddress.Loopback, listener.Port, 1);
            using var peer = new Discv5RawPeer(new IPEndPoint(IPAddress.Loopback, listener.Port), key);
            Assert.NotNull(await peer.EstablishSessionAsync(Ping()));

            var task = listener.SendFindNodeAsync(peer.Endpoint, peer.NodeId, null, new uint[] { 256 }, TimeSpan.FromSeconds(5), CancellationToken.None);
            byte[] requestId = null;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(4);
            while (requestId == null && DateTime.UtcNow < deadline)
            {
                var message = await peer.ReceiveMessageAsync(TimeSpan.FromSeconds(1));
                if (message == null) continue;
                var (type, body) = Discv5MessageEncoder.Unpack(message);
                if (type == Discv5MessageType.FindNode) requestId = Discv5MessageEncoder.DecodeFindNode(body).RequestId;
            }
            Assert.NotNull(requestId);

            var nodes = new Discv5NodesMessage { RequestId = requestId, Total = 1 };
            nodes.Records.AddRange(records(peer.NodeId));
            await peer.SendMessageAsync(Discv5MessageEncoder.EncodeNodes(nodes));
            return await task;
        }

        private static byte[] Record(EthECKey key, string ip, int port)
            => Discv5RawPeer.SignEnr(key, IPAddress.Parse(ip), port, 1, tcpPort: 30303);

        private static bool Contains(List<EnrRecord> result, byte[] encoded)
            => result.Any(r => EnrRecordEncoder.EncodeRecord(r).SequenceEqual(encoded));

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_NodesWithARecordAtARequestedDistance_When_FindNodeIsAnswered_Then_TheRecordIsReturned()
        {
            byte[] good = null;
            var result = await FindNodeAnsweredWithAsync(peerId =>
            {
                good = Record(KeyWithDistance(peerId, d => d == 256), "8.8.4.4", 30303);
                return new List<byte[]> { good };
            });

            Assert.True(Contains(result, good));
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_NodesWithARecordAtADistanceNobodyAskedFor_When_FindNodeIsAnswered_Then_ThatRecordIsDropped()
        {
            byte[] wrong = null;
            var result = await FindNodeAnsweredWithAsync(peerId =>
            {
                wrong = Record(KeyWithDistance(peerId, d => d != 256), "9.9.9.9", 30303);
                return new List<byte[]> { wrong };
            });

            Assert.False(Contains(result, wrong), "a record at an unrequested log distance was returned");
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_NodesRepeatingTheSameRecord_When_FindNodeIsAnswered_Then_ItIsReturnedOnce()
        {
            var result = await FindNodeAnsweredWithAsync(peerId =>
            {
                var record = Record(KeyWithDistance(peerId, d => d == 256), "8.8.4.4", 30303);
                return new List<byte[]> { record, record, record };
            });

            Assert.Single(result);
        }

        [Theory]
        [Trait("Category", "Security")]
        [InlineData("224.0.0.251", 30303)]
        [InlineData("255.255.255.255", 30303)]
        [InlineData("8.8.4.4", 80)]
        [InlineData("8.8.4.4", 1024)]
        public async Task Given_NodesWithASpecialNetworkOrLowPortRecord_When_FindNodeIsAnswered_Then_ThatRecordIsDropped(string ip, int port)
        {
            byte[] bad = null;
            var result = await FindNodeAnsweredWithAsync(peerId =>
            {
                bad = Record(KeyWithDistance(peerId, d => d == 256), ip, port);
                return new List<byte[]> { bad };
            });

            Assert.False(Contains(result, bad), $"{ip}:{port} was accepted as a relayed node");
        }
    }
}
