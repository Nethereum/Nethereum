using System;
using System.Linq;

namespace Nethereum.ABI.Decoders
{
    public class DynamicArrayTypeDecoder : ArrayTypeDecoder
    {
        public DynamicArrayTypeDecoder(ABIType elementType) : base(elementType, -1)
        {
        }

        public override object Decode(byte[] encoded, Type type)
        {
            var head = new byte[Math.Min(32, encoded.Length)];
            Array.Copy(encoded, head, head.Length);
            var size = new IntTypeDecoder().DecodeInt(head);
            var values = new byte[Math.Max(0, encoded.Length - 32)];
            if (values.Length > 0) Array.Copy(encoded, 32, values, 0, values.Length);
            return Decode(values, type, size);
        }
    }
}