using System;
using System.Security.Cryptography;
using Nethereum.DevP2P.Crypto;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Nethereum.Util;

namespace Nethereum.DevP2P.Rlpx
{
    public class HandshakeState
    {
        public EthECKey LocalKey { get; init; }
        public EthECKey EphemeralKey { get; init; }
        public byte[] Nonce { get; init; }
        public byte[] AuthPacket { get; internal set; }
        public byte[] AckPacket { get; internal set; }
        public byte[] RemoteEphemeralPubNoPrefix { get; internal set; }
        public byte[] RemoteNonce { get; internal set; }
        public byte[] RemotePubNoPrefix { get; init; }
        public bool IsInitiator { get; init; }
    }

    public static class RlpxHandshake
    {
        private const int NonceSize = 32;
        private const int SignatureSize = 65;
        private const int MinPadding = 100;
        private const int MaxPaddingRange = 100;
        private const byte AuthVersion = 4;

        private const int MinHandshakePlaintextSize = 65;

        public const int MinHandshakePacketSize = 100;

        public const int MaxHandshakePacketSize = 2048;

        public static (byte[] authPacket, HandshakeState state) CreateAuth(
            EthECKey localKey, byte[] remotePubNoPrefix)
        {
            var ephemeral = EthECKey.GenerateKey();
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            var remoteKey = new EthECKey(remotePubNoPrefix, false, EthECKey.DEFAULT_PREFIX);
            var staticSharedSecret = localKey.CalculateCommonSecret(remoteKey);
            var signedData = staticSharedSecret.XOR(nonce);
            var sig = ephemeral.SignAndCalculateV(signedData);
            var sigBytes = BuildAuthSignature(sig);

            var authBody = RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(sigBytes),
                RLP.RLP.EncodeElement(localKey.GetPubKeyNoPrefix()),
                RLP.RLP.EncodeElement(nonce),
                RLP.RLP.EncodeElement(new byte[] { AuthVersion })
            );

            var authPacket = SealHandshakePacket(authBody, remotePubNoPrefix);

            return (authPacket, new HandshakeState
            {
                LocalKey = localKey,
                EphemeralKey = ephemeral,
                Nonce = nonce,
                AuthPacket = authPacket,
                RemotePubNoPrefix = remotePubNoPrefix,
                IsInitiator = true
            });
        }

        private static byte[] BuildAuthSignature(EthECDSASignature sig)
        {
            var sigBytes = new byte[SignatureSize];
            var rPadded = sig.R.PadBytes(NonceSize);
            var sPadded = sig.S.PadBytes(NonceSize);
            Buffer.BlockCopy(rPadded, 0, sigBytes, 0, NonceSize);
            Buffer.BlockCopy(sPadded, 0, sigBytes, NonceSize, NonceSize);
            sigBytes[SignatureSize - 1] = (byte)(sig.V[0] - 27);
            return sigBytes;
        }

        public static (byte[] ackPacket, HandshakeState state, RlpxSecrets secrets) HandleAuth(
            EthECKey localKey, byte[] authPacket)
        {
            var (encryptedBody, sizeBytes) = ValidateAndSliceHandshakePacket(authPacket, "auth");
            var items = DecryptAndDecodeHandshakeBody(
                localKey.GetPrivateKeyAsBytes(), encryptedBody, sizeBytes, "auth");

            var sigBytes = items[0].RLPData;
            var remotePubNoPrefix = items[1].RLPData;
            var remoteNonce = items[2].RLPData;

            var remoteKey = new EthECKey(remotePubNoPrefix, false, EthECKey.DEFAULT_PREFIX);
            var staticSharedSecret = localKey.CalculateCommonSecret(remoteKey);
            var signedData = staticSharedSecret.XOR(remoteNonce);
            var remoteEphPubNoPrefix = RecoverRemoteEphemeralKey(sigBytes, signedData);

            var ephemeral = EthECKey.GenerateKey();
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            var ackBody = RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(ephemeral.GetPubKeyNoPrefix()),
                RLP.RLP.EncodeElement(nonce),
                RLP.RLP.EncodeElement(new byte[] { AuthVersion })
            );

            var ackPacket = SealHandshakePacket(ackBody, remotePubNoPrefix);

            var secrets = RlpxSecrets.Derive(
                ephemeral, remoteEphPubNoPrefix,
                remoteNonce, nonce,
                authPacket, ackPacket,
                isInitiator: false);

