using System;
using System.Security.Cryptography;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    public class RlpxHandshakeSizeBoundTests
    {
        [Fact]
        public void Given_AuthSize0xFFFFButTruncatedBody_When_HandleAuth_Then_RejectsBeforeAllocation()
        {
            var localKey = EthECKey.GenerateKey();

            var packet = new byte[2 + 16];
            packet[0] = 0xFF;
            packet[1] = 0xFF;

            var ex = Assert.Throws<CryptographicException>(
                () => RlpxHandshake.HandleAuth(localKey, packet));
            Assert.Contains("out of range", ex.Message);
            Assert.Contains("65535", ex.Message);
        }

        [Fact]
        public void Given_AuthSizeBelowMinimum_When_HandleAuth_Then_Rejects()
        {
            var localKey = EthECKey.GenerateKey();

            var packet = new byte[2 + 64];
            packet[0] = 0x00;
            packet[1] = 0x01;

            var ex = Assert.Throws<CryptographicException>(
                () => RlpxHandshake.HandleAuth(localKey, packet));
            Assert.Contains("out of range", ex.Message);
        }

        [Fact]
        public void Given_AuthSizeJustAboveCap_When_HandleAuth_Then_Rejects()
        {
            var localKey = EthECKey.GenerateKey();

            var packet = new byte[2 + 2049];
            packet[0] = 0x08;
            packet[1] = 0x01;

            var ex = Assert.Throws<CryptographicException>(
                () => RlpxHandshake.HandleAuth(localKey, packet));
            Assert.Contains("out of range", ex.Message);
            Assert.Contains("2049", ex.Message);
        }

        [Fact]
        public void Given_AuthSizeAtCap_When_HandleAuth_Then_PassesSizeCheck()
        {
            var localKey = EthECKey.GenerateKey();

            var packet = new byte[2 + 2048];
            packet[0] = 0x08;
            packet[1] = 0x00;

            var ex = Assert.ThrowsAny<Exception>(
                () => RlpxHandshake.HandleAuth(localKey, packet));
            Assert.DoesNotContain("out of range", ex.Message ?? string.Empty);
        }

        [Fact]
        public void Given_AckSize0xFFFFButTruncatedBody_When_HandleAck_Then_RejectsBeforeAllocation()
        {
            var localKey = EthECKey.GenerateKey();
            var state = new HandshakeState
            {
                LocalKey = localKey,
                EphemeralKey = EthECKey.GenerateKey(),
                Nonce = new byte[32],
                RemotePubNoPrefix = EthECKey.GenerateKey().GetPubKeyNoPrefix(),
                IsInitiator = true
            };

            var packet = new byte[2 + 16];
            packet[0] = 0xFF;
            packet[1] = 0xFF;

            var ex = Assert.Throws<CryptographicException>(
                () => RlpxHandshake.HandleAck(state, packet));
            Assert.Contains("out of range", ex.Message);
            Assert.Contains("ack", ex.Message);
        }

        [Fact]
        public void Given_HandshakePacketSizeConstants_Then_MatchGethInteropInvariant()
        {
            Assert.Equal(100, RlpxHandshake.MinHandshakePacketSize);
            Assert.Equal(2048, RlpxHandshake.MaxHandshakePacketSize);
        }

        [Fact]
        public void Given_RealEip8AuthPacket_When_HandleAuth_Then_StillWithinBounds()
        {
            var keyA = EthECKey.GenerateKey();
            var keyB = EthECKey.GenerateKey();

            var (authPacket, _) = RlpxHandshake.CreateAuth(keyA, keyB.GetPubKeyNoPrefix());

            var size = (authPacket[0] << 8) | authPacket[1];
            Assert.InRange(size, RlpxHandshake.MinHandshakePacketSize, RlpxHandshake.MaxHandshakePacketSize);

            var (_, _, _) = RlpxHandshake.HandleAuth(keyB, authPacket);
        }
    }
}
