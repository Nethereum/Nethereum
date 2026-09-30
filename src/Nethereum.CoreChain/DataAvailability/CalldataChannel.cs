using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Nethereum.CoreChain.DataAvailability
{
    public sealed class CalldataFrame
    {
        public byte[] ChannelId { get; init; }
        public ushort FrameNumber { get; init; }
        public byte[] Data { get; init; }
        public bool IsLast { get; init; }
    }

    public sealed class CalldataChannel
    {
        private readonly MemoryStream _buffer = new();
        private readonly byte[] _channelId;
        private readonly CompressionAlgo _compression;
        private long _startBlock;
        private long _endBlock;
        private int _blockCount;
        private bool _closed;

        public CalldataChannel(CompressionAlgo compression = CompressionAlgo.Brotli)
        {
            _compression = compression;
            _channelId = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(_channelId);
        }

        public byte[] ChannelId => _channelId;
        public int BlockCount => _blockCount;
        public long StartBlock => _startBlock;
        public long EndBlock => _endBlock;
        public int RawSize => (int)_buffer.Length;

        public void AddBlock(long blockNumber, byte[] rlpBlock)
        {
            if (_closed) throw new InvalidOperationException("Channel is closed");
            if (rlpBlock == null || rlpBlock.Length == 0) return;

            if (_blockCount == 0)
                _startBlock = blockNumber;
            _endBlock = blockNumber;
            _blockCount++;

            var bw = new BinaryWriter(_buffer);
            bw.Write(blockNumber);
            bw.Write(rlpBlock.Length);
            bw.Write(rlpBlock);
        }

        public byte[] Close()
        {
            if (_closed) throw new InvalidOperationException("Channel already closed");
            _closed = true;

            var raw = _buffer.ToArray();
            var compressed = ChannelCompressor.Compress(raw, _compression);

            using var output = new MemoryStream();
            using var bw = new BinaryWriter(output);

            bw.Write((byte)1);
            bw.Write((byte)_compression);
            bw.Write(_channelId);
            bw.Write(_startBlock);
            bw.Write(_endBlock);
            bw.Write(_blockCount);
            bw.Write(raw.Length);
            bw.Write(compressed.Length);
            bw.Write(compressed);

            return output.ToArray();
        }

        public List<CalldataFrame> CloseAndFrame(int maxFrameSize = ChannelCompressor.MaxFrameSize)
        {
            var channelBytes = Close();
            var frames = new List<CalldataFrame>();
            int offset = 0;
            ushort frameNum = 0;

            while (offset < channelBytes.Length)
            {
                int remaining = channelBytes.Length - offset;
                int size = Math.Min(remaining, maxFrameSize);
                var data = new byte[size];
                Array.Copy(channelBytes, offset, data, 0, size);

                frames.Add(new CalldataFrame
                {
                    ChannelId = _channelId,
                    FrameNumber = frameNum,
                    Data = data,
                    IsLast = offset + size >= channelBytes.Length
                });

                offset += size;
                frameNum++;
            }

            return frames;
        }

        public static (long startBlock, long endBlock, int blockCount, List<byte[]> blocks) Decode(byte[] channelData)
        {
            using var ms = new MemoryStream(channelData);
            using var br = new BinaryReader(ms);

            var version = br.ReadByte();
            if (version != 1)
                throw new NotSupportedException($"Unsupported channel version: {version}");

            var compression = (CompressionAlgo)br.ReadByte();
            var channelId = br.ReadBytes(16);
            var startBlock = br.ReadInt64();
            var endBlock = br.ReadInt64();
            var blockCount = br.ReadInt32();
            var rawLength = br.ReadInt32();
            var compressedLength = br.ReadInt32();
            var compressed = br.ReadBytes(compressedLength);

            var raw = ChannelCompressor.Decompress(compressed, compression);
            if (raw.Length != rawLength)
                throw new InvalidDataException($"Decompressed size mismatch: expected {rawLength}, got {raw.Length}");

            var blocks = new List<byte[]>(blockCount);
            using var blockStream = new MemoryStream(raw);
            using var blockReader = new BinaryReader(blockStream);

            for (int i = 0; i < blockCount; i++)
            {
                var bn = blockReader.ReadInt64();
                var len = blockReader.ReadInt32();
                var data = blockReader.ReadBytes(len);
                blocks.Add(data);
            }

            return (startBlock, endBlock, blockCount, blocks);
        }
    }
}
