using System;
using System.Net;
using System.Reflection;
using System.Threading;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    /// <summary>
    /// Per discv5-wire.md §"Sessions": an unanswered WHOAREYOU challenge must
    /// be GC'd after the handshake timeout. Without expiry, a peer that
    /// triggers fresh challenges without ever completing the handshake can
    /// exhaust the responder's pending-challenge dictionary.
    /// </summary>
    public class Discv5SessionManagerHandshakeTimeoutTests
    {
        [Fact]
        public void Given_StalePendingChallenge_When_SweepRuns_Then_ChallengeEvicted()
        {
            var localKey = EthECKey.GenerateKey();
            var mgr = new Discv5SessionManager(localKey);

            var peerId = new byte[32];
            for (int i = 0; i < peerId.Length; i++) peerId[i] = (byte)(0xAA ^ i);
            var peerAddr = new IPEndPoint(IPAddress.Parse("203.0.113.7"), 30303);

            SendUndecryptableOrdinary(mgr, peerId, peerAddr);
            Assert.Equal(1, mgr.PendingChallengeCount);

            // Backdate the only pending challenge so it is older than the handshake timeout.
            BackdateAllPendingChallenges(mgr, TimeSpan.FromSeconds(5));

            mgr.SweepStaleChallenges();
            Assert.Equal(0, mgr.PendingChallengeCount);
        }

        [Fact]
        public void Given_FreshPendingChallenge_When_SweepRuns_Then_ChallengePreserved()
        {
            var localKey = EthECKey.GenerateKey();
            var mgr = new Discv5SessionManager(localKey);

            var peerId = new byte[32];
            for (int i = 0; i < peerId.Length; i++) peerId[i] = (byte)(0xAA ^ i);
            var peerAddr = new IPEndPoint(IPAddress.Parse("203.0.113.7"), 30303);

            SendUndecryptableOrdinary(mgr, peerId, peerAddr);
            mgr.SweepStaleChallenges();
            Assert.Equal(1, mgr.PendingChallengeCount);
        }

        private static void SendUndecryptableOrdinary(
            Discv5SessionManager mgr, byte[] srcId, IPEndPoint fromAddr)
        {
            var nonce = new byte[Discv5Packet.NonceLength];
            for (int i = 0; i < nonce.Length; i++) nonce[i] = (byte)(0x11 + i);
            var maskingIv = new byte[Discv5Packet.MaskingIvLength];
            for (int i = 0; i < maskingIv.Length; i++) maskingIv[i] = (byte)(0x33 + i);
            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.Ordinary,
                Nonce = nonce,
                AuthData = srcId,
            };
            var junk = new byte[32];
            for (int i = 0; i < junk.Length; i++) junk[i] = 0xCC;
            var packet = Discv5Packet.EncodePacket(maskingIv, header, mgr.LocalNodeId, junk);
            mgr.Process(packet, fromAddr);
        }

        private static void BackdateAllPendingChallenges(Discv5SessionManager mgr, TimeSpan ageDelta)
        {
            var field = typeof(Discv5SessionManager).GetField(
                "_pendingChallenges", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)field.GetValue(mgr);
            foreach (System.Collections.DictionaryEntry kvp in dict)
            {
                var pending = (Discv5PendingChallenge)kvp.Value;
                pending.CreatedUtc = DateTime.UtcNow - ageDelta;
            }
        }
    }
}
