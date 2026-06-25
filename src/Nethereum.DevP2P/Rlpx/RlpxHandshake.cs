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

        /// <summary>
        /// Lower bound for the RLPx auth/ack ciphertext body length declared in the
        /// 2-byte size prefix. Legacy v4 plaintext is 165 bytes and EIP-8 auth packets
        /// add at least 100 bytes of random padding (RLPx spec, devp2p/rlpx.md),
        /// so anything below this is structurally malformed and rejected before
        /// the body allocation in <c>RlpxConnection.AcceptIncomingAsync</c> /
        /// <c>ConnectAsync</c>.
        /// </summary>
        public const int MinHandshakePacketSize = 100;

        /// <summary>
        /// Upper bound for the RLPx auth/ack ciphertext body length declared in the
        /// 2-byte size prefix. Maximum handshake packet size — an interop
        /// invariant, not spec-defined. Real EIP-8
        /// auth packets are ~300-600 bytes; legacy v4 is 307. The 2-byte size prefix
        /// allows up to 65 535 bytes for forward compatibility (devp2p/rlpx.md), but
        /// every production implementation caps lower to limit per-connection
        /// allocations during the unauthenticated handshake.
        /// </summary>
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

            var sigBytes = new byte[SignatureSize];
            var rPadded = sig.R.PadBytes(NonceSize);
            var sPadded = sig.S.PadBytes(NonceSize);
            Buffer.BlockCopy(rPadded, 0, sigBytes, 0, NonceSize);
            Buffer.BlockCopy(sPadded, 0, sigBytes, NonceSize, NonceSize);
            sigBytes[SignatureSize - 1] = (byte)(sig.V[0] - 27);

            var authBody = RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(sigBytes),
                RLP.RLP.EncodeElement(localKey.GetPubKeyNoPrefix()),
                RLP.RLP.EncodeElement(nonce),
                RLP.RLP.EncodeElement(new byte[] { AuthVersion })
            );

            var padding = RandomPadding();
            var authPlain = authBody.ConcatArrays(padding);

            var size = authPlain.Length + EciesEncryption.Overhead;
            var sizePrefix = new byte[] { (byte)(size >> 8), (byte)size };
            var encryptedBody = EciesEncryption.Encrypt(remotePubNoPrefix, authPlain, sizePrefix);

            var authPacket = ByteUtil.Merge(sizePrefix, encryptedBody);

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

        public static (byte[] ackPacket, HandshakeState state, RlpxSecrets secrets) HandleAuth(
            EthECKey localKey, byte[] authPacket)
        {
            if (authPacket == null || authPacket.Length < 2)
                throw new CryptographicException("RLPx auth packet shorter than 2-byte size prefix");

            var size = (authPacket[0] << 8) | authPacket[1];
            if (size < MinHandshakePacketSize || size > MaxHandshakePacketSize)
                throw new CryptographicException(
                    $"RLPx auth size {size} out of range [{MinHandshakePacketSize}, {MaxHandshakePacketSize}]");
            if (authPacket.Length < 2 + size)
                throw new CryptographicException(
                    $"RLPx auth packet length {authPacket.Length} shorter than declared size {2 + size}");

            var encryptedBody = authPacket.Slice(2, 2 + size);
            var sizeBytes = authPacket.Slice(0, 2);

            var authPlain = EciesEncryption.Decrypt(localKey.GetPrivateKeyAsBytes(), encryptedBody, sizeBytes);

            if (authPlain == null || authPlain.Length < SignatureSize)
                throw new CryptographicException(
                    "RLPx auth plaintext too short to contain the RLP envelope");

            var rlpLength = RLP.RLP.GetFirstElementLength(authPlain);
            var items = (RLP.RLPCollection)RLP.RLP.Decode(authPlain.Slice(0, rlpLength));

            var sigBytes = items[0].RLPData;
            var remotePubNoPrefix = items[1].RLPData;
            var remoteNonce = items[2].RLPData;

            var remoteKey = new EthECKey(remotePubNoPrefix, false, EthECKey.DEFAULT_PREFIX);
            var staticSharedSecret = localKey.CalculateCommonSecret(remoteKey);
            var signedData = staticSharedSecret.XOR(remoteNonce);

            var r = sigBytes.Slice(0, NonceSize);
            var s = sigBytes.Slice(NonceSize, NonceSize * 2);
            int recId = sigBytes[SignatureSize - 1];
            if (recId < 0 || recId > 3)
                throw new CryptographicException(
                    $"RLPx auth signature recovery id {recId} outside ECDSA range 0..3");

            var ethSig = EthECDSASignatureFactory.FromComponents(r, s);
            var remoteEphPub = EthECKey.RecoverFromSignature(ethSig, recId, signedData);
            var remoteEphPubNoPrefix = remoteEphPub.GetPubKeyNoPrefix();

            var ephemeral = EthECKey.GenerateKey();
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            var ackBody = RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(ephemeral.GetPubKeyNoPrefix()),
                RLP.RLP.EncodeElement(nonce),
                RLP.RLP.EncodeElement(new byte[] { AuthVersion })
            );

            var ackPlain = ackBody.ConcatArrays(RandomPadding());

            var ackSize = ackPlain.Length + EciesEncryption.Overhead;
            var ackSizePrefix = new byte[] { (byte)(ackSize >> 8), (byte)ackSize };
            var ackEncrypted = EciesEncryption.Encrypt(remotePubNoPrefix, ackPlain, ackSizePrefix);

            var ackPacket = ByteUtil.Merge(ackSizePrefix, ackEncrypted);

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

        public static RlpxSecrets HandleAck(HandshakeState initiatorState, byte[] ackPacket)
        {
            if (ackPacket == null || ackPacket.Length < 2)
                throw new CryptographicException("RLPx ack packet shorter than 2-byte size prefix");

            var size = (ackPacket[0] << 8) | ackPacket[1];
            if (size < MinHandshakePacketSize || size > MaxHandshakePacketSize)
                throw new CryptographicException(
                    $"RLPx ack size {size} out of range [{MinHandshakePacketSize}, {MaxHandshakePacketSize}]");
            if (ackPacket.Length < 2 + size)
                throw new CryptographicException(
                    $"RLPx ack packet length {ackPacket.Length} shorter than declared size {2 + size}");

            var encryptedBody = ackPacket.Slice(2, 2 + size);
            var sizeBytes = ackPacket.Slice(0, 2);

            var ackPlain = EciesEncryption.Decrypt(
                initiatorState.LocalKey.GetPrivateKeyAsBytes(), encryptedBody, sizeBytes);

            if (ackPlain == null || ackPlain.Length < SignatureSize)
                throw new CryptographicException(
                    "RLPx ack plaintext too short to contain the RLP envelope");

            var rlpLength = RLP.RLP.GetFirstElementLength(ackPlain);
            var items = (RLP.RLPCollection)RLP.RLP.Decode(ackPlain.Slice(0, rlpLength));

            initiatorState.RemoteEphemeralPubNoPrefix = items[0].RLPData;
            initiatorState.RemoteNonce = items[1].RLPData;
            initiatorState.AckPacket = ackPacket;

            return RlpxSecrets.Derive(
                initiatorState.EphemeralKey, initiatorState.RemoteEphemeralPubNoPrefix,
                initiatorState.Nonce, initiatorState.RemoteNonce,
                initiatorState.AuthPacket, ackPacket,
                isInitiator: true);
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