            return (ackPacket, new HandshakeState
            {
                LocalKey = localKey,
                EphemeralKey = ephemeral,
                Nonce = nonce,
                AuthPacket = authPacket,
                AckPacket = ackPacket,
                RemoteEphemeralPubNoPrefix = remoteEphPubNoPrefix,
                RemoteNonce = remoteNonce,
                RemotePubNoPrefix = remotePubNoPrefix,
                IsInitiator = false
            }, secrets);
        }

        private static byte[] RecoverRemoteEphemeralKey(byte[] sigBytes, byte[] signedData)
        {
            var r = sigBytes.Slice(0, NonceSize);
            var s = sigBytes.Slice(NonceSize, NonceSize * 2);
            int recId = sigBytes[SignatureSize - 1];
            if (recId < 0 || recId > 3)
                throw new CryptographicException(
                    $"RLPx auth signature recovery id {recId} outside ECDSA range 0..3");

            var ethSig = EthECDSASignatureFactory.FromComponents(r, s);
            var remoteEphPub = EthECKey.RecoverFromSignature(ethSig, recId, signedData);
            return remoteEphPub.GetPubKeyNoPrefix();
        }

        public static RlpxSecrets HandleAck(HandshakeState initiatorState, byte[] ackPacket)
        {
            var (encryptedBody, sizeBytes) = ValidateAndSliceHandshakePacket(ackPacket, "ack");
            var items = DecryptAndDecodeHandshakeBody(
                initiatorState.LocalKey.GetPrivateKeyAsBytes(), encryptedBody, sizeBytes, "ack");

            var remoteEphemeralPubNoPrefix = items[0].RLPData;
            var remoteNonce = items[1].RLPData;

            initiatorState.RemoteEphemeralPubNoPrefix = remoteEphemeralPubNoPrefix;
            initiatorState.RemoteNonce = remoteNonce;
            initiatorState.AckPacket = ackPacket;

            return RlpxSecrets.Derive(
                initiatorState.EphemeralKey, initiatorState.RemoteEphemeralPubNoPrefix,
                initiatorState.Nonce, initiatorState.RemoteNonce,
                initiatorState.AuthPacket, ackPacket,
                isInitiator: true);
        }

        private static (byte[] encryptedBody, byte[] sizeBytes) ValidateAndSliceHandshakePacket(
            byte[] packet, string packetLabel)
        {
            if (packet == null || packet.Length < 2)
                throw new CryptographicException($"RLPx {packetLabel} packet shorter than 2-byte size prefix");

            var size = (packet[0] << 8) | packet[1];
            if (size < MinHandshakePacketSize || size > MaxHandshakePacketSize)
                throw new CryptographicException(
                    $"RLPx {packetLabel} size {size} out of range [{MinHandshakePacketSize}, {MaxHandshakePacketSize}]");
            if (packet.Length < 2 + size)
                throw new CryptographicException(
                    $"RLPx {packetLabel} packet length {packet.Length} shorter than declared size {2 + size}");

            var encryptedBody = packet.Slice(2, 2 + size);
            var sizeBytes = packet.Slice(0, 2);
            return (encryptedBody, sizeBytes);
        }

        private static RLP.RLPCollection DecryptAndDecodeHandshakeBody(
            byte[] privateKey, byte[] encryptedBody, byte[] sizeBytes, string packetLabel)
        {
            var plain = EciesEncryption.Decrypt(privateKey, encryptedBody, sizeBytes);

            if (plain == null || plain.Length < MinHandshakePlaintextSize)
                throw new CryptographicException(
                    $"RLPx {packetLabel} plaintext too short to contain the RLP envelope");

            var rlpLength = RLP.RLP.GetFirstElementLength(plain);
            return (RLP.RLPCollection)RLP.RLP.Decode(plain.Slice(0, rlpLength));
        }

        private static byte[] SealHandshakePacket(byte[] body, byte[] remotePubNoPrefix)
        {
            var plain = body.ConcatArrays(RandomPadding());

            var size = plain.Length + EciesEncryption.Overhead;
            var sizePrefix = new byte[] { (byte)(size >> 8), (byte)size };
            var encryptedBody = EciesEncryption.Encrypt(remotePubNoPrefix, plain, sizePrefix);

            return ByteUtil.Merge(sizePrefix, encryptedBody);
        }

        private static byte[] RandomPadding()
        {
            var buf = new byte[1];
            RandomNumberGenerator.Fill(buf);
            var padding = new byte[MinPadding + (buf[0] % MaxPaddingRange)];
            RandomNumberGenerator.Fill(padding);
            return padding;
        }
    }
}
