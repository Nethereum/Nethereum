using System;
using System.Security.Cryptography;

namespace Nethereum.DevP2P.Discv5
{
    public static class Discv5Packet
    {
        public const int MaskingIvLength = 16;
        public const int HeaderStaticLength = 23;
        public const int NonceLength = 12;
        public static readonly byte[] ProtocolId = new byte[] { (byte)'d', (byte)'i', (byte)'s', (byte)'c', (byte)'v', (byte)'5' };
        public const ushort Version = 0x0001;

        public const int MinPacketSize = 63;

        public const int MinMessagePayloadSize = 48;

        public const int MaxPacketSize = 1280;

        public enum PacketFlag : byte
        {
            Ordinary = 0,
            WhoAreYou = 1,
            Handshake = 2
        }

        public class Header
        {
            public PacketFlag Flag { get; set; }
            public byte[] Nonce { get; set; } = new byte[NonceLength];
            public byte[] AuthData { get; set; } = Array.Empty<byte>();
        }

        public static byte[] EncodePacket(byte[] maskingIv, Header header, byte[] destNodeId, byte[] encryptedMessage)
        {
            if (maskingIv == null || maskingIv.Length != MaskingIvLength)
                throw new ArgumentException($"masking-iv must be exactly {MaskingIvLength} bytes");
            if (destNodeId == null || destNodeId.Length < 16)
                throw new ArgumentException("destination node-id must be at least 16 bytes");
            if (header.Nonce == null || header.Nonce.Length != NonceLength)
                throw new ArgumentException($"nonce must be exactly {NonceLength} bytes");

            var rawHeader = BuildRawHeader(header);
            var maskKey = new byte[16];
            Buffer.BlockCopy(destNodeId, 0, maskKey, 0, 16);
            var maskedHeader = AesCtrTransform(maskKey, maskingIv, rawHeader);

            var encryptedMsg = encryptedMessage ?? Array.Empty<byte>();
            var result = new byte[MaskingIvLength + maskedHeader.Length + encryptedMsg.Length];
            Buffer.BlockCopy(maskingIv, 0, result, 0, MaskingIvLength);
            Buffer.BlockCopy(maskedHeader, 0, result, MaskingIvLength, maskedHeader.Length);
            Buffer.BlockCopy(encryptedMsg, 0, result, MaskingIvLength + maskedHeader.Length, encryptedMsg.Length);
            return result;
        }

        public static (byte[] maskingIv, Header header, byte[] encryptedMessage, byte[] rawHeaderForAad) DecodePacket(
            byte[] packet, byte[] localNodeId)
        {
            if (packet == null || packet.Length < MaskingIvLength + HeaderStaticLength)
                throw new ArgumentException("packet too short");
            if (localNodeId == null || localNodeId.Length < 16)
                throw new ArgumentException("local node-id must be at least 16 bytes");

            var maskingIv = new byte[MaskingIvLength];
            Buffer.BlockCopy(packet, 0, maskingIv, 0, MaskingIvLength);

            var maskKey = new byte[16];
            Buffer.BlockCopy(localNodeId, 0, maskKey, 0, 16);

            var maskedStatic = new byte[HeaderStaticLength];
            Buffer.BlockCopy(packet, MaskingIvLength, maskedStatic, 0, HeaderStaticLength);
            var staticHeader = AesCtrTransform(maskKey, maskingIv, maskedStatic);

            ValidateStaticHeader(staticHeader);
            var flag = (PacketFlag)staticHeader[ProtocolId.Length + 2];
            var nonce = new byte[NonceLength];
            Buffer.BlockCopy(staticHeader, ProtocolId.Length + 3, nonce, 0, NonceLength);
            var authdataSize = (ushort)((staticHeader[HeaderStaticLength - 2] << 8) | staticHeader[HeaderStaticLength - 1]);

            if (packet.Length < MaskingIvLength + HeaderStaticLength + authdataSize)
                throw new ArgumentException("packet truncated before authdata");
            var maskedAuth = new byte[authdataSize];
            Buffer.BlockCopy(packet, MaskingIvLength + HeaderStaticLength, maskedAuth, 0, authdataSize);

            var combinedMasked = new byte[HeaderStaticLength + authdataSize];
            Buffer.BlockCopy(maskedStatic, 0, combinedMasked, 0, HeaderStaticLength);
            Buffer.BlockCopy(maskedAuth, 0, combinedMasked, HeaderStaticLength, authdataSize);
            var combinedPlain = AesCtrTransform(maskKey, maskingIv, combinedMasked);

            var authData = new byte[authdataSize];
            Buffer.BlockCopy(combinedPlain, HeaderStaticLength, authData, 0, authdataSize);

            var header = new Header
            {
                Flag = flag,
                Nonce = nonce,
                AuthData = authData
            };

            var msgStart = MaskingIvLength + HeaderStaticLength + authdataSize;
            var encryptedMessage = new byte[packet.Length - msgStart];
            Buffer.BlockCopy(packet, msgStart, encryptedMessage, 0, encryptedMessage.Length);

            return (maskingIv, header, encryptedMessage, combinedPlain);
        }

