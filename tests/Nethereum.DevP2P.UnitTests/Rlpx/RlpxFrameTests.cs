using System;
using System.Security.Cryptography;
using Nethereum.DevP2P.Crypto;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    public class RlpxFrameTests
    {
        [Fact]
        public void WriteRead_RoundTrip()
        {
            var (writer, reader) = CreateWriterReaderPair();

            var payload = new byte[] { 0xc3, 0x01, 0x02, 0x03 };
            var frame = writer.WriteFrame(0x10, payload);
            var (msgId, decoded) = reader.ReadFrame(frame);

            Assert.Equal(0x10, msgId);
            Assert.Equal(payload, decoded);
        }

        [Fact]
        public void WriteRead_MultipleFrames_CtrStreamContinuous()
        {
            var (writer, reader) = CreateWriterReaderPair();

            for (int i = 0; i < 10; i++)
            {
                var payload = new byte[] { (byte)(0xc0 + i), (byte)i };
                var frame = writer.WriteFrame(i + 1, payload);
                var (msgId, decoded) = reader.ReadFrame(frame);

                Assert.Equal(i + 1, msgId);
                Assert.Equal(payload, decoded);
            }
        }

        [Fact]
        public void WriteRead_LargePayload()
        {
            var (writer, reader) = CreateWriterReaderPair();

            var payload = new byte[1024];
            RandomNumberGenerator.Fill(payload);
            var frame = writer.WriteFrame(0x04, payload);
            var (msgId, decoded) = reader.ReadFrame(frame);

            Assert.Equal(0x04, msgId);
            Assert.Equal(payload, decoded);
        }

        [Fact]
        public void WriteRead_MsgIdZero_Hello_NotCompressed()
        {
            var (writer, reader) = CreateWriterReaderPair();

            var helloPayload = new byte[] { 0xc5, 0x05, 0x80, 0xc0, 0x80, 0x80 };
            var frame = writer.WriteFrame(0x00, helloPayload);
            var (msgId, decoded) = reader.ReadFrame(frame);

            Assert.Equal(0, msgId);
            Assert.Equal(helloPayload, decoded);
        }

        [Fact]
        public void WriteRead_SnappyCompressedPayload()
        {
            var (writer, reader) = CreateWriterReaderPair();

            var payload = new byte[256];
            for (int i = 0; i < payload.Length; i++)
                payload[i] = (byte)(i % 4);

            var frame = writer.WriteFrame(0x04, payload);
            var (msgId, decoded) = reader.ReadFrame(frame);

            Assert.Equal(0x04, msgId);
            Assert.Equal(payload, decoded);
        }

        [Fact]
        public void WriteFrame_FrameSize_MinimumIs64Bytes()
        {
            var (writer, _) = CreateWriterReaderPair();

            // Smallest possible: 1-byte msgId + 0 payload
            var frame = writer.WriteFrame(1, Array.Empty<byte>());

            // header(16) + headerMac(16) + body padded to 16 + bodyMac(16) = 64 minimum
            Assert.Equal(64, frame.Length);
        }

        [Fact]
        public void ReadFrame_CorruptedHeaderMac_Throws()
        {
            var (writer, reader) = CreateWriterReaderPair();

            var frame = writer.WriteFrame(1, new byte[] { 0xc0 });
            frame[20] ^= 0xFF;

            Assert.Throws<CryptographicException>(() => reader.ReadFrame(frame));
        }

        [Fact]
        public void ReadFrame_CorruptedFrameMac_Throws()
        {
            var (writer, reader) = CreateWriterReaderPair();

            var frame = writer.WriteFrame(1, new byte[] { 0xc0 });
            frame[frame.Length - 2] ^= 0xFF;

            Assert.Throws<CryptographicException>(() => reader.ReadFrame(frame));
        }

        [Fact]
        public void ReadFrame_CorruptedCiphertext_Throws()
        {
            var (writer, reader) = CreateWriterReaderPair();

            var frame = writer.WriteFrame(1, new byte[] { 0xc0 });
            frame[35] ^= 0xFF; // corrupt frame body ciphertext

            Assert.Throws<CryptographicException>(() => reader.ReadFrame(frame));
        }

        [Fact]
        public void WriteRead_AllMsgIdEncodings()
        {
            // Test RLP encoding of msgId at boundary values
            var msgIds = new[] { 0, 1, 0x7f, 0x80, 0xff, 0x100 };

            foreach (var id in msgIds)
            {
                var (writer, reader) = CreateWriterReaderPair();
                var payload = new byte[] { 0xc0 };
                var frame = writer.WriteFrame(id, payload);
                var (decoded, _) = reader.ReadFrame(frame);
                Assert.Equal(id, decoded);
            }
        }

        private static (RlpxFrameWriter writer, RlpxFrameReader reader) CreateWriterReaderPair()
        {
            var aesKey = new byte[32];
            var macKey = new byte[32];
            RandomNumberGenerator.Fill(aesKey);
            RandomNumberGenerator.Fill(macKey);

            // Both sides initialized with same MAC state
            // In real RLPx: initiator egress = recipient ingress (same init)
            var macSeed = new byte[32];
            RandomNumberGenerator.Fill(macSeed);
            var egressMac = KeccakMacState.Init(macSeed, Array.Empty<byte>());
            var ingressMac = KeccakMacState.Init(macSeed, Array.Empty<byte>());

            var writer = new RlpxFrameWriter(aesKey, macKey, egressMac);
            var reader = new RlpxFrameReader(aesKey, macKey, ingressMac);

            return (writer, reader);
        }
    }
}
