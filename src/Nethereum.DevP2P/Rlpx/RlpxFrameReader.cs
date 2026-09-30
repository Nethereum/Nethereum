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

        public const int MaxFrameSize = 16 * 1024 * 1024 - 1;

        public const int MaxDecompressedFrameSize = 32 * 1024 * 1024;

        private readonly SicBlockCipher _decStream;
        private readonly RlpxMac _mac;

        public RlpxFrameReader(byte[] aesSecret, byte[] macSecret, KeccakMacState ingressMac)
        {
            _decStream = new SicBlockCipher(new AesEngine());
            _decStream.Init(true, new ParametersWithIV(
                new KeyParameter(aesSecret), new byte[BlockSize]));

            _mac = new RlpxMac(macSecret, ingressMac);
        }

        public (int msgId, byte[] payload) ReadFrame(byte[] frame)
        {
            if (frame.Length < HeaderBlockLen + BlockSize + BlockSize)
                throw new CryptographicException("Frame too short");

            var headerCipher = frame.Slice(0, BlockSize);
            var receivedHeaderMac = frame.Slice(BlockSize, HeaderBlockLen);

            var computedHeaderMac = _mac.NextHeaderMac(headerCipher);
            if (!ByteUtil.ConstantTimeEquals(computedHeaderMac, receivedHeaderMac))
                throw new CryptographicException("Header MAC verification failed");

            var header = DecryptBlock(headerCipher);
            var frameSize = RlpxFrameFormat.ReadHeaderSize(header);
            if (frameSize > MaxFrameSize)
                throw new CryptographicException($"Frame size {frameSize} exceeds MaxFrameSize ({MaxFrameSize})");
            var framePaddedSize = RlpxFrameFormat.PaddedSize(frameSize);

            if (frame.Length < HeaderBlockLen + framePaddedSize + BlockSize)
                throw new CryptographicException("Frame body shorter than header claims");

            var frameCipher = frame.Slice(HeaderBlockLen, HeaderBlockLen + framePaddedSize);
            var receivedFrameMac = frame.Slice(HeaderBlockLen + framePaddedSize, HeaderBlockLen + framePaddedSize + BlockSize);

            var computedFrameMac = _mac.NextFrameMac(frameCipher);
            if (!ByteUtil.ConstantTimeEquals(computedFrameMac, receivedFrameMac))
                throw new CryptographicException("Frame MAC verification failed");

            var frameData = Decrypt(frameCipher);
            return ExtractMessage(frameData, frameSize);
        }

        public int ReadHeader(byte[] headerBlock)
        {
            var headerCipher = headerBlock.Slice(0, BlockSize);
            var receivedMac = headerBlock.Slice(BlockSize, HeaderBlockLen);

            var computedMac = _mac.NextHeaderMac(headerCipher);
            if (!ByteUtil.ConstantTimeEquals(computedMac, receivedMac))
                throw new CryptographicException("Header MAC verification failed");

            var header = DecryptBlock(headerCipher);
            var frameSize = RlpxFrameFormat.ReadHeaderSize(header);
            if (frameSize > MaxFrameSize)
                throw new CryptographicException($"Frame size {frameSize} exceeds MaxFrameSize ({MaxFrameSize})");
            return frameSize;
        }

        public (int msgId, byte[] payload) ReadBody(int frameSize, byte[] bodyBlock)
        {
            var framePaddedSize = RlpxFrameFormat.PaddedSize(frameSize);

            if (bodyBlock.Length < framePaddedSize + BlockSize)
                throw new CryptographicException("Body block shorter than expected");

            var frameCipher = bodyBlock.Slice(0, framePaddedSize);
            var receivedMac = bodyBlock.Slice(framePaddedSize, framePaddedSize + BlockSize);

            var computedMac = _mac.NextFrameMac(frameCipher);
            if (!ByteUtil.ConstantTimeEquals(computedMac, receivedMac))
                throw new CryptographicException("Frame MAC verification failed");

            var frameData = Decrypt(frameCipher);
            return ExtractMessage(frameData, frameSize);
        }

        private (int msgId, byte[] payload) ExtractMessage(byte[] frameData, int frameSize)
        {
            var (msgId, msgIdLen) = RlpxMessageId.Decode(frameData);

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
                    if (msgId != P2PMessageIds.Disconnect) throw;
                }
            }

            return (msgId, body);
        }

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
    }
}
