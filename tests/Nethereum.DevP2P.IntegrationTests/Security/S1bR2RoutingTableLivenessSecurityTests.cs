using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class S1bR2RoutingTableLivenessSecurityTests
    {
        private static readonly Sha3Keccack Keccak = new Sha3Keccack();

        private static List<Discv4Node> Discv4NodesInTheFarBucket(byte[] localId, int count)
        {
            var localHash = Keccak.CalculateHash(localId);
            var nodes = new List<Discv4Node>();
            while (nodes.Count < count)
            {
                var id = EthECKey.GenerateKey().GetPubKeyNoPrefix();
                if (((Keccak.CalculateHash(id)[0] ^ localHash[0]) & 0x80) == 0) continue;
                nodes.Add(new Discv4Node
                {
                    NodeId = id,
                    IP = IPAddress.Parse("8." + (nodes.Count + 10) + ".4.4"),
                    UdpPort = 30303,
                    TcpPort = 30303
                });
            }
            return nodes;
        }

        private static List<Discv5RoutingTable.Entry> Discv5EntriesInTheFarBucket(byte[] localId, int count)
        {
            var entries = new List<Discv5RoutingTable.Entry>();
            while (entries.Count < count)
            {
                var id = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix());
                if (((id[0] ^ localId[0]) & 0x80) == 0) continue;
                entries.Add(new Discv5RoutingTable.Entry
                {
                    NodeId = id,
                    Address = new IPEndPoint(IPAddress.Parse("8." + (entries.Count + 10) + ".4.4"), 30303),
                    EnrEncoded = new byte[] { 0xc0 }
                });
            }
            return entries;
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_FullDiscv4BucketAndAWaitingNewcomer_When_AnEntryIsRemoved_Then_TheNewcomerTakesItsPlace()
        {
            var localId = EthECKey.GenerateKey().GetPubKeyNoPrefix();
            var table = new Discv4RoutingTable(localId);
            var nodes = Discv4NodesInTheFarBucket(localId, Discv4RoutingTable.BucketSize + 1);
            foreach (var node in nodes.Take(Discv4RoutingTable.BucketSize)) table.Add(node);
            var newcomer = nodes.Last();
            table.Add(newcomer);
            Assert.DoesNotContain(table.AllNodes(), n => n.NodeId.SequenceEqual(newcomer.NodeId));

            table.Remove(nodes[0].NodeId);

            Assert.Contains(table.AllNodes(), n => n.NodeId.SequenceEqual(newcomer.NodeId));
            Assert.Equal(Discv4RoutingTable.BucketSize, table.Count);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_FullDiscv4Bucket_When_ReplacementsShareOneSubnet_Then_OnlyTheBucketIpLimitIsKept()
        {
            var localId = EthECKey.GenerateKey().GetPubKeyNoPrefix();
            var table = new Discv4RoutingTable(localId);
            var nodes = Discv4NodesInTheFarBucket(localId, Discv4RoutingTable.BucketSize + 5);
            foreach (var node in nodes.Take(Discv4RoutingTable.BucketSize)) table.Add(node);
            var sybils = nodes.Skip(Discv4RoutingTable.BucketSize).ToList();
            for (var i = 0; i < sybils.Count; i++) sybils[i].IP = IPAddress.Parse("9.9.9." + (i + 1));

            foreach (var sybil in sybils) table.Add(sybil);

            Assert.Equal(Discv4RoutingTable.BucketIpLimit, table.ReplacementsFor(sybils[0].NodeId).Count);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_FullDiscv5Bucket_When_ReplacementsShareOneSubnet_Then_OnlyTheBucketIpLimitIsKept()
        {
            var localId = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix());
            var table = new Discv5RoutingTable(localId);
            var entries = Discv5EntriesInTheFarBucket(localId, table.BucketCapacity + 5);
            foreach (var entry in entries.Take(table.BucketCapacity)) table.Upsert(entry);
            var sybils = entries.Skip(table.BucketCapacity).ToList();
            for (var i = 0; i < sybils.Count; i++) sybils[i].Address = new IPEndPoint(IPAddress.Parse("9.9.9." + (i + 1)), 30303);

            foreach (var sybil in sybils) table.Upsert(sybil);

            var distance = Discv5RoutingTable.LogDistance(localId, sybils[0].NodeId);
            Assert.Equal(Discv5RoutingTable.BucketIpLimit, table.ReplacementsAtDistance(distance).Count);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_FullDiscv5BucketAndAWaitingNewcomer_When_AnEntryIsRemoved_Then_TheNewcomerTakesItsPlace()
        {
            var localId = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix());
            var table = new Discv5RoutingTable(localId);
            var entries = Discv5EntriesInTheFarBucket(localId, table.BucketCapacity + 1);
            foreach (var entry in entries.Take(table.BucketCapacity)) table.Upsert(entry);
            var newcomer = entries.Last();
            table.Upsert(newcomer);
            Assert.DoesNotContain(table.Snapshot(), e => e.NodeId.SequenceEqual(newcomer.NodeId));

            table.Remove(entries[0].NodeId);

            Assert.Contains(table.Snapshot(), e => e.NodeId.SequenceEqual(newcomer.NodeId));
            Assert.Equal(table.BucketCapacity, table.Count);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_Discv5EntryThatFailsOnce_When_ItAnswersAfterwards_Then_ItStaysInTheTable()
        {
            var localId = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix());
            var table = new Discv5RoutingTable(localId);
            var entry = Discv5EntriesInTheFarBucket(localId, 1)[0];
            table.Upsert(entry);

            for (var round = 0; round < Discv5RoutingTable.MaxFailures; round++)
            {
                table.RecordFailure(entry.NodeId);
                table.RecordFailure(entry.NodeId);
                table.RecordSuccess(entry.NodeId);
            }

            Assert.Equal(1, table.Count);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_Discv5EntryThatKeepsFailing_When_FailuresReachTheLimit_Then_ItIsEvicted()
        {
            var localId = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix());
            var table = new Discv5RoutingTable(localId);
            var entry = Discv5EntriesInTheFarBucket(localId, 1)[0];
            table.Upsert(entry);

            for (var i = 0; i < Discv5RoutingTable.MaxFailures - 1; i++)
                Assert.False(table.RecordFailure(entry.NodeId));
            var evicted = table.RecordFailure(entry.NodeId);

            Assert.True(evicted);
            Assert.Equal(0, table.Count);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_BondedDiscv4PeerThatStopsAnswering_When_Revalidated_Then_ItIsEvicted()
        {
            var keyA = EthECKey.GenerateKey();
            using var a = new Discv4Listener(keyA, new Discv4RoutingTable(keyA.GetPubKeyNoPrefix()))
            {
                RevalidationInterval = TimeSpan.Zero,
                RevalidationTimeout = TimeSpan.FromMilliseconds(600)
            };
            var keyB = EthECKey.GenerateKey();
            var b = new Discv4Listener(keyB, new Discv4RoutingTable(keyB.GetPubKeyNoPrefix())) { RevalidationInterval = TimeSpan.Zero };
            a.Start(0, IPAddress.Loopback);
            b.Start(0, IPAddress.Loopback);
            var remote = new IPEndPoint(IPAddress.Loopback, b.Port);
            await a.SendPingAsync(remote, new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)a.Port, TcpPort = (ushort)a.Port },
                To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)b.Port, TcpPort = (ushort)b.Port },
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
            }, default, b.NodeId);
            await WaitUntil(() => a.Routing.Count == 1);

            var aliveAnswered = await a.RevalidateAsync();
            await b.StopAsync();
            var deadAnswered = await a.RevalidateAsync();

            Assert.True(aliveAnswered);
            Assert.False(deadAnswered);
            Assert.Equal(0, a.Routing.Count);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_BondedPeerLimit_When_MoreIdentitiesBond_Then_OnlyTheLimitIsKeptAndTheNewestBondIsRecorded()
        {
            var key = EthECKey.GenerateKey();
            using var listener = new Discv4Listener(key, new Discv4RoutingTable(key.GetPubKeyNoPrefix()))
            {
                RevalidationInterval = TimeSpan.Zero,
                MaxBondedPeers = 2
            };
            listener.Start(0, IPAddress.Loopback);
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);
            var peers = new List<Discv4RawPeer>();
            try
            {
                for (var i = 0; i < 4; i++)
                {
                    var peer = new Discv4RawPeer();
                    peers.Add(peer);
                    await peer.BondAsync(target);
                }

                var answered = new List<bool>();
                foreach (var peer in peers) answered.Add(await AnswersFindNode(peer, target));

                Assert.Equal(new[] { false, false, true, true }, answered);
            }
            finally
            {
                foreach (var peer in peers) peer.Dispose();
            }
        }

        private static async Task<bool> AnswersFindNode(Discv4RawPeer peer, IPEndPoint target)
        {
            await peer.CollectAsync(TimeSpan.FromMilliseconds(100));
            var findNode = new Discv4FindNodeMessage { Target = new byte[64], Expiration = Discv4RawPeer.Future() };
            await peer.SendAsync(Discv4RawPeer.BuildPacket(peer.Key, Discv4MessageType.FindNode, Discv4MessageEncoder.EncodeFindNode(findNode)), target);
            var replies = await peer.CollectAsync(TimeSpan.FromMilliseconds(600));
            return replies.Any(r => r.Packet.Type == Discv4MessageType.Neighbors);
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(25);
        }
    }
}
