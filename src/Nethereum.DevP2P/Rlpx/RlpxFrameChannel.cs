using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Rlpx
{
    internal sealed class RlpxFrameChannel : IDisposable
    {
        private readonly Stream _stream;
        private readonly RlpxFrameWriter _writer;
        private readonly RlpxFrameReader _reader;
        private readonly int _writeTimeoutMs;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly SemaphoreSlim _readLock = new(1, 1);
        private int _maxFrameSize = RlpxFrameReader.BaseProtocolMaxMsgSize + 16;

        public RlpxFrameChannel(Stream stream, RlpxFrameWriter writer, RlpxFrameReader reader, int writeTimeoutMs)
        {
            _stream = stream;
            _writer = writer;
            _reader = reader;
            _writeTimeoutMs = writeTimeoutMs;
        }

        public void LiftHandshakeFrameLimit()
            => Volatile.Write(ref _maxFrameSize, RlpxFrameReader.MaxFrameSize);

        public async Task SendFrameAsync(int msgId, byte[] payload, CancellationToken ct = default)
        {
            using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (_writeTimeoutMs > 0) writeCts.CancelAfter(_writeTimeoutMs);
            var frameBegun = false;
            try
            {
                await _writeLock.WaitAsync(writeCts.Token).ConfigureAwait(false);
                try
                {
                    frameBegun = true;
                    var frame = _writer.WriteFrame(msgId, payload);
                    await _stream.WriteAsync(frame, writeCts.Token).ConfigureAwait(false);
                }
                finally
                {
                    ReleaseQuietly(_writeLock);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException($"RLPx frame write exceeded {_writeTimeoutMs} ms");
            }
            catch (OperationCanceledException ex) when (frameBegun)
            {
                throw new IOException("RLPx frame write cancelled after the frame began; the stream can no longer be framed", ex);
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
                var limit = Volatile.Read(ref _maxFrameSize);
                if (frameSize > limit)
                    throw new CryptographicException(
                        $"Frame size {frameSize} exceeds the {limit} byte limit before the Hello is complete");
                var framePaddedSize = RlpxFrameFormat.PaddedSize(frameSize);

                var bodyBlock = new byte[framePaddedSize + 16];
                await RlpxStreamReader.ReadExactlyAsync(_stream, bodyBlock, ct);

                return _reader.ReadBody(frameSize, bodyBlock);
            }
            finally
            {
                ReleaseQuietly(_readLock);
            }
        }

        private static void ReleaseQuietly(SemaphoreSlim semaphore)
        {
            try { semaphore.Release(); }
            catch (ObjectDisposedException) { }
        }

        public void Dispose()
        {
            _writeLock.Dispose();
            _readLock.Dispose();
        }
    }
}
