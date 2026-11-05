using System;
using System.Numerics;

namespace Nethereum.RLP
{
    public enum RlpKind
    {
        Byte,
        String,
        List
    }

    public readonly struct RlpListScope
    {
        internal RlpListScope(int start, int end, int parentLimit)
        {
            Start = start;
            End = end;
            ParentLimit = parentLimit;
        }

        public int Start { get; }
        public int End { get; }
        public int Length => End - Start;
        internal int ParentLimit { get; }
    }

    public readonly struct RlpSegment
    {
        public RlpSegment(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }

        public int Offset { get; }
        public int Length { get; }
    }

    public sealed class RlpReader
    {
        private readonly byte[] _data;
        private readonly int _end;
        private int _position;
        private int _limit;
        private int _depth;

        public RlpReader(byte[] data) : this(data, 0, data == null ? 0 : data.Length)
        {
        }

        public RlpReader(byte[] data, int offset, int length)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || length < 0 || offset > data.Length - length)
                throw new ArgumentOutOfRangeException(nameof(length));
            _data = data;
            _position = offset;
            _limit = offset + length;
            _end = _limit;
        }

        public int Position => _position;

        public bool IsAtEnd() => _position >= _limit;

        public bool IsAtEnd(RlpListScope scope) => _position >= scope.End;

        public RlpKind PeekKind()
        {
            if (_position >= _limit)
                throw new InvalidOperationException("RLP element starts beyond the input");
            var prefix = _data[_position];
            if (prefix < RLP.OFFSET_SHORT_ITEM) return RlpKind.Byte;
            if (prefix <= 0xbf) return RlpKind.String;
            return RlpKind.List;
        }

        public int PeekSize()
        {
            RLP.ReadHeader(_data, _position, _limit, out _, out var payloadLength);
            return payloadLength;
        }

        public RlpListScope ReadListHeader()
        {
            var isList = RLP.ReadHeader(_data, _position, _limit, out var payloadStart, out var payloadLength);
            if (!isList)
                throw new InvalidOperationException("RLP expected a list but found a string");
            if (_depth + 1 > RLP.MaxDecodeDepth)
                throw new InvalidOperationException("RLP list nesting exceeds " + RLP.MaxDecodeDepth);
            var scope = new RlpListScope(payloadStart, payloadStart + payloadLength, _limit);
            _position = payloadStart;
            _limit = scope.End;
            _depth++;
            return scope;
        }

        public void EnsureEnd(RlpListScope scope)
        {
            if (_position != scope.End)
                throw new InvalidOperationException("RLP list holds more elements than expected");
            _limit = scope.ParentLimit;
            _depth--;
        }

        public void EnsureEnd()
        {
            if (_depth != 0 || _position != _end)
                throw new InvalidOperationException("RLP input has bytes after the top-level value");
        }

        public int CountItems(RlpListScope scope)
        {
            var position = _position;
            var count = 0;
            while (position < scope.End)
            {
                RLP.ReadHeader(_data, position, scope.End, out var payloadStart, out var payloadLength);
                position = payloadStart + payloadLength;
                count++;
            }
            return count;
        }

        public byte[] ReadBytes()
        {
            ReadStringHeader(out var payloadStart, out var payloadLength);
            return CopyAndAdvance(payloadStart, payloadLength);
        }

        public byte[] ReadFixed(int length)
        {
            ReadStringHeader(out var payloadStart, out var payloadLength);
            if (payloadLength != length)
                throw new InvalidOperationException("RLP string of " + payloadLength + " bytes where " + length + " expected");
            return CopyAndAdvance(payloadStart, payloadLength);
        }

        public ulong ReadUInt64()
        {
            ReadStringHeader(out var payloadStart, out var payloadLength);
            if (payloadLength > 8)
                throw new InvalidOperationException("RLP integer does not fit in 64 bits");
            if (payloadLength > 0 && _data[payloadStart] == 0)
                throw new InvalidOperationException("RLP integer has a leading zero");
            ulong value = 0;
            for (var i = 0; i < payloadLength; i++)
                value = (value << 8) | _data[payloadStart + i];
            _position = payloadStart + payloadLength;
            return value;
        }

        public BigInteger ReadBigInteger()
        {
            ReadStringHeader(out var payloadStart, out var payloadLength);
            if (payloadLength > 0 && _data[payloadStart] == 0)
                throw new InvalidOperationException("RLP integer has a leading zero");
            return CopyAndAdvance(payloadStart, payloadLength).ToBigIntegerFromRLPDecoded();
        }

        public bool ReadBool()
        {
            var value = ReadUInt64();
            if (value > 1)
                throw new InvalidOperationException("RLP boolean must be 0 or 1");
            return value == 1;
        }

        public void Skip()
        {
            RLP.ReadHeader(_data, _position, _limit, out var payloadStart, out var payloadLength);
            _position = payloadStart + payloadLength;
        }

        public RlpSegment ReadRawSegment()
        {
            var start = _position;
            RLP.ReadHeader(_data, _position, _limit, out var payloadStart, out var payloadLength);
            _position = payloadStart + payloadLength;
            return new RlpSegment(start, _position - start);
        }

        public byte[] ReadRaw()
        {
            var segment = ReadRawSegment();
            var raw = new byte[segment.Length];
            Array.Copy(_data, segment.Offset, raw, 0, segment.Length);
            return raw;
        }

        public byte[] ReadData()
        {
            return PeekKind() == RlpKind.List ? ReadRaw() : ReadBytes();
        }

        private void ReadStringHeader(out int payloadStart, out int payloadLength)
        {
            var isList = RLP.ReadHeader(_data, _position, _limit, out payloadStart, out payloadLength);
            if (isList)
                throw new InvalidOperationException("RLP expected a string but found a list");
        }

        private byte[] CopyAndAdvance(int payloadStart, int payloadLength)
        {
            _position = payloadStart + payloadLength;
            if (payloadLength == 0) return RLP.EMPTY_BYTE_ARRAY;
            var item = new byte[payloadLength];
            Array.Copy(_data, payloadStart, item, 0, payloadLength);
            return item;
        }
    }
}
