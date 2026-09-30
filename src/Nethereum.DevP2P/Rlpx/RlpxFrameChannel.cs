using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Rlpx
{
    internal sealed class RlpxFrameChannel : IDisposable
    {
        private readonly Stream _stream;
        private readonly RlpxFrameWriter _writer;
        private readonly RlpxFrameReader _reader;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly SemaphoreSlim _readLock = new(1, 1);

        public RlpxFrameChannel(Stream stream, RlpxFrameWriter writer, RlpxFrameReader reader)
        {
            _stream = stream;
            _writer = writer;
            _reader = reader;
        }

        public async Task SendFrameAsync(int msgId, byte[] payload, CancellationToken ct = default)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                var frame = _writer.WriteFrame(msgId, payload);
                await _stream.WriteAsync(frame, ct);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<(int msgId, byte[] payload)> ReadFrameAsync(CancellationToken ct = default)
        {
            await _readLock.WaitAsync(ct);
            try
            {
                var headerBlock = new byte[32];
                await RlpxStreamReader.ReadExactlyAsync(_stream, headerBlock, ct);

                var frameSize = _reader.ReadHeader(headerBlock);
                var framePaddedSize = RlpxFrameFormat.PaddedSize(frameSize);

                var bodyBlock = new byte[framePaddedSize + 16];
                await RlpxStreamReader.ReadExactlyAsync(_stream, bodyBlock, ct);

                return _reader.ReadBody(frameSize, bodyBlock);
            }
            finally
            {
                _readLock.Release();
            }
        }

        public void Dispose()
        {
            _writeLock.Dispose();
            _readLock.Dispose();
        }
    }
}
