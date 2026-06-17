using System;
using Nethereum.DevP2P.Crypto;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Nethereum.DevP2P.Rlpx
{
    public class RlpxFrameWriter
    {
        private const int BlockSize = 16;
        private static readonly byte[] HeaderData = { 0xc2, 0x80, 0x80 };

        private readonly SicBlockCipher _encStream;
        private readonly AesEngine _macEncryptor;
        private readonly KeccakMacState _egressMac;

        public RlpxFrameWriter(byte[] aesSecret, byte[] macSecret, KeccakMacState egressMac)
        {
            _encStream = new SicBlockCipher(new AesEngine());
            _encStream.Init(true, new ParametersWithIV(
                new KeyParameter(aesSecret), new byte[BlockSize]));

            _macEncryptor = new AesEngine();
            _macEncryptor.Init(true, new KeyParameter(macSecret));

            _egressMac = egressMac;
        }

        public byte[] WriteFrame(int msgId, byte[] payload)
        {
            var frameData = BuildFrameData(msgId, payload);
            var frameSize = frameData.Length;

            // BuildHeader packs frameSize into a 24-bit big-endian field; any
            // value ≥ 2^24 silently truncates and produces a frame the peer
            // can't reassemble (MAC mismatch). Reject at the boundary instead
            // of corrupting the wire — symmetric with RlpxFrameReader's
            // MaxFrameSize enforcement on read.
            if (frameSize > RlpxFrameReader.MaxFrameSize)
                throw new InvalidOperationException(
                    $"egress frame size {frameSize} exceeds MaxFrameSize {RlpxFrameReader.MaxFrameSize}");

            var header = BuildHeader(frameSize);
            var headerCipher = EncryptBlock(header);
            var headerMac = ComputeHeaderMac(headerCipher);

            var framePaddedLen = ((frameData.Length + BlockSize - 1) / BlockSize) * BlockSize;
            var framePadded = framePaddedLen == frameData.Length
                ? frameData
                : frameData.PadBytesRight(framePaddedLen);
            var frameCipher = Encrypt(framePadded);
            var frameMac = ComputeFrameMac(frameCipher);

            return ByteUtil.Merge(headerCipher, headerMac, frameCipher, frameMac);
        }

        private static byte[] BuildFrameData(int msgId, byte[] payload)
        {
            var msgIdEncoded = RlpEncodeMsgId(msgId);
            // Hello is always uncompressed. Every post-Hello message must be
            // Snappy-encoded even when payload is empty — otherwise the peer's
            // Snappy decoder receives 0 bytes and reports "corrupt input"
            // (caught by Geth's eth-test BlockRangeUpdate sub-tests when our
            // empty Pong frames went out unencoded).
            byte[] body = (msgId != P2PMessageIds.Hello)
                ? IronSnappy.Snappy.Encode(payload)
                : payload;
            return msgIdEncoded.ConcatArrays(body);
        }

        private static byte[] BuildHeader(int frameSize)
        {
            var header = new byte[BlockSize];
            header[0] = (byte)(frameSize >> 16);
            header[1] = (byte)(frameSize >> 8);
            header[2] = (byte)(frameSize);
            Buffer.BlockCopy(HeaderData, 0, header, 3, HeaderData.Length);
            return header;
        }

        private byte[] EncryptBlock(byte[] block)
        {
            var output = new byte[BlockSize];
            _encStream.ProcessBlock(block, 0, output, 0);
            return output;
        }

        private byte[] Encrypt(byte[] data)
        {
            var output = new byte[data.Length];
            for (int i = 0; i < data.Length; i += BlockSize)
                _encStream.ProcessBlock(data, i, output, i);
            return output;
        }

        private byte[] ComputeHeaderMac(byte[] headerCipher)
        {
            var digest = _egressMac.DigestFirst16();
            var seed = new byte[BlockSize];
            _macEncryptor.ProcessBlock(digest, 0, seed, 0);
            XorInPlace(seed, headerCipher, BlockSize);
            _egressMac.Update(seed);
            return _egressMac.DigestFirst16();
        }

        private byte[] ComputeFrameMac(byte[] frameCipher)
        {
            _egressMac.Update(frameCipher);
            var digest = _egressMac.DigestFirst16();
            var seed = new byte[BlockSize];
            _macEncryptor.ProcessBlock(digest, 0, seed, 0);
            XorInPlace(seed, digest, BlockSize);
            _egressMac.Update(seed);
            return _egressMac.DigestFirst16();
        }

        private static void XorInPlace(byte[] target, byte[] source, int length)
        {
            for (int i = 0; i < length; i++)
                target[i] ^= source[i];
        }

        private static byte[] RlpEncodeMsgId(int id)
        {
            if (id == 0) return new byte[] { 0x80 };
            if (id < 0x80) return new byte[] { (byte)id };
            if (id < 0x100) return new byte[] { 0x81, (byte)id };
            return new byte[] { 0x82, (byte)(id >> 8), (byte)id };
        }
    }
}
