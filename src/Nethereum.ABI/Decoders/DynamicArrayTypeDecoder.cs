using System;
using System.Linq;

namespace Nethereum.ABI.Decoders
{
    public class DynamicArrayTypeDecoder : ArrayTypeDecoder
    {
        public DynamicArrayTypeDecoder(ABIType elementType) : base(elementType)
        {
        }

        public override object Decode(byte[] encoded, Type type)
        {
            //Skip the length of the array, just pass the array values
            return base.Decode(encoded.Skip(32).ToArray(), type);
        }
    }
}