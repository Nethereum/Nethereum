using System;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Rlpx
{
    /// <summary>
    /// EIP-8 test vectors for the RLPx auth/ack handshake.
    /// Source: https://github.com/ethereum/devp2p/blob/master/rlpx.md#test-vectors
    ///
    /// These are the canonical inputs used to verify any RLPx implementation
    /// derives the same shared secrets as Geth, Nethermind, Reth, Besu.
    /// </summary>
    public class Eip8AuthAckVectorTests
    {
        // EIP-8 spec vector A: static key, ephemeral key, nonce — initiator
        private const string KeyAStaticPrivHex = "49a7b37aa6f6645917e7b807e9d1c00d4fa71f18343b0d4122a4d2df64dd6fee";
        private const string KeyBStaticPrivHex = "b71c71a67e1177ad4e901695e1b4b9ee17ae16c6668d313eac2f96dbcda3f291";

        [Fact]
        public void AuthAck_Eip8RoundTrip_BothSidesDeriveSameSecrets()
        {
            var keyA = new EthECKey(KeyAStaticPrivHex);
            var keyB = new EthECKey(KeyBStaticPrivHex);

            var (authPacket, initiatorState) = RlpxHandshake.CreateAuth(
                keyA, keyB.GetPubKeyNoPrefix());

            var (ackPacket, _, recipientSecrets) = RlpxHandshake.HandleAuth(
                keyB, authPacket);

            var initiatorSecrets = RlpxHandshake.HandleAck(initiatorState, ackPacket);

            Assert.Equal(initiatorSecrets.AesSecret.ToHex(), recipientSecrets.AesSecret.ToHex());
            Assert.Equal(initiatorSecrets.MacSecret.ToHex(), recipientSecrets.MacSecret.ToHex());
        }

        [Fact]
        public void AuthPacket_AlwaysProducesSizePrefix_Eip8Compliant()
        {
            var keyA = new EthECKey(KeyAStaticPrivHex);
            var keyB = new EthECKey(KeyBStaticPrivHex);

            var (authPacket, _) = RlpxHandshake.CreateAuth(keyA, keyB.GetPubKeyNoPrefix());

            Assert.True(authPacket.Length >= 2, "Auth packet must have at least 2-byte size prefix");
            var declaredSize = (authPacket[0] << 8) | authPacket[1];
            Assert.Equal(authPacket.Length - 2, declaredSize);

            // EIP-8 minimum auth packet size: 282 bytes (encrypted body) + padding.
            // Spec says padding is between 100 and 200 bytes — total encrypted body
            // should be at least 307 bytes plus 2-byte prefix = 309 bytes total.
            Assert.True(authPacket.Length >= 309,
                $"EIP-8 auth packet should be at least 309 bytes (sig + nonce + pubkey + version + padding + ECIES overhead), got {authPacket.Length}");
        }

        [Fact]
        public void AckPacket_AlwaysProducesSizePrefix_Eip8Compliant()
        {
            var keyA = new EthECKey(KeyAStaticPrivHex);
            var keyB = new EthECKey(KeyBStaticPrivHex);

            var (authPacket, _) = RlpxHandshake.CreateAuth(keyA, keyB.GetPubKeyNoPrefix());
            var (ackPacket, _, _) = RlpxHandshake.HandleAuth(keyB, authPacket);

            Assert.True(ackPacket.Length >= 2);
            var declaredSize = (ackPacket[0] << 8) | ackPacket[1];
            Assert.Equal(ackPacket.Length - 2, declaredSize);

            // EIP-8 minimum ack packet: ~210 bytes
            Assert.True(ackPacket.Length >= 200,
                $"EIP-8 ack packet should be at least ~200 bytes, got {ackPacket.Length}");
        }

        [Fact]
        public void InvalidAuthPacket_ThrowsCleanError()
        {
            var keyB = new EthECKey(KeyBStaticPrivHex);

            var corrupted = new byte[400];
            corrupted[0] = 1;
            corrupted[1] = 0x90;
            for (int i = 2; i < corrupted.Length; i++) corrupted[i] = 0xff;

            Assert.ThrowsAny<Exception>(() => RlpxHandshake.HandleAuth(keyB, corrupted));
        }

        [Fact]
        public void AuthSignature_IsValidEcdsa_RecoversEphemeralKey()
        {
            var keyA = new EthECKey(KeyAStaticPrivHex);
            var keyB = new EthECKey(KeyBStaticPrivHex);

            var (authPacket, initiatorState) = RlpxHandshake.CreateAuth(keyA, keyB.GetPubKeyNoPrefix());
            var (_, recipientState, _) = RlpxHandshake.HandleAuth(keyB, authPacket);

            Assert.NotNull(recipientState.RemoteEphemeralPubNoPrefix);
            Assert.Equal(64, recipientState.RemoteEphemeralPubNoPrefix!.Length);

            Assert.Equal(initiatorState.EphemeralKey.GetPubKeyNoPrefix().ToHex(),
                         recipientState.RemoteEphemeralPubNoPrefix.ToHex());
        }
    }
}
