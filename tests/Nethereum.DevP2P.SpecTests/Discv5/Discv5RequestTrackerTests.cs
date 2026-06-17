using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    public class Discv5RequestTrackerTests
    {
        private static byte[] FakeNodeId(byte fill)
        {
            var id = new byte[32];
            for (int i = 0; i < id.Length; i++) id[i] = fill;
            return id;
        }

        private static byte[] BuildSignedEnrEncoded(EthECKey key)
        {
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = System.Text.Encoding.ASCII.GetBytes("v4");
            EnrRecordSigner.Sign(enr, key);
            return EnrRecordEncoder.EncodeRecord(enr);
        }

        [Fact]
        public async Task Given_RegisteredPing_When_PongArrives_Then_TaskResolves()
        {
            using var tracker = new Discv5RequestTracker();
            var peer = FakeNodeId(0x11);
            var reqId = new byte[] { 0x01, 0x02, 0x03 };

            var task = tracker.RegisterPing(peer, reqId, TimeSpan.FromSeconds(2), CancellationToken.None);
            var pong = new Discv5PongMessage { RequestId = reqId, EnrSeq = 7 };
            var matched = tracker.CompletePong(peer, pong);

            Assert.True(matched);
            var result = await task;
            Assert.Equal(7ul, result.EnrSeq);
            Assert.Equal(0, tracker.PendingCount);
        }

        [Fact]
        public async Task Given_RegisteredPing_When_TimeoutElapses_Then_TaskIsCancelled()
        {
            using var tracker = new Discv5RequestTracker();
            var task = tracker.RegisterPing(
                FakeNodeId(0x22), new byte[] { 0x09 },
                TimeSpan.FromMilliseconds(50), CancellationToken.None);
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
            Assert.Equal(0, tracker.PendingCount);
        }

        [Fact]
        public async Task Given_FindNode_When_MultiPacketNodesArrive_Then_AllEnrsAccumulated()
        {
            using var tracker = new Discv5RequestTracker();
            var peer = FakeNodeId(0x33);
            var reqId = new byte[] { 0x42 };
            var k1 = EthECKey.GenerateKey();
            var k2 = EthECKey.GenerateKey();
            var k3 = EthECKey.GenerateKey();

            var task = tracker.RegisterFindNode(peer, reqId,
                expectedTotalHint: 2, TimeSpan.FromSeconds(2), CancellationToken.None);

            tracker.CompleteNodesChunk(peer, new Discv5NodesMessage
            {
                RequestId = reqId,
                Total = 2,
                Records = new List<byte[]> { BuildSignedEnrEncoded(k1), BuildSignedEnrEncoded(k2) }
            });
            // Still pending — only 1 of 2 chunks received.
            Assert.False(task.IsCompleted);

            tracker.CompleteNodesChunk(peer, new Discv5NodesMessage
            {
                RequestId = reqId,
                Total = 2,
                Records = new List<byte[]> { BuildSignedEnrEncoded(k3) }
            });

            var enrs = await task;
            Assert.Equal(3, enrs.Count);
            Assert.Equal(0, tracker.PendingCount);
        }

        [Fact]
        public async Task Given_FindNode_When_PeerClaimsAbsurdTotal_Then_ChunkCountCapped()
        {
            using var tracker = new Discv5RequestTracker();
            var peer = FakeNodeId(0x44);
            var reqId = new byte[] { 0x55 };
            var k = EthECKey.GenerateKey();
            var task = tracker.RegisterFindNode(peer, reqId,
                expectedTotalHint: 1, TimeSpan.FromSeconds(2), CancellationToken.None);

            // Peer claims 255 chunks (uint8 max) — tracker must cap at
            // MaxNodesChunksPerRequest and accept up to that many before
            // fulfilling the task.
            for (int i = 0; i < Discv5RequestTracker.MaxNodesChunksPerRequest; i++)
            {
                tracker.CompleteNodesChunk(peer, new Discv5NodesMessage
                {
                    RequestId = reqId,
                    Total = 255,
                    Records = new List<byte[]> { BuildSignedEnrEncoded(k) }
                });
            }

            var enrs = await task;
            Assert.True(enrs.Count <= Discv5RequestTracker.MaxNodesEnrsPerRequest);
            Assert.Equal(0, tracker.PendingCount);
        }

        [Fact]
        public void Given_UnsolicitedPong_When_Delivered_Then_TrackerIgnoresIt()
        {
            using var tracker = new Discv5RequestTracker();
            var matched = tracker.CompletePong(
                FakeNodeId(0x66),
                new Discv5PongMessage { RequestId = new byte[] { 0x77 } });
            Assert.False(matched);
        }
    }
}
