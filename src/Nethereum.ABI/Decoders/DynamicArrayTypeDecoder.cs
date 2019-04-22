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
           //if (ElementType.IsDynamic())
            //{
            //    var dataLocation = new IntTypeDecoder().DecodeInt(encoded.Take(32).ToArray());

            //    var size = new IntTypeDecoder().DecodeInt(encoded.Skip(dataLocation).Take(32).ToArray());
            //    //Skip the length of the array and data location, just pass the array values
            //    return Decode(encoded.Skip(dataLocation + 32).ToArray(), type, size);
            //}
            //else
            //{
                var size = new IntTypeDecoder().DecodeInt(encoded.Take(32).ToArray());
                //Skip the length of the array, just pass the array values
                return Decode(encoded.Skip(32).ToArray(), type, size);
            //}
        }
    }
}