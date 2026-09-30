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
        private readonly RlpxMac _mac;

        public RlpxFrameWriter(byte[] aesSecret, byte[] macSecret, KeccakMacState egressMac)
        {
            _encStream = new SicBlockCipher(new AesEngine());
            _encStream.Init(true, new ParametersWithIV(
                new KeyParameter(aesSecret), new byte[BlockSize]));

            _mac = new RlpxMac(macSecret, egressMac);
        }

        public byte[] WriteFrame(int msgId, byte[] payload)
        {
            var frameData = BuildFrameData(msgId, payload);
            var frameSize = frameData.Length;

            if (frameSize > RlpxFrameReader.MaxFrameSize)
                throw new InvalidOperationException(
                    $"egress frame size {frameSize} exceeds MaxFrameSize {RlpxFrameReader.MaxFrameSize}");

            var header = BuildHeader(frameSize);
            var headerCipher = EncryptBlock(header);
            var headerMac = _mac.NextHeaderMac(headerCipher);

            var framePaddedLen = RlpxFrameFormat.PaddedSize(frameData.Length);
            var framePadded = framePaddedLen == frameData.Length
                ? frameData
                : frameData.PadBytesRight(framePaddedLen);
            var frameCipher = Encrypt(framePadded);
            var frameMac = _mac.NextFrameMac(frameCipher);

            return ByteUtil.Merge(headerCipher, headerMac, frameCipher, frameMac);
        }

        private static byte[] BuildFrameData(int msgId, byte[] payload)
        {
            var msgIdEncoded = RlpxMessageId.Encode(msgId);
            byte[] body = (msgId != P2PMessageIds.Hello)
                ? IronSnappy.Snappy.Encode(payload)
                : payload;
            return msgIdEncoded.ConcatArrays(body);
        }

        private static byte[] BuildHeader(int frameSize)
        {
            var header = new byte[BlockSize];
            RlpxFrameFormat.WriteHeaderSize(header, frameSize);
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
    }
}
