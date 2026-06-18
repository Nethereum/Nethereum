using System;
using System.Security.Cryptography;
using Nethereum.DevP2P.Crypto;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Nethereum.DevP2P.Rlpx
{
    public class RlpxFrameReader
    {
        private const int BlockSize = 16;
        private const int HeaderBlockLen = BlockSize + BlockSize;

        /// <summary>
        /// Maximum allowed RLPx frame body size (16 MB - 1). The 24-bit length
        /// field structurally caps frames at this value, but without an
        /// explicit guard a buggy or malicious peer could still cause large
        /// allocations before downstream validation. Enforced in both
        /// <see cref="ReadFrame"/> and <see cref="ReadHeader"/>.
        /// </summary>
        public const int MaxFrameSize = 16 * 1024 * 1024 - 1;

        /// <summary>
        /// Hard ceiling on the post-Snappy-decode body size. A peer can ship a
        /// compressed 16 MB frame that decompresses to gigabytes (Snappy ratio
        /// can exceed 100×), exhausting memory before any application-layer
        /// validation sees the message. 32 MiB is roughly twice the compressed
        /// ceiling — enough headroom for any genuine eth/snap message, far
        /// short of OOM territory.
        /// </summary>
        public const int MaxDecompressedFrameSize = 32 * 1024 * 1024;

        private readonly SicBlockCipher _decStream;
        private readonly AesEngine _macEncryptor;
        private readonly KeccakMacState _ingressMac;

        public RlpxFrameReader(byte[] aesSecret, byte[] macSecret, KeccakMacState ingressMac)
        {
            _decStream = new SicBlockCipher(new AesEngine());
            _decStream.Init(true, new ParametersWithIV(
                new KeyParameter(aesSecret), new byte[BlockSize]));

            _macEncryptor = new AesEngine();
            _macEncryptor.Init(true, new KeyParameter(macSecret));

            _ingressMac = ingressMac;
        }

        public (int msgId, byte[] payload) ReadFrame(byte[] frame)
        {
            if (frame.Length < HeaderBlockLen + BlockSize + BlockSize)
                throw new CryptographicException("Frame too short");

            var headerCipher = frame.Slice(0, BlockSize);
            var receivedHeaderMac = frame.Slice(BlockSize, HeaderBlockLen);

            var computedHeaderMac = VerifyHeaderMac(headerCipher);
            if (!ByteUtil.ConstantTimeEquals(computedHeaderMac, receivedHeaderMac))
                throw new CryptographicException("Header MAC verification failed");

            var header = DecryptBlock(headerCipher);
            var frameSize = (header[0] << 16) | (header[1] << 8) | header[2];
            if (frameSize > MaxFrameSize)
                throw new CryptographicException($"Frame size {frameSize} exceeds MaxFrameSize ({MaxFrameSize})");
            var framePaddedSize = ((frameSize + BlockSize - 1) / BlockSize) * BlockSize;

            if (frame.Length < HeaderBlockLen + framePaddedSize + BlockSize)
                throw new CryptographicException("Frame body shorter than header claims");

            var frameCipher = frame.Slice(HeaderBlockLen, HeaderBlockLen + framePaddedSize);
            var receivedFrameMac = frame.Slice(HeaderBlockLen + framePaddedSize, HeaderBlockLen + framePaddedSize + BlockSize);

            var computedFrameMac = VerifyFrameMac(frameCipher);
            if (!ByteUtil.ConstantTimeEquals(computedFrameMac, receivedFrameMac))
                throw new CryptographicException("Frame MAC verification failed");

            var frameData = Decrypt(frameCipher);
            return ExtractMessage(frameData, frameSize);
        }

        public int ReadHeader(byte[] headerBlock)
        {
            var headerCipher = headerBlock.Slice(0, BlockSize);
            var receivedMac = headerBlock.Slice(BlockSize, HeaderBlockLen);

            var computedMac = VerifyHeaderMac(headerCipher);
            if (!ByteUtil.ConstantTimeEquals(computedMac, receivedMac))
                throw new CryptographicException("Header MAC verification failed");

            var header = DecryptBlock(headerCipher);
            var frameSize = (header[0] << 16) | (header[1] << 8) | header[2];
            if (frameSize > MaxFrameSize)
                throw new CryptographicException($"Frame size {frameSize} exceeds MaxFrameSize ({MaxFrameSize})");
            return frameSize;
        }

        public (int msgId, byte[] payload) ReadBody(int frameSize, byte[] bodyBlock)
        {
            var framePaddedSize = ((frameSize + BlockSize - 1) / BlockSize) * BlockSize;

            if (bodyBlock.Length < framePaddedSize + BlockSize)
                throw new CryptographicException("Body block shorter than expected");

            var frameCipher = bodyBlock.Slice(0, framePaddedSize);
            var receivedMac = bodyBlock.Slice(framePaddedSize, framePaddedSize + BlockSize);

            var computedMac = VerifyFrameMac(frameCipher);
            if (!ByteUtil.ConstantTimeEquals(computedMac, receivedMac))
                throw new CryptographicException("Frame MAC verification failed");

            var frameData = Decrypt(frameCipher);
            return ExtractMessage(frameData, frameSize);
        }

        private (int msgId, byte[] payload) ExtractMessage(byte[] frameData, int frameSize)
        {
            var (msgId, msgIdLen) = RlpDecodeMsgId(frameData);

            if (frameSize < msgIdLen)
                throw new CryptographicException("Frame size smaller than message ID encoding");

            var bodyLen = frameSize - msgIdLen;
            var body = frameData.Slice(msgIdLen, msgIdLen + bodyLen);

            if (msgId != P2PMessageIds.Hello && bodyLen > 0)
            {
                try
                {
                    if (TryReadSnappyDecompressedLength(body, out var claimedDecompressedSize) &&
                        claimedDecompressedSize > MaxDecompressedFrameSize)
                    {
                        throw new CryptographicException(
                            $"Snappy frame header claims decompressed size {claimedDecompressedSize} > {MaxDecompressedFrameSize}");
                    }

                    body = IronSnappy.Snappy.Decode(body);
                    if (body.Length > MaxDecompressedFrameSize)
                        throw new CryptographicException(
                            $"Snappy-decoded frame ({body.Length} bytes) exceeds {MaxDecompressedFrameSize} cap");
                }
                catch (CryptographicException)
                {
                    throw;
                }
                catch
                {
                    // Snappy is enabled only after the mutual Hello exchange
                    // completes. A peer that rejects us before sending its own
                    // Hello — e.g. capability mismatch or "too many peers" —
                    // sends a Disconnect uncompressed. Trying to snappy-decode
                    // an uncompressed Disconnect raises "corrupt input"; fall
                    // back to the raw body so the caller can read the
                    // Disconnect reason and surface it.
                    if (msgId != P2PMessageIds.Disconnect) throw;
                }
            }

            return (msgId, body);
        }

        /// <summary>
        /// Parses the leading varint that every Snappy-compressed block begins
        /// with — the declared uncompressed length. Per the Snappy framing
        /// format (§3 of
        /// https://github.com/google/snappy/blob/master/format_description.txt),
        /// the varint is at most 5 bytes (encoding a uint32 in 7-bit groups,
        /// little-endian, with the high bit set on every byte except the last).
        /// Returning <c>false</c> means the body is truncated or malformed and
        /// the size check is skipped — <c>IronSnappy.Snappy.Decode</c> will
        /// surface the same problem.
        /// </summary>
        public static bool TryReadSnappyDecompressedLength(byte[] body, out long decompressedLength)
        {
            decompressedLength = 0;
            if (body == null || body.Length == 0) return false;

            long value = 0;
            int shift = 0;
            int maxBytes = body.Length < 5 ? body.Length : 5;
            for (int i = 0; i < maxBytes; i++)
            {
                byte b = body[i];
                value |= (long)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    decompressedLength = value;
                    return true;
                }
                shift += 7;
            }
            return false;
        }

        private byte[] DecryptBlock(byte[] block)
        {
            var output = new byte[BlockSize];
            _decStream.ProcessBlock(block, 0, output, 0);
            return output;
        }

        private byte[] Decrypt(byte[] data)
        {
            var output = new byte[data.Length];
            for (int i = 0; i < data.Length; i += BlockSize)
                _decStream.ProcessBlock(data, i, output, i);
            return output;
        }

        private byte[] VerifyHeaderMac(byte[] headerCipher)
        {
            var digest = _ingressMac.DigestFirst16();
            var seed = new byte[BlockSize];
            _macEncryptor.ProcessBlock(digest, 0, seed, 0);
            XorInPlace(seed, headerCipher, BlockSize);
            _ingressMac.Update(seed);
            return _ingressMac.DigestFirst16();
        }

        private byte[] VerifyFrameMac(byte[] frameCipher)
        {
            _ingressMac.Update(frameCipher);
            var digest = _ingressMac.DigestFirst16();
            var seed = new byte[BlockSize];
            _macEncryptor.ProcessBlock(digest, 0, seed, 0);
            XorInPlace(seed, digest, BlockSize);
            _ingressMac.Update(seed);
            return _ingressMac.DigestFirst16();
        }

        private static void XorInPlace(byte[] target, byte[] source, int length)
        {
            for (int i = 0; i < length; i++)
                target[i] ^= source[i];
        }

        private static (int msgId, int bytesConsumed) RlpDecodeMsgId(byte[] data)
        {
            if (data[0] == 0x80) return (0, 1);
            if (data[0] < 0x80) return (data[0], 1);
            if (data[0] == 0x81) return (data[1], 2);
            if (data[0] == 0x82) return ((data[1] << 8) | data[2], 3);
            throw new InvalidOperationException($"Unexpected RLP msg-id prefix: 0x{data[0]:x2}");
        }
    }
}