        public static byte[] EncryptMessage(byte[] sessionKey, byte[] nonce, byte[] aad, byte[] plaintext)
        {
            using var aesGcm = new AesGcm(sessionKey, 16);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, aad);

            var combined = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);
            return combined;
        }

        public static byte[] DecryptMessage(byte[] sessionKey, byte[] nonce, byte[] aad, byte[] ciphertextWithTag)
        {
            if (ciphertextWithTag == null || ciphertextWithTag.Length < 16)
                throw new ArgumentException("ciphertext+tag too short");

            using var aesGcm = new AesGcm(sessionKey, 16);
            var ciphertextLen = ciphertextWithTag.Length - 16;
            var ciphertext = new byte[ciphertextLen];
            var tag = new byte[16];
            Buffer.BlockCopy(ciphertextWithTag, 0, ciphertext, 0, ciphertextLen);
            Buffer.BlockCopy(ciphertextWithTag, ciphertextLen, tag, 0, 16);

            var plaintext = new byte[ciphertextLen];
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, aad);
            return plaintext;
        }

        internal static byte[] BuildAad(byte[] maskingIv, byte[] rawHeader)
        {
            var aad = new byte[maskingIv.Length + rawHeader.Length];
            Buffer.BlockCopy(maskingIv, 0, aad, 0, maskingIv.Length);
            Buffer.BlockCopy(rawHeader, 0, aad, maskingIv.Length, rawHeader.Length);
            return aad;
        }

        internal static byte[] BuildRawHeader(Header h)
        {
            var raw = new byte[HeaderStaticLength + h.AuthData.Length];
            int o = 0;
            Buffer.BlockCopy(ProtocolId, 0, raw, o, ProtocolId.Length); o += ProtocolId.Length;
            raw[o++] = (byte)((Version >> 8) & 0xff);
            raw[o++] = (byte)(Version & 0xff);
            raw[o++] = (byte)h.Flag;
            Buffer.BlockCopy(h.Nonce, 0, raw, o, NonceLength); o += NonceLength;
            raw[o++] = (byte)((h.AuthData.Length >> 8) & 0xff);
            raw[o++] = (byte)(h.AuthData.Length & 0xff);
            Buffer.BlockCopy(h.AuthData, 0, raw, o, h.AuthData.Length);
            return raw;
        }

        private static void ValidateStaticHeader(byte[] header)
        {
            for (int i = 0; i < ProtocolId.Length; i++)
                if (header[i] != ProtocolId[i])
                    throw new InvalidOperationException("Discv5 protocol-id mismatch (wrong masking key or corrupted packet)");
            var version = (ushort)((header[ProtocolId.Length] << 8) | header[ProtocolId.Length + 1]);
            if (version != Version)
                throw new InvalidOperationException($"Discv5 version 0x{version:X4} not supported");
        }

        private static byte[] AesCtrTransform(byte[] key, byte[] iv, byte[] input)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = key;

            var output = new byte[input.Length];
            var counter = new byte[16];
            Buffer.BlockCopy(iv, 0, counter, 0, 16);

            using var encryptor = aes.CreateEncryptor();
            var keystreamBlock = new byte[16];

            int processed = 0;
            while (processed < input.Length)
            {
                encryptor.TransformBlock(counter, 0, 16, keystreamBlock, 0);
                int chunk = Math.Min(16, input.Length - processed);
                for (int i = 0; i < chunk; i++)
                    output[processed + i] = (byte)(input[processed + i] ^ keystreamBlock[i]);
                processed += chunk;
                IncrementCounter(counter);
            }
            return output;
        }

        private static void IncrementCounter(byte[] counter)
        {
            for (int i = counter.Length - 1; i >= 0; i--)
            {
                if (++counter[i] != 0) break;
            }
        }
    }
}
