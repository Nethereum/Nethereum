using System;
using System.Net;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    /// <summary>
    /// Per discv5-wire.md §"Packet Encoding": min-packet-size 63 bytes,
    /// max-packet-size 1280 bytes. The session manager additionally rejects
    /// non-WHOAREYOU packets whose ciphertext is shorter than the 16-byte
    /// GCM tag plus a single plaintext byte. Undersized / oversized datagrams
    /// must be silently dropped without crashing the listener.
    /// </summary>
    public class Discv5ListenerSizeCheckTests
    {
        [Fact]
        public void Given_PacketBelowDecoderMinimum_When_Decoded_Then_ThrowsArgumentException()
        {
            // Anything below masking-iv(16) + static-header(23) = 39 cannot
            // possibly parse — the decoder rejects it immediately.
            var localKey = EthECKey.GenerateKey();
            var localNodeId = Discv5Crypto.ComputeNodeId(localKey.GetPubKeyNoPrefix());
            var tooShort = new byte[Discv5Packet.MaskingIvLength + Discv5Packet.HeaderStaticLength - 1];

            Assert.Throws<ArgumentException>(() => Discv5Packet.DecodePacket(tooShort, localNodeId));
        }

        [Fact]
        public void Discv5Packet_MinPacketSize_MatchesSpec()
        {
            // discv5-wire.md §"Packet Encoding" — 63-byte minimum (masking-iv
            // + static-header + minimum-WHOAREYOU-authdata 24 = 63).
            Assert.Equal(63, Discv5Packet.MinPacketSize);
            Assert.Equal(1280, Discv5Packet.MaxPacketSize);
        }

        [Fact]
        public void Given_NonWhoAreYouPacketWithSubMinimumPayload_When_Processed_Then_Ignored()
        {
            var localKey = EthECKey.GenerateKey();
            var mgr = new Discv5SessionManager(localKey);

            // Build an Ordinary packet with a 16-byte ciphertext (== GCM tag
            // length, zero plaintext bytes). The session manager must reject
            // it before invoking AES-GCM.
            var srcId = new byte[32];
            for (int i = 0; i < srcId.Length; i++) srcId[i] = (byte)(0xAA ^ i);
            var addr = new IPEndPoint(IPAddress.Parse("203.0.113.7"), 30303);

            var nonce = new byte[Discv5Packet.NonceLength];
            for (int i = 0; i < nonce.Length; i++) nonce[i] = (byte)(0x11 + i);
            var maskingIv = new byte[Discv5Packet.MaskingIvLength];
            for (int i = 0; i < maskingIv.Length; i++) maskingIv[i] = (byte)(0x33 + i);

            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.Ordinary,
                Nonce = nonce,
                AuthData = srcId
            };
            var trivialCiphertext = new byte[16]; // tag length only, no plaintext
            var packet = Discv5Packet.EncodePacket(maskingIv, header, mgr.LocalNodeId, trivialCiphertext);

            var result = mgr.Process(packet, addr);
            Assert.Equal(Discv5SessionManager.IncomingPacketKind.Ignored, result.Kind);
        }
    }
}
