using System;
using System.Security.Cryptography;

namespace Nethereum.DevP2P.Discv5
{
    /// <summary>
    /// discv5 packet wire format per discv5-wire.md:
    /// `masking-iv (16) || masked-header || message`
    /// - The 23-byte fixed header + authdata is AES-CTR-masked with key =
    ///   first 16 bytes of destination node-id and IV = masking-iv.
    /// - The message is AES-GCM-encrypted with the session key, nonce taken
    ///   from the header, AAD = protocol-id || version || flag || nonce ||
    ///   authdata-size || authdata.
    ///
    /// This class implements the framing (mask/unmask and encrypt/decrypt
    /// with provided keys). Session establishment (HKDF key derivation,
    /// WhoAreYou challenge/response) is separate work.
    /// </summary>
    public static class Discv5Packet
    {
        public const int MaskingIvLength = 16;
        public const int HeaderStaticLength = 23; // protocol-id(6) + version(2) + flag(1) + nonce(12) + authdata-size(2)
        public const int NonceLength = 12;
        public static readonly byte[] ProtocolId = new byte[] { (byte)'d', (byte)'i', (byte)'s', (byte)'c', (byte)'v', (byte)'5' };
        public const ushort Version = 0x0001;

        /// <summary>
        /// Minimum legal packet size per discv5-wire.md §"Packet Encoding":
        /// <c>masking-iv(16) ‖ static-header(23) ‖ minimum-authdata(24, WHOAREYOU)</c>.
        /// Datagrams shorter than this cannot be parsed and should be dropped
        /// at the listener before any crypto work is attempted.
        /// </summary>
        public const int MinPacketSize = 63;

        /// <summary>
        /// Minimum size of the encrypted-message portion (after the masked
        /// header) for a non-WHOAREYOU packet, per discv5-wire.md §"Packet
        /// Encoding": at least one plaintext byte plus the 16-byte GCM tag,
        /// plus the lowest-overhead authdata. The reference implementation
        /// rejects such packets in <c>checkValid</c>; we mirror by rejecting
        /// any non-WHOAREYOU packet whose total size is below the equivalent
        /// threshold.
        /// </summary>
        public const int MinMessagePayloadSize = 48;

        /// <summary>
        /// Maximum legal discv5 packet size — also the standard datagram size
        /// the reference implementation reads into. Datagrams larger than this
        /// are dropped per discv5-wire.md §"Packet Encoding".
        /// </summary>
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

        /// <summary>
        /// Encode a packet: maskedHeader = AES-CTR(headerBytes, key=destNodeId[0..16], iv=maskingIv).
        /// Message field is provided already-encrypted (or empty for WhoAreYou).
        /// </summary>
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

        /// <summary>
        /// Decode a packet. Returns the masking IV, the decoded Header, and the
        /// raw encrypted message bytes that follow the header in the packet.
        /// </summary>
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

            // Decrypt the static 23-byte prefix first to learn authdata-size.
            var maskedStatic = new byte[HeaderStaticLength];
            Buffer.BlockCopy(packet, MaskingIvLength, maskedStatic, 0, HeaderStaticLength);
            var staticHeader = AesCtrTransform(maskKey, maskingIv, maskedStatic);

            ValidateStaticHeader(staticHeader);
            var flag = (PacketFlag)staticHeader[ProtocolId.Length + 2];
            var nonce = new byte[NonceLength];
            Buffer.BlockCopy(staticHeader, ProtocolId.Length + 3, nonce, 0, NonceLength);
            var authdataSize = (ushort)((staticHeader[HeaderStaticLength - 2] << 8) | staticHeader[HeaderStaticLength - 1]);

            // Now we know how much more to decrypt (authdata).
            if (packet.Length < MaskingIvLength + HeaderStaticLength + authdataSize)
                throw new ArgumentException("packet truncated before authdata");
            var maskedAuth = new byte[authdataSize];
            Buffer.BlockCopy(packet, MaskingIvLength + HeaderStaticLength, maskedAuth, 0, authdataSize);

            // AES-CTR is a stream cipher: to decrypt bytes [HeaderStaticLength..]
            // we need to advance the counter that many bytes. We do that by
            // re-running the cipher over a zero-prefix sized buffer and slicing.
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

        /// <summary>
        /// Encrypt a discv5 message payload (already prefixed with type byte +
        /// rlp(body)) using AES-GCM with the supplied session key. The nonce
        /// is the 12-byte header nonce. AAD is the unmasked header bytes
        /// (protocol-id || version || flag || nonce || authdata-size || authdata).
        /// </summary>
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

        /// <summary>
        /// AES-GCM AAD for an encrypted discv5 message: <c>masking-iv ‖ raw-header</c>.
        /// The masking-iv is included in the AAD per the de-facto wire convention,
        /// even though the wording in <c>discv5-wire.md §"message-ad"</c> reads
        /// otherwise. Interop with reference implementations confirms.
        /// </summary>
        internal static byte[] BuildAad(byte[] maskingIv, byte[] rawHeader)
        {
            var aad = new byte[maskingIv.Length + rawHeader.Length];
            Buffer.BlockCopy(maskingIv, 0, aad, 0, maskingIv.Length);
            Buffer.BlockCopy(rawHeader, 0, aad, maskingIv.Length, rawHeader.Length);
            return aad;
        }

        /// <summary>
        /// Build the unmasked header bytes for a discv5 packet:
        /// <c>protocol-id(6) ‖ version(2) ‖ flag(1) ‖ nonce(12) ‖ authdata-size(2) ‖ authdata</c>.
        /// Exposed as <c>internal</c> so the session manager can reconstruct the
        /// same bytes for challenge-data + AAD computation — both sides must
        /// agree on these for AES-GCM to authenticate.
        /// </summary>
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
