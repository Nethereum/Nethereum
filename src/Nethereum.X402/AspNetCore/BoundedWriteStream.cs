namespace Nethereum.X402.AspNetCore;

internal sealed class ResponseTooLargeException : Exception { }

internal sealed class BoundedWriteStream : Stream
{
    private readonly Stream _inner;
    private readonly long _limit;
    private long _written;

    public BoundedWriteStream(Stream inner, long limit)
    {
        _inner = inner;
        _limit = limit;
    }

    private void Track(long count)
    {
        _written += count;
        if (_written > _limit) throw new ResponseTooLargeException();
    }

    public override bool CanWrite => true;
    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }

    public override void Write(byte[] buffer, int offset, int count) { Track(count); _inner.Write(buffer, offset, count); }
    public override void Write(ReadOnlySpan<byte> buffer) { Track(buffer.Length); _inner.Write(buffer); }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { Track(count); return _inner.WriteAsync(buffer, offset, count, cancellationToken); }
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) { Track(buffer.Length); return _inner.WriteAsync(buffer, cancellationToken); }
    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
}
