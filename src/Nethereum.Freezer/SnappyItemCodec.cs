using System;

namespace Nethereum.Freezer
{
    public sealed class SnappyItemCodec<T> : IItemCodec<T>
    {
        private readonly IItemCodec<T> _inner;

        public SnappyItemCodec(IItemCodec<T> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public byte[] Encode(T item)
        {
            var raw = _inner.Encode(item);
            return IronSnappy.Snappy.Encode(raw);
        }

        public T Decode(ReadOnlySpan<byte> bytes)
        {
            var raw = IronSnappy.Snappy.Decode(bytes);
            return _inner.Decode(raw);
        }
    }
}
