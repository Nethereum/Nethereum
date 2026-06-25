using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    /// <summary>
    /// End-to-end coverage for the discv5 initiator surface — pairs of
    /// <see cref="Discv5Listener"/> instances dialled across loopback.
    /// Receiver-path tests live in <c>Discv5ListenerPassiveTests</c> and the
    /// devp2p conformance suite.
    /// </summary>
    public class Discv5InitiatorTests
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

        [Fact]
        [Trait("Category", "Discv5-Initiator")]
        public async Task Given_TwoListeners_When_AInitiatesPing_Then_PongReturnsAndSessionEstablishes()
        {
            await using var a = StartListener(out var aEnr, out _);
            await using var b = StartListener(out var bEnr, out _);

            var bEndpoint = new IPEndPoint(IPAddress.Loopback, b.Port);
            var bNodeId = Discv5Crypto.ComputeNodeId(bEnr.Secp256k1);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var pong = await a.SendPingAsync(bEndpoint, bNodeId, bEnr.Secp256k1, RequestTimeout, cts.Token);

            Assert.NotNull(pong);
            Assert.Equal(b.LocalEnrSequence, pong.EnrSeq);
            Assert.NotNull(pong.RecipientIp);
            Assert.True(pong.RecipientIp.Length is 4 or 16);
            Assert.True(pong.RecipientPort > 0);
        }

        [Fact]
        [Trait("Category", "Discv5-Initiator")]
        public async Task Given_PeerWithSeededRoutingTable_When_FindNodeAtDistance256_Then_RecordsReturned()
        {
            await using var a = StartListener(out _, out _);
            await using var b = StartListener(out var bEnr, out var bKey);

            // Seed B's routing table with a synthetic peer whose nodeId is
            // log-distance 256 from B. Probability is ~99% but we search
            // deterministically to avoid flake.
            var (syntheticNodeId, syntheticEnrEncoded) = SeedRoutingTableEntry(b, distanceFromLocal: 256);

            var bEndpoint = new IPEndPoint(IPAddress.Loopback, b.Port);
            var bNodeId = Discv5Crypto.ComputeNodeId(bEnr.Secp256k1);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var enrs = await a.SendFindNodeAsync(
                bEndpoint, bNodeId, bEnr.Secp256k1,
                new uint[] { 256 }, RequestTimeout, cts.Token);

            Assert.NotEmpty(enrs);
            Assert.Contains(enrs, e =>
                e.Secp256k1 != null &&
                Nethereum.Util.ByteUtil.AreEqual(
                    Discv5Crypto.ComputeNodeId(e.Secp256k1), syntheticNodeId));
        }

        [Fact]
        [Trait("Category", "Discv5-Initiator")]
        public async Task Given_EstablishedSession_When_SecondPingFires_Then_NoFreshHandshakeOccurs()
        {
            await using var a = StartListener(out _, out _);
            await using var b = StartListener(out var bEnr, out _);

            var bEndpoint = new IPEndPoint(IPAddress.Loopback, b.Port);
            var bNodeId = Discv5Crypto.ComputeNodeId(bEnr.Secp256k1);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // First ping triggers WHOAREYOU handshake.
            await a.SendPingAsync(bEndpoint, bNodeId, bEnr.Secp256k1, RequestTimeout, cts.Token);

            // The reciprocal-ping that B fires back after handshake also
            // establishes B->A. Let everything settle.
            await Task.Delay(250, cts.Token);

            var aChallengesBefore = GetPendingChallengeCount(a);
            var bChallengesBefore = GetPendingChallengeCount(b);

            // Second ping must reuse the session — no new pending challenges
            // should appear on either side. We measure across the round-trip.
            var pong = await a.SendPingAsync(bEndpoint, bNodeId, bEnr.Secp256k1, RequestTimeout, cts.Token);
            Assert.NotNull(pong);

            // Allow GC sweep cadence to settle any transient state from the
            // reciprocal ping that happened on the first call.
            await Task.Delay(50, cts.Token);

            var aChallengesAfter = GetPendingChallengeCount(a);
            var bChallengesAfter = GetPendingChallengeCount(b);

            // Session-resume: a fresh handshake would have left exactly one
            // pending challenge on either side until garbage-collected.
            Assert.Equal(aChallengesBefore, aChallengesAfter);
            Assert.Equal(bChallengesBefore, bChallengesAfter);
        }

        [Fact]
        [Trait("Category", "Discv5-Initiator")]
        public async Task Given_FirstPing_When_NoExistingSession_Then_WhoAreYouHandshakeRunsThenPongReturns()
        {
            // Mirrors geth's session.go test for cold-dial → WHOAREYOU → handshake
            // → re-encrypt original message. Asserts the in-flight WHOAREYOU path
            // is exercised on the *first* ping (no prior session), and the
            // request still completes successfully.
            await using var a = StartListener(out _, out _);
            await using var b = StartListener(out var bEnr, out _);

            var bEndpoint = new IPEndPoint(IPAddress.Loopback, b.Port);
            var bNodeId = Discv5Crypto.ComputeNodeId(bEnr.Secp256k1);

            // A starts with zero sessions. The cold-dial path goes through
            // BuildInitialOrdinaryPacket → B replies WHOAREYOU → A handshakes
            // → A re-emits the original ping under the new key.
            Assert.Equal(0, GetSessionCount(a));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var pong = await a.SendPingAsync(bEndpoint, bNodeId, bEnr.Secp256k1, RequestTimeout, cts.Token);

            Assert.NotNull(pong);
            // After WHOAREYOU + handshake, A must hold exactly one established session.
            Assert.Equal(1, GetSessionCount(a));
        }

        [Fact]
        [Trait("Category", "Discv5-Initiator")]
        public async Task Given_BHasTalkHandlerForTestProtocol_When_AIssuesTalkRequest_Then_ResponseReturned()
        {
            await using var a = StartListener(out _, out _);
            await using var b = StartListener(out var bEnr, out _);

            var payload = Encoding.UTF8.GetBytes("hello");
            var canned = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

            byte[] seenPayload = null;
            b.RegisterTalkHandler("test", (req, _) =>
            {
                seenPayload = req;
                return canned;
            });

            var bEndpoint = new IPEndPoint(IPAddress.Loopback, b.Port);
            var bNodeId = Discv5Crypto.ComputeNodeId(bEnr.Secp256k1);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await a.SendTalkRequestAsync(
                bEndpoint, bNodeId, bEnr.Secp256k1,
                Encoding.ASCII.GetBytes("test"),
                payload,
                RequestTimeout, cts.Token);

            Assert.Equal(canned, response);
            Assert.NotNull(seenPayload);
            Assert.Equal(payload, seenPayload);
        }

        [Fact]
        [Trait("Category", "Discv5-Initiator")]
        public async Task Given_BlackHoleEndpoint_When_PingIssued_Then_TimeoutSurfaces()
        {
            await using var a = StartListener(out _, out _);

            // RFC 5737 TEST-NET-1 — packets to 192.0.2.x are guaranteed to
            // never reach a real peer.
            var blackHole = new IPEndPoint(IPAddress.Parse("192.0.2.1"), 30303);
            var fakeNodeId = new byte[32];
            for (int i = 0; i < fakeNodeId.Length; i++) fakeNodeId[i] = (byte)i;
            var fakePub = EthECKey.GenerateKey().GetPubKey(compresseed: true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await a.SendPingAsync(blackHole, fakeNodeId, fakePub,
                    TimeSpan.FromMilliseconds(300), cts.Token);
            });
        }

        private static Discv5Listener StartListener(out EnrRecord enr, out EthECKey key)
        {
            key = EthECKey.GenerateKey();
            var listener = new Discv5Listener(key);
            listener.Start(IPAddress.Loopback, port: 0);
            enr = BuildSignedEnr(key, IPAddress.Loopback,
                (ushort)listener.Port, (ushort)listener.Port);
            listener.LocalEnrEncoded = EnrRecordEncoder.EncodeRecord(enr);
            listener.LocalEnrSequence = enr.Sequence;
            return listener;
        }

        private static (byte[] nodeId, byte[] encoded) SeedRoutingTableEntry(
            Discv5Listener target, uint distanceFromLocal)
        {
            var targetId = target.NodeId;
            for (int attempt = 0; attempt < 200; attempt++)
            {
                var candidate = EthECKey.GenerateKey();
                var candidateId = Discv5Crypto.ComputeNodeId(candidate.GetPubKey(compresseed: true));
                if (Discv5RoutingTable.LogDistance(targetId, candidateId) == distanceFromLocal)
                {
                    var ip = new IPAddress(new byte[] { 203, 0, 113, (byte)(attempt + 1) });
                    const ushort port = 30444;
                    var enr = BuildSignedEnr(candidate, ip, port, port);
                    var encoded = EnrRecordEncoder.EncodeRecord(enr);
                    target.Routing.Upsert(new Discv5RoutingTable.Entry
                    {
                        NodeId = candidateId,
                        Address = new IPEndPoint(ip, port),
                        EnrEncoded = encoded,
                    });
                    return (candidateId, encoded);
                }
            }
            throw new InvalidOperationException(
                $"Could not generate a key at log-distance {distanceFromLocal} after 200 attempts");
        }

        private static EnrRecord BuildSignedEnr(EthECKey key, IPAddress ip, ushort tcp, ushort udp)
        {
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
            enr.Pairs["ip"] = ip.GetAddressBytes();
            enr.Pairs["tcp"] = new[] { (byte)((tcp >> 8) & 0xff), (byte)(tcp & 0xff) };
            enr.Pairs["udp"] = new[] { (byte)((udp >> 8) & 0xff), (byte)(udp & 0xff) };
            enr.Pairs["secp256k1"] = key.GetPubKey(compresseed: true);
            EnrRecordSigner.Sign(enr, key);
            return enr;
        }

        private static int GetPendingChallengeCount(Discv5Listener listener)
            => GetSessionManager(listener).PendingChallengeCount;

        private static int GetSessionCount(Discv5Listener listener)
            => GetSessionManager(listener).SessionCount;

        private static Discv5SessionManager GetSessionManager(Discv5Listener listener)
        {
            // SessionManager state is private on the listener; reflection is
            // confined to this test-scoped helper so we can observe handshake
            // outcomes without widening the public API.
            var smField = typeof(Discv5Listener).GetField("_sessionManager",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (Discv5SessionManager)smField.GetValue(listener);
        }
    }
}
