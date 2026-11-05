using System;
using System.Collections.Generic;
using System.Threading;

namespace Nethereum.RLP
{
    public class RLPCollection : List<IRLPElement>, IRLPElement
    {
        private sealed class SourceSlice
        {
            public SourceSlice(byte[] buffer, int offset, int length)
            {
                Buffer = buffer;
                Offset = offset;
                Length = length;
            }

            public byte[] Buffer { get; }
            public int Offset { get; }
            public int Length { get; }
        }

        private object _data;

        public byte[] RLPData
        {
            get
            {
                var data = _data;
                var bytes = data as byte[];
                if (bytes != null || data == null)
                    return bytes;

                var slice = (SourceSlice) data;
                bytes = new byte[slice.Length];
                Array.Copy(slice.Buffer, slice.Offset, bytes, 0, slice.Length);
                var winner = Interlocked.CompareExchange(ref _data, bytes, slice);
                return winner == (object) slice ? bytes : (winner as byte[] ?? bytes);
            }
            set { _data = value; }
        }

        internal void SetSource(byte[] buffer, int offset, int length)
        {
            if (offset < 0 || length < 0 || length > buffer.Length - offset)
                throw new ArgumentException("RLP list extends beyond the input");
            _data = new SourceSlice(buffer, offset, length);
        }
    }
}
