using System;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Nethereum.Util;

namespace Nethereum.DevP2P.Discv4
{
    /// <summary>
    /// discv4 packet wire format: [hash(32) || signature(65) || type(1) || data]
    /// hash = keccak256(signature || type || data)
    /// signature = secp256k1 ECDSA over keccak256(type || data)
    /// </summary>
    public static class Discv4Packet
    {
        public const int HashLength = 32;
        public const int SignatureLength = 65;
        public const int HeaderLength = HashLength + SignatureLength + 1;
        public const int MaxPacketSize = 1280;

        public static byte[] Encode(EthECKey localKey, Discv4MessageType type, byte[] data)
        {
            var keccak = new Sha3Keccack();

            var typeAndData = new byte[1 + data.Length];
            typeAndData[0] = (byte)type;
            Buffer.BlockCopy(data, 0, typeAndData, 1, data.Length);

            var sigDigest = keccak.CalculateHash(typeAndData);
            var sig = localKey.SignAndCalculateV(sigDigest);

            var sigBytes = new byte[SignatureLength];
            var rPadded = sig.R.PadBytes(32);
            var sPadded = sig.S.PadBytes(32);
            Buffer.BlockCopy(rPadded, 0, sigBytes, 0, 32);
            Buffer.BlockCopy(sPadded, 0, sigBytes, 32, 32);
            sigBytes[SignatureLength - 1] = (byte)(sig.V[0] - 27);

            var hashInput = new byte[SignatureLength + typeAndData.Length];
            Buffer.BlockCopy(sigBytes, 0, hashInput, 0, SignatureLength);
            Buffer.BlockCopy(typeAndData, 0, hashInput, SignatureLength, typeAndData.Length);
            var hash = keccak.CalculateHash(hashInput);

            var packet = new byte[HashLength + SignatureLength + typeAndData.Length];
            Buffer.BlockCopy(hash, 0, packet, 0, HashLength);
            Buffer.BlockCopy(sigBytes, 0, packet, HashLength, SignatureLength);
            Buffer.BlockCopy(typeAndData, 0, packet, HashLength + SignatureLength, typeAndData.Length);

            if (packet.Length > MaxPacketSize)
                throw new InvalidOperationException(
                    $"Discv4 packet size {packet.Length} exceeds maximum {MaxPacketSize}");

            return packet;
        }

        public static DecodedPacket Decode(byte[] packet)
        {
            if (packet == null || packet.Length < HeaderLength + 1)
                throw new ArgumentException("Discv4 packet too short");

            var keccak = new Sha3Keccack();

            var receivedHash = new byte[HashLength];
            Buffer.BlockCopy(packet, 0, receivedHash, 0, HashLength);

            var signedPortion = new byte[packet.Length - HashLength];
            Buffer.BlockCopy(packet, HashLength, signedPortion, 0, signedPortion.Length);
            var computedHash = keccak.CalculateHash(signedPortion);

            if (!ByteUtil.AreEqual(receivedHash, computedHash))
                throw new InvalidOperationException("Discv4 packet hash mismatch");

            var sigBytes = new byte[SignatureLength];
            Buffer.BlockCopy(packet, HashLength, sigBytes, 0, SignatureLength);

            var type = (Discv4MessageType)packet[HashLength + SignatureLength];

            var data = new byte[packet.Length - HeaderLength];
            Buffer.BlockCopy(packet, HeaderLength, data, 0, data.Length);

            var typeAndData = new byte[1 + data.Length];
            typeAndData[0] = (byte)type;
            Buffer.BlockCopy(data, 0, typeAndData, 1, data.Length);
            var sigDigest = keccak.CalculateHash(typeAndData);

            byte[] r = new byte[32];
            byte[] s = new byte[32];
            Buffer.BlockCopy(sigBytes, 0, r, 0, 32);
            Buffer.BlockCopy(sigBytes, 32, s, 0, 32);
            var v = new[] { (byte)(sigBytes[64] + 27) };
            var sig = EthECDSASignatureFactory.FromComponents(r, s, v);

            var senderPubKey = EthECKey.RecoverFromSignature(sig, sigDigest);
            var senderPubNoPrefix = senderPubKey.GetPubKeyNoPrefix();

            return new DecodedPacket
            {
                Hash = receivedHash,
                Type = type,
                Data = data,
                SenderPubKey = senderPubNoPrefix
            };
        }

        public class DecodedPacket
        {
            public byte[] Hash { get; set; }
            public Discv4MessageType Type { get; set; }
            public byte[] Data { get; set; }
            public byte[] SenderPubKey { get; set; }
        }
    }
}
